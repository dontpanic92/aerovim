// <copyright file="EditorControl.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Controls;

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using AeroVim.Editor;
using AeroVim.Editor.Utilities;
using AeroVim.Utilities;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using EditorScreen = AeroVim.Editor.Screen;

/// <summary>
/// The editor control using Avalonia custom rendering with SkiaSharp.
/// </summary>
public class EditorControl : Control, IDisposable
{
    private readonly IEditorClient editorClient;
    private readonly ConcurrentQueue<Action> pendingActions = new();
    private readonly Dictionary<TypefaceKey, SKTypeface> typefaceCache = new();
    private readonly Dictionary<GlyphKey, SKTypeface> glyphTypefaceCache = new();
    private readonly SKPaint backgroundPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };
    private readonly SKPaint textPaint = new() { IsAntialias = true, IsLinearText = false, SubpixelText = true };
    private readonly SKPaint underlinePaint = new() { StrokeWidth = 1, IsAntialias = true };
    private readonly SKPaint undercurlPaint = new() { StrokeWidth = 1, IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint cursorPaint = new() { BlendMode = SKBlendMode.Difference, Color = SKColors.White };
    private readonly SKPaint preeditUnderlinePaint = new() { StrokeWidth = 2, IsAntialias = true, Color = SKColors.White };
    private readonly EditorTextInputMethodClient imeClient;

    private TextLayoutParameters textParam;
    private SKTypeface? primaryTypeface;
    private SKTypeface? emojiTypeface;
    private bool isDisposed;
    private string? pressedMouseButton;
    private int redrawQueued;

    /// <summary>
    /// Initializes a new instance of the <see cref="EditorControl"/> class.
    /// </summary>
    /// <param name="editorClient">The editor client.</param>
    public EditorControl(IEditorClient editorClient)
    {
        this.editorClient = editorClient;
        this.editorClient.Redraw += this.OnRedraw;
        this.editorClient.FontChanged += this.OnFontChanged;

        this.ClipToBounds = true;
        this.Focusable = true;

        this.imeClient = new EditorTextInputMethodClient(this);
        this.AddHandler(
            InputElement.TextInputMethodClientRequestedEvent,
            (_, e) =>
            {
                e.Client = this.imeClient;
            });

        var defaultFont = Utilities.Helpers.GetDefaultMonospaceFontName();
        this.primaryTypeface = this.CreateValidatedTypeface(defaultFont);
        this.typefaceCache[new TypefaceKey(this.primaryTypeface.FamilyName, SKFontStyleWeight.Normal, SKFontStyleSlant.Upright)] = this.primaryTypeface;
        this.textParam = new TextLayoutParameters(this.primaryTypeface.FamilyName, 11);
        this.textPaint.TextSize = this.textParam.SkiaFontSize;
    }

    /// <summary>
    /// Gets or sets a value indicating whether font ligature is enabled.
    /// </summary>
    public bool EnableLigature { get; set; }

    /// <summary>
    /// Gets a value indicating whether an IME composition is in progress.
    /// </summary>
    public bool IsComposing => this.imeClient.IsComposing;

    /// <summary>
    /// Gets or sets the alpha channel used for the default background.
    /// </summary>
    public byte BackgroundAlpha { get; set; } = byte.MaxValue;

    /// <summary>
    /// Gets the desired row count.
    /// </summary>
    public uint DesiredRowCount
    {
        get
        {
            var c = (uint)(this.Bounds.Height / this.textParam.LineHeight);
            return c == 0 ? 1 : c;
        }
    }

    /// <summary>
    /// Gets the desired column count.
    /// </summary>
    public uint DesiredColCount
    {
        get
        {
            var c = (uint)(this.Bounds.Width / this.textParam.CharWidth);
            return c == 0 ? 1 : c;
        }
    }

    /// <summary>
    /// Input text to the editor.
    /// </summary>
    /// <param name="text">The input text sequence.</param>
    public void Input(string text)
    {
        this.editorClient.Input(text);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var customOp = new EditorDrawOperation(this, new Rect(0, 0, this.Bounds.Width, this.Bounds.Height));
        context.Custom(customOp);
    }

    /// <summary>
    /// Dispose the control.
    /// </summary>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        return availableSize;
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            this.editorClient.TryResize(this.DesiredColCount, this.DesiredRowCount);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);
        var (row, col) = this.PixelToGridPosition(point.Position);
        var modifier = this.GetModifierString(e.KeyModifiers);

        if (point.Properties.IsLeftButtonPressed)
        {
            this.pressedMouseButton = "left";
        }
        else if (point.Properties.IsRightButtonPressed)
        {
            this.pressedMouseButton = "right";
        }
        else if (point.Properties.IsMiddleButtonPressed)
        {
            this.pressedMouseButton = "middle";
        }
        else
        {
            return;
        }

        this.editorClient.InputMouse(this.pressedMouseButton, "press", modifier, 0, row, col);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (this.pressedMouseButton is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        var (row, col) = this.PixelToGridPosition(point.Position);
        var modifier = this.GetModifierString(e.KeyModifiers);

        this.editorClient.InputMouse(this.pressedMouseButton, "drag", modifier, 0, row, col);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (this.pressedMouseButton is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        var (row, col) = this.PixelToGridPosition(point.Position);
        var modifier = this.GetModifierString(e.KeyModifiers);

        this.editorClient.InputMouse(this.pressedMouseButton, "release", modifier, 0, row, col);
        this.pressedMouseButton = null;
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var point = e.GetCurrentPoint(this);
        var (row, col) = this.PixelToGridPosition(point.Position);
        var modifier = this.GetModifierString(e.KeyModifiers);

        if (e.Delta.Y > 0)
        {
            this.editorClient.InputMouse("wheel", "up", modifier, 0, row, col);
        }
        else if (e.Delta.Y < 0)
        {
            this.editorClient.InputMouse("wheel", "down", modifier, 0, row, col);
        }

        if (e.Delta.X > 0)
        {
            this.editorClient.InputMouse("wheel", "right", modifier, 0, row, col);
        }
        else if (e.Delta.X < 0)
        {
            this.editorClient.InputMouse("wheel", "left", modifier, 0, row, col);
        }

        e.Handled = true;
    }

    /// <summary>
    /// Dispose managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose, false from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this.isDisposed)
        {
            if (disposing)
            {
                this.editorClient.Redraw -= this.OnRedraw;
                this.editorClient.FontChanged -= this.OnFontChanged;
                this.DisposeCachedResources();
            }

            this.isDisposed = true;
        }
    }

    private static bool IsEmojiCodePoint(int codePoint)
    {
        return (codePoint >= 0x2600 && codePoint <= 0x27BF)
            || (codePoint >= 0x2B05 && codePoint <= 0x2B55)
            || (codePoint >= 0x1F000 && codePoint <= 0x1FAFF)
            || (codePoint >= 0x1F1E0 && codePoint <= 0x1F1FF)
            || codePoint == 0x200D
            || codePoint == 0xFE0F;
    }

    /// <summary>
    /// Creates a typeface and validates it resolved to the requested font family.
    /// Falls back to the SkiaSharp default typeface when the font cannot be found.
    /// </summary>
    private SKTypeface CreateValidatedTypeface(string fontName)
    {
        var typeface = SKTypeface.FromFamilyName(fontName);
        if (string.Equals(typeface.FamilyName, fontName, StringComparison.OrdinalIgnoreCase))
        {
            return typeface;
        }

        // The requested font wasn't found; SkiaSharp silently substituted another.
        System.Diagnostics.Trace.TraceWarning(
            $"AeroVim: Font \"{fontName}\" not found (resolved to \"{typeface.FamilyName}\"). Using SkiaSharp default.");
        typeface.Dispose();
        return SKTypeface.Default;
    }

    private (int Row, int Col) PixelToGridPosition(Point pixel)
    {
        int row = (int)(pixel.Y / this.textParam.LineHeight);
        int col = (int)(pixel.X / this.textParam.CharWidth);

        int maxRow = (int)this.DesiredRowCount - 1;
        int maxCol = (int)this.DesiredColCount - 1;

        row = Math.Clamp(row, 0, maxRow);
        col = Math.Clamp(col, 0, maxCol);

        return (row, col);
    }

    private string GetModifierString(KeyModifiers modifiers)
    {
        var parts = string.Empty;

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts += "S";
        }

        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            parts += "C";
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts += "A";
        }

        return parts;
    }

    private void OnRedraw()
    {
        if (Interlocked.CompareExchange(ref this.redrawQueued, 1, 0) != 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            this.UpdateImeCursorRectangle();
            this.InvalidateVisual();
            Interlocked.Exchange(ref this.redrawQueued, 0);
        });
    }

    private void OnFontChanged(FontSettings font)
    {
        this.pendingActions.Enqueue(() =>
        {
            var weight = font.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
            var slant = font.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
            var newTypeface = SKTypeface.FromFamilyName(font.FontName, weight, SKFontStyleWidth.Normal, slant);

            if (!string.Equals(newTypeface?.FamilyName, font.FontName, StringComparison.OrdinalIgnoreCase))
            {
                newTypeface?.Dispose();
                System.Diagnostics.Trace.TraceWarning(
                    $"AeroVim: Font \"{font.FontName}\" not found (resolved to \"{newTypeface?.FamilyName}\"). Keeping current font.");
                return;
            }

            this.DisposeTypefaceCaches();
            this.primaryTypeface = newTypeface;
            this.typefaceCache[new TypefaceKey(this.primaryTypeface!.FamilyName, weight, slant)] = this.primaryTypeface;
            this.textParam = new TextLayoutParameters(font.FontName, font.FontPointSize);
            this.textPaint.TextSize = this.textParam.SkiaFontSize;
            this.editorClient.TryResize(this.DesiredColCount, this.DesiredRowCount);
        });

        Dispatcher.UIThread.Post(() => this.InvalidateVisual());
    }

    private void RenderWithSkia(SKCanvas canvas)
    {
        while (this.pendingActions.TryDequeue(out var action))
        {
            action();
        }

        var args = this.editorClient.GetScreen();
        if (args is null)
        {
            return;
        }

        Interlocked.Exchange(ref this.redrawQueued, 0);
        canvas.Clear(Helpers.GetSkColor(args.BackgroundColor, this.BackgroundAlpha));

        int rows = args.Cells.GetLength(0);
        int cols = args.Cells.GetLength(1);

        // Paint backgrounds
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                if (args.Cells[i, j].BackgroundColor != args.BackgroundColor || args.Cells[i, j].Reverse)
                {
                    float x = j * this.textParam.CharWidth;
                    float y = i * this.textParam.LineHeight;
                    int color = args.Cells[i, j].Reverse
                        ? args.Cells[i, j].ForegroundColor
                        : args.Cells[i, j].BackgroundColor;
                    this.backgroundPaint.Color = Helpers.GetSkColor(color);
                    canvas.DrawRect(x, y, this.textParam.CharWidth, this.textParam.LineHeight, this.backgroundPaint);
                }
            }
        }

        // Paint foreground text
        for (int i = 0; i < rows; i++)
        {
            int j = 0;
            while (j < cols)
            {
                // Group cells with the same style for correct ligature rendering
                int cellRangeStart = j;
                int cellRangeEnd = j;
                Cell startCell = args.Cells[i, cellRangeStart];
                while (cellRangeEnd < cols)
                {
                    Cell cell = args.Cells[i, cellRangeEnd];
                    if (cell.Character is not null
                        && (cell.ForegroundColor != startCell.ForegroundColor
                            || cell.BackgroundColor != startCell.BackgroundColor
                            || cell.SpecialColor != startCell.SpecialColor
                            || cell.Italic != startCell.Italic
                            || cell.Bold != startCell.Bold
                            || cell.Reverse != startCell.Reverse
                            || cell.Undercurl != startCell.Undercurl
                            || cell.Underline != startCell.Underline))
                    {
                        break;
                    }

                    cellRangeEnd++;
                }

                j = cellRangeEnd;

                this.DrawCellRange(canvas, args, i, cellRangeStart, cellRangeEnd);
            }
        }

        // Draw cursor
        this.DrawCursor(canvas, args);

        // Draw preedit (IME composition) overlay
        this.DrawPreedit(canvas, args);
    }

    private void DrawCellRange(SKCanvas canvas, EditorScreen args, int row, int colStart, int colEnd)
    {
        bool bold = args.Cells[row, colStart].Bold;
        bool italic = args.Cells[row, colStart].Italic;
        bool underline = args.Cells[row, colStart].Underline;
        bool undercurl = args.Cells[row, colStart].Undercurl;
        int foregroundColor = args.Cells[row, colStart].Reverse
            ? args.Cells[row, colStart].BackgroundColor
            : args.Cells[row, colStart].ForegroundColor;

        var weight = bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        var slant = italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        var styledTypeface = this.GetStyledTypeface(weight, slant);
        this.textPaint.Color = Helpers.GetSkColor(foregroundColor);
        this.textPaint.FakeBoldText = bold;
        this.textPaint.Typeface = styledTypeface;

        float baselineY = (row * this.textParam.LineHeight) + (this.textParam.LineHeight * 0.8f);

        // Draw each cell's character individually at its fixed position
        // This preserves the terminal grid alignment while still supporting proper glyph rendering
        int cellIndex = colStart;
        while (cellIndex < colEnd)
        {
            var cell = args.Cells[row, cellIndex];
            if (cell.Character is null)
            {
                cellIndex++;
                continue;
            }

            string text = cell.Character;
            int codePoint = char.ConvertToUtf32(text, 0);
            float x = cellIndex * this.textParam.CharWidth;

            this.textPaint.Typeface = this.GetTypefaceForGlyph(codePoint, weight, slant, styledTypeface, text);

            canvas.DrawText(text, x, baselineY, this.textPaint);

            // Restore the styled typeface for subsequent characters
            this.textPaint.Typeface = styledTypeface;

            int charWidth = this.GetCharWidth(args.Cells, row, cellIndex);
            cellIndex += charWidth;
        }

        // Draw underline
        if (underline)
        {
            this.underlinePaint.Color = Helpers.GetSkColor(foregroundColor);
            float ulY = ((row + 1) * this.textParam.LineHeight) - 1;
            canvas.DrawLine(colStart * this.textParam.CharWidth, ulY, colEnd * this.textParam.CharWidth, ulY, this.underlinePaint);
        }

        // Draw undercurl
        if (undercurl)
        {
            int specialColor = args.Cells[row, colStart].SpecialColor;
            this.undercurlPaint.Color = Helpers.GetSkColor(specialColor);
            float curlY = ((row + 1) * this.textParam.LineHeight) - 2;
            using var path = new SKPath();
            float startX = colStart * this.textParam.CharWidth;
            float endX = colEnd * this.textParam.CharWidth;
            path.MoveTo(startX, curlY);
            for (float cx = startX; cx < endX; cx += 4)
            {
                path.QuadTo(cx + 2, curlY - 2, cx + 4, curlY);
            }

            canvas.DrawPath(path, this.undercurlPaint);
        }
    }

    private void DrawCursor(SKCanvas canvas, EditorScreen args)
    {
        var cursorPercentage = this.editorClient.ModeInfo?.CellPercentage ?? 100;
        var cursorShape = this.editorClient.ModeInfo?.CursorShape ?? CursorShape.Block;
        int cellWidth = this.GetCharWidth(args.Cells, args.CursorPosition.Row, args.CursorPosition.Col);

        float left, top, right, bottom;
        switch (cursorShape)
        {
            case CursorShape.Vertical:
                left = args.CursorPosition.Col * this.textParam.CharWidth;
                top = args.CursorPosition.Row * this.textParam.LineHeight;
                right = (args.CursorPosition.Col + (cursorPercentage / 100f)) * this.textParam.CharWidth;
                bottom = (args.CursorPosition.Row + 1) * this.textParam.LineHeight;
                break;
            case CursorShape.Horizontal:
                float topMargin = this.textParam.LineHeight * (100 - cursorPercentage) / 100f;
                left = args.CursorPosition.Col * this.textParam.CharWidth;
                top = (args.CursorPosition.Row * this.textParam.LineHeight) + topMargin;
                right = (args.CursorPosition.Col + cellWidth) * this.textParam.CharWidth;
                bottom = (args.CursorPosition.Row + 1) * this.textParam.LineHeight;
                break;
            default: // Block
                left = args.CursorPosition.Col * this.textParam.CharWidth;
                top = args.CursorPosition.Row * this.textParam.LineHeight;
                right = (args.CursorPosition.Col + cellWidth) * this.textParam.CharWidth;
                bottom = (args.CursorPosition.Row + 1) * this.textParam.LineHeight;
                break;
        }

        var cursorRect = new SKRect(left, top, right, bottom);

        // Draw cursor as inverted rectangle
        canvas.DrawRect(cursorRect, this.cursorPaint);
    }

    private void DrawPreedit(SKCanvas canvas, EditorScreen args)
    {
        string? preedit = this.imeClient.PreeditText;
        if (preedit is null)
        {
            return;
        }

        float x = args.CursorPosition.Col * this.textParam.CharWidth;
        float y = args.CursorPosition.Row * this.textParam.LineHeight;
        float baselineY = y + (this.textParam.LineHeight * 0.8f);

        // Draw a background behind the preedit text
        float textWidth = this.textPaint.MeasureText(preedit);
        this.backgroundPaint.Color = Helpers.GetSkColor(args.BackgroundColor);
        canvas.DrawRect(x, y, textWidth, this.textParam.LineHeight, this.backgroundPaint);

        // Draw the preedit text at the cursor position
        this.textPaint.Color = Helpers.GetSkColor(args.ForegroundColor);
        canvas.DrawText(preedit, x, baselineY, this.textPaint);

        // Draw an underline to indicate composition in progress
        float underlineY = y + this.textParam.LineHeight - 1;
        this.preeditUnderlinePaint.Color = Helpers.GetSkColor(args.ForegroundColor);
        canvas.DrawLine(x, underlineY, x + textWidth, underlineY, this.preeditUnderlinePaint);
    }

    private void UpdateImeCursorRectangle()
    {
        var screen = this.editorClient.GetScreen();
        if (screen is null)
        {
            return;
        }

        float x = screen.CursorPosition.Col * this.textParam.CharWidth;
        float y = screen.CursorPosition.Row * this.textParam.LineHeight;
        this.imeClient.UpdateCursorRectangle(new Rect(x, y, this.textParam.CharWidth, this.textParam.LineHeight));
    }

    private int GetCharWidth(Cell[,] screen, int row, int col)
    {
        if (col >= screen.GetLength(1) - 1)
        {
            return 1;
        }

        if (screen[row, col + 1].Character is null)
        {
            return 2;
        }

        return 1;
    }

    private void DisposeCachedResources()
    {
        this.DisposeTypefaceCaches();
        this.backgroundPaint.Dispose();
        this.textPaint.Dispose();
        this.underlinePaint.Dispose();
        this.undercurlPaint.Dispose();
        this.cursorPaint.Dispose();
        this.preeditUnderlinePaint.Dispose();
    }

    private void DisposeTypefaceCaches()
    {
        foreach (var typeface in this.typefaceCache.Values)
        {
            typeface.Dispose();
        }

        var disposedFallbacks = new HashSet<SKTypeface>();
        foreach (var typeface in this.glyphTypefaceCache.Values)
        {
            if (typeface is not null)
            {
                disposedFallbacks.Add(typeface);
            }
        }

        foreach (var typeface in disposedFallbacks)
        {
            if (!this.typefaceCache.ContainsValue(typeface))
            {
                typeface.Dispose();
            }
        }

        this.typefaceCache.Clear();
        this.glyphTypefaceCache.Clear();

        this.emojiTypeface?.Dispose();
        this.emojiTypeface = null;
    }

    private SKTypeface GetStyledTypeface(SKFontStyleWeight weight, SKFontStyleSlant slant)
    {
        var key = new TypefaceKey(this.textParam.FontName, weight, slant);
        if (!this.typefaceCache.TryGetValue(key, out var typeface))
        {
            typeface = SKTypeface.FromFamilyName(this.textParam.FontName, weight, SKFontStyleWidth.Normal, slant) ?? this.primaryTypeface!;
            this.typefaceCache[key] = typeface;
        }

        return typeface!;
    }

    private SKTypeface? GetTypefaceForGlyph(int codePoint, SKFontStyleWeight weight, SKFontStyleSlant slant, SKTypeface styledTypeface, string text)
    {
        var key = new GlyphKey(this.textParam.FontName, weight, slant, codePoint);
        if (!this.glyphTypefaceCache.TryGetValue(key, out var fallbackTypeface))
        {
            if (styledTypeface is not null && styledTypeface.ContainsGlyphs(text))
            {
                fallbackTypeface = styledTypeface;
            }
            else
            {
                // For emoji codepoints, try the platform emoji font first since
                // SKFontManager.MatchCharacter may not reliably select a color emoji font.
                if (IsEmojiCodePoint(codePoint))
                {
                    fallbackTypeface = this.GetEmojiTypeface();
                    if (fallbackTypeface is not null && !fallbackTypeface.ContainsGlyphs(text))
                    {
                        fallbackTypeface = null;
                    }
                }

                fallbackTypeface ??= SKFontManager.Default.MatchCharacter(codePoint);
            }

            this.glyphTypefaceCache[key] = (fallbackTypeface ?? styledTypeface)!;
        }

        return fallbackTypeface ?? styledTypeface;
    }

    private SKTypeface? GetEmojiTypeface()
    {
        if (this.emojiTypeface is not null)
        {
            return this.emojiTypeface;
        }

        string[] candidates;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            candidates = ["Segoe UI Emoji", "Segoe UI Symbol"];
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            candidates = ["Apple Color Emoji"];
        }
        else
        {
            candidates = ["Noto Color Emoji", "Noto Emoji", "Emoji One"];
        }

        foreach (var name in candidates)
        {
            var typeface = SKTypeface.FromFamilyName(name);
            if (typeface is not null && typeface.FamilyName.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                this.emojiTypeface = typeface;
                return this.emojiTypeface;
            }

            typeface?.Dispose();
        }

        return null;
    }

    private readonly struct TypefaceKey : IEquatable<TypefaceKey>
    {
        public TypefaceKey(string fontName, SKFontStyleWeight weight, SKFontStyleSlant slant)
        {
            this.FontName = fontName;
            this.Weight = weight;
            this.Slant = slant;
        }

        public string FontName { get; }

        public SKFontStyleWeight Weight { get; }

        public SKFontStyleSlant Slant { get; }

        public bool Equals(TypefaceKey other)
        {
            return string.Equals(this.FontName, other.FontName, StringComparison.Ordinal)
                && this.Weight == other.Weight
                && this.Slant == other.Slant;
        }

        public override bool Equals(object? obj)
        {
            return obj is TypefaceKey other && this.Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.FontName, this.Weight, this.Slant);
        }
    }

    private readonly struct GlyphKey : IEquatable<GlyphKey>
    {
        public GlyphKey(string fontName, SKFontStyleWeight weight, SKFontStyleSlant slant, int codePoint)
        {
            this.FontName = fontName;
            this.Weight = weight;
            this.Slant = slant;
            this.CodePoint = codePoint;
        }

        public string FontName { get; }

        public SKFontStyleWeight Weight { get; }

        public SKFontStyleSlant Slant { get; }

        public int CodePoint { get; }

        public bool Equals(GlyphKey other)
        {
            return string.Equals(this.FontName, other.FontName, StringComparison.Ordinal)
                && this.Weight == other.Weight
                && this.Slant == other.Slant
                && this.CodePoint == other.CodePoint;
        }

        public override bool Equals(object? obj)
        {
            return obj is GlyphKey other && this.Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.FontName, this.Weight, this.Slant, this.CodePoint);
        }
    }

    /// <summary>
    /// Custom draw operation for rendering the editor grid with SkiaSharp.
    /// </summary>
    private sealed class EditorDrawOperation : ICustomDrawOperation
    {
        private readonly EditorControl control;

        public EditorDrawOperation(EditorControl control, Rect bounds)
        {
            this.control = control;
            this.Bounds = bounds;
        }

        public Rect Bounds { get; }

        public void Dispose()
        {
        }

        public bool Equals(ICustomDrawOperation? other) => false;

        public bool HitTest(Point p) => this.Bounds.Contains(p);

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature is null)
            {
                return;
            }

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            this.control.RenderWithSkia(canvas);
        }
    }

    /// <summary>
    /// Font metrics for the terminal grid.
    /// </summary>
    private sealed class TextLayoutParameters
    {
        public TextLayoutParameters(string fontName, float pointSize)
        {
            this.FontName = fontName;
            this.PointSize = pointSize;
            this.SkiaFontSize = pointSize * 96f / 72f;

            using var typeface = SKTypeface.FromFamilyName(fontName);
            using var paint = new SKPaint();
            paint.Typeface = typeface;
            paint.TextSize = this.SkiaFontSize;
            paint.IsAntialias = true;

            var metrics = paint.FontMetrics;
            this.LineHeight = (float)Math.Ceiling(-metrics.Ascent + metrics.Descent + metrics.Leading);
            this.CharWidth = paint.MeasureText("A");

            // Round to pixels to prevent sub-pixel artifacts in the grid
            this.LineHeight = (float)Math.Ceiling(this.LineHeight);
            this.CharWidth = (float)Math.Ceiling(this.CharWidth);
        }

        public string FontName { get; }

        public float PointSize { get; }

        public float SkiaFontSize { get; }

        public float LineHeight { get; }

        public float CharWidth { get; }
    }
}
