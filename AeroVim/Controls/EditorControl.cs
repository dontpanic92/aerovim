// <copyright file="EditorControl.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Controls;

using System.Collections.Concurrent;
using System.Text;
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
    private static readonly TimeSpan DefaultCursorBlinkInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan DefaultCursorBlinkWait = TimeSpan.FromMilliseconds(700);

    private readonly IEditorClient editorClient;
    private readonly ConcurrentQueue<Action> pendingActions = new();
    private readonly SKPaint backgroundPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };
    private readonly SKPaint textPaint = new() { IsAntialias = true, IsLinearText = false, SubpixelText = true };
    private readonly SKPaint underlinePaint = new() { StrokeWidth = 1, IsAntialias = true };
    private readonly SKPaint undercurlPaint = new() { StrokeWidth = 1, IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint cursorPaint = new() { BlendMode = SKBlendMode.Difference, Color = SKColors.White };
    private readonly SKPaint preeditUnderlinePaint = new() { StrokeWidth = 2, IsAntialias = true, Color = SKColors.White };
    private readonly EditorTextInputMethodClient imeClient;
    private readonly FontFallbackChain fontChain = new FontFallbackChain();
    private readonly LigatureTextShaper ligatureTextShaper = new();

    private TextLayoutParameters textParam;
    private List<string> currentGuiFontNames = new List<string>();
    private List<string> currentUserFallbackFonts = new List<string>();
    private volatile bool isDisposed;
    private Cursor? pointerCursor;
    private DispatcherTimer? cursorBlinkTimer;
    private string? pressedMouseButton;
    private StandardCursorType? resolvedPointerCursorType;
    private bool cursorBlinkVisible = true;
    private bool cursorBlinkStarted;
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

        this.RebuildFontChain([]);
        this.textParam = new TextLayoutParameters(this.fontChain.PrimaryFontName, 11);
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
    /// Gets the currently resolved pointer cursor type for tests.
    /// </summary>
    internal StandardCursorType? ResolvedPointerCursorType => this.resolvedPointerCursorType;

    /// <summary>
    /// Sets the user-configured fallback font list and rebuilds the font chain.
    /// </summary>
    /// <param name="fonts">Ordered list of user fallback font names.</param>
    public void SetFallbackFonts(List<string> fonts)
    {
        this.currentUserFallbackFonts = fonts;
        this.pendingActions.Enqueue(() =>
        {
            this.fontChain.Rebuild(
                this.currentGuiFontNames,
                this.currentUserFallbackFonts,
                Utilities.Helpers.GetDefaultFallbackFontNames());

            if (this.fontChain.PrimaryFontName.Length > 0)
            {
                this.textParam = new TextLayoutParameters(this.fontChain.PrimaryFontName, this.textParam.PointSize);
                this.textPaint.TextSize = this.textParam.SkiaFontSize;
                this.editorClient.TryResize(this.DesiredColCount, this.DesiredRowCount);
            }
        });

        Dispatcher.UIThread.Post(() => this.InvalidateVisual());
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

    /// <summary>
    /// Renders the control directly to a supplied Skia canvas for tests.
    /// </summary>
    /// <param name="canvas">The target Skia canvas.</param>
    internal void RenderForTesting(SKCanvas canvas)
    {
        this.RenderWithSkia(canvas);
    }

    /// <summary>
    /// Sets the IME preedit text for renderer tests.
    /// </summary>
    /// <param name="preeditText">The preedit text, or <c>null</c> to clear it.</param>
    /// <param name="cursorPos">The cursor position within the preedit text.</param>
    internal void SetPreeditTextForTesting(string? preeditText, int? cursorPos)
    {
        this.imeClient.SetPreeditText(preeditText, cursorPos);
    }

    /// <summary>
    /// Recomputes editor UI state from the current backend capability snapshot for tests.
    /// </summary>
    internal void RefreshEditorUiStateForTesting()
    {
        this.ApplyEditorUiState(this.editorClient.ModeInfo, resetCursorBlink: true);
    }

    /// <summary>
    /// Sets the current cursor blink visibility state for renderer tests.
    /// </summary>
    /// <param name="visible">A value indicating whether the cursor should be rendered as visible.</param>
    internal void SetCursorBlinkVisibleForTesting(bool visible)
    {
        this.cursorBlinkVisible = visible;
    }

    /// <summary>
    /// Handles a pointer press using grid coordinates for tests.
    /// </summary>
    /// <param name="button">The button name.</param>
    /// <param name="row">The zero-based grid row.</param>
    /// <param name="col">The zero-based grid column.</param>
    /// <param name="modifiers">The active key modifiers.</param>
    /// <returns>A value indicating whether the event was handled.</returns>
    internal bool HandlePointerPressedForTesting(string button, int row, int col, KeyModifiers modifiers = KeyModifiers.None)
    {
        return this.HandlePointerPressedCore(button, row, col, modifiers);
    }

    /// <summary>
    /// Handles a pointer drag using grid coordinates for tests.
    /// </summary>
    /// <param name="row">The zero-based grid row.</param>
    /// <param name="col">The zero-based grid column.</param>
    /// <param name="modifiers">The active key modifiers.</param>
    /// <returns>A value indicating whether the event was handled.</returns>
    internal bool HandlePointerMovedForTesting(int row, int col, KeyModifiers modifiers = KeyModifiers.None)
    {
        return this.HandlePointerMovedCore(row, col, modifiers);
    }

    /// <summary>
    /// Handles a pointer release using grid coordinates for tests.
    /// </summary>
    /// <param name="row">The zero-based grid row.</param>
    /// <param name="col">The zero-based grid column.</param>
    /// <param name="modifiers">The active key modifiers.</param>
    /// <returns>A value indicating whether the event was handled.</returns>
    internal bool HandlePointerReleasedForTesting(int row, int col, KeyModifiers modifiers = KeyModifiers.None)
    {
        return this.HandlePointerReleasedCore(row, col, modifiers);
    }

    /// <summary>
    /// Handles a pointer wheel event using grid coordinates for tests.
    /// </summary>
    /// <param name="row">The zero-based grid row.</param>
    /// <param name="col">The zero-based grid column.</param>
    /// <param name="delta">The wheel delta.</param>
    /// <param name="modifiers">The active key modifiers.</param>
    /// <returns>A value indicating whether the event was handled.</returns>
    internal bool HandlePointerWheelForTesting(int row, int col, Vector delta, KeyModifiers modifiers = KeyModifiers.None)
    {
        return this.HandlePointerWheelCore(row, col, delta, modifiers);
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
        string? button = null;

        if (point.Properties.IsLeftButtonPressed)
        {
            button = "left";
        }
        else if (point.Properties.IsRightButtonPressed)
        {
            button = "right";
        }
        else if (point.Properties.IsMiddleButtonPressed)
        {
            button = "middle";
        }

        e.Handled = this.HandlePointerPressedCore(button, row, col, e.KeyModifiers);
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var point = e.GetCurrentPoint(this);
        var (row, col) = this.PixelToGridPosition(point.Position);
        e.Handled = this.HandlePointerMovedCore(row, col, e.KeyModifiers);
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        var point = e.GetCurrentPoint(this);
        var (row, col) = this.PixelToGridPosition(point.Position);
        e.Handled = this.HandlePointerReleasedCore(row, col, e.KeyModifiers);
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var point = e.GetCurrentPoint(this);
        var (row, col) = this.PixelToGridPosition(point.Position);
        e.Handled = this.HandlePointerWheelCore(row, col, e.Delta, e.KeyModifiers);
    }

    /// <summary>
    /// Dispose managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose, false from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this.isDisposed)
        {
            // Set the flag BEFORE disposing resources so any concurrent
            // render (on the composition thread) sees the flag and bails
            // out before touching disposed Skia objects.
            this.isDisposed = true;

            if (disposing)
            {
                this.editorClient.Redraw -= this.OnRedraw;
                this.editorClient.FontChanged -= this.OnFontChanged;

                // Defer resource disposal to the next UI-thread cycle so
                // any in-flight render operation that already passed the
                // isDisposed check can complete safely.
                Dispatcher.UIThread.Post(this.DisposeCachedResources, DispatcherPriority.Background);
            }
        }
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

    private bool HandlePointerPressedCore(string? button, int row, int col, KeyModifiers modifiers)
    {
        if (button is null)
        {
            return false;
        }

        if (!this.editorClient.MouseEnabled)
        {
            this.pressedMouseButton = null;
            return false;
        }

        this.pressedMouseButton = button;
        this.editorClient.InputMouse(button, "press", this.GetModifierString(modifiers), 0, row, col);
        return true;
    }

    private bool HandlePointerMovedCore(int row, int col, KeyModifiers modifiers)
    {
        if (this.pressedMouseButton is null)
        {
            return false;
        }

        if (!this.editorClient.MouseEnabled)
        {
            this.pressedMouseButton = null;
            return false;
        }

        this.editorClient.InputMouse(this.pressedMouseButton, "drag", this.GetModifierString(modifiers), 0, row, col);
        return true;
    }

    private bool HandlePointerReleasedCore(int row, int col, KeyModifiers modifiers)
    {
        if (this.pressedMouseButton is null)
        {
            return false;
        }

        if (!this.editorClient.MouseEnabled)
        {
            this.pressedMouseButton = null;
            return false;
        }

        this.editorClient.InputMouse(this.pressedMouseButton, "release", this.GetModifierString(modifiers), 0, row, col);
        this.pressedMouseButton = null;
        return true;
    }

    private bool HandlePointerWheelCore(int row, int col, Vector delta, KeyModifiers modifiers)
    {
        if (!this.editorClient.MouseEnabled)
        {
            return false;
        }

        var modifier = this.GetModifierString(modifiers);
        bool handled = false;
        if (delta.Y > 0)
        {
            this.editorClient.InputMouse("wheel", "up", modifier, 0, row, col);
            handled = true;
        }
        else if (delta.Y < 0)
        {
            this.editorClient.InputMouse("wheel", "down", modifier, 0, row, col);
            handled = true;
        }

        if (delta.X > 0)
        {
            this.editorClient.InputMouse("wheel", "right", modifier, 0, row, col);
            handled = true;
        }
        else if (delta.X < 0)
        {
            this.editorClient.InputMouse("wheel", "left", modifier, 0, row, col);
            handled = true;
        }

        return handled;
    }

    private void OnRedraw()
    {
        if (Interlocked.CompareExchange(ref this.redrawQueued, 1, 0) != 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            this.ApplyEditorUiState(this.editorClient.ModeInfo, resetCursorBlink: true);
            this.UpdateImeCursorRectangle();
            this.InvalidateVisual();
            Interlocked.Exchange(ref this.redrawQueued, 0);
        });
    }

    private void OnFontChanged(FontSettings font)
    {
        this.pendingActions.Enqueue(() =>
        {
            this.currentGuiFontNames = font.FontNames;
            this.RebuildFontChain(font.FontNames);

            if (this.fontChain.PrimaryFontName.Length == 0)
            {
                System.Diagnostics.Trace.TraceWarning(
                    $"AeroVim: None of the guifont names resolved to a valid font. Keeping current font.");
                return;
            }

            this.textParam = new TextLayoutParameters(this.fontChain.PrimaryFontName, font.FontPointSize);
            this.textPaint.TextSize = this.textParam.SkiaFontSize;
            this.editorClient.TryResize(this.DesiredColCount, this.DesiredRowCount);
        });

        Dispatcher.UIThread.Post(() => this.InvalidateVisual());
    }

    private void RenderWithSkia(SKCanvas canvas)
    {
        if (this.isDisposed)
        {
            return;
        }

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

        // Capture the Cells reference once. The Screen object is shared
        // and its Cells array can be replaced by a concurrent GetScreen()
        // call on the redraw thread. Using a local prevents the array from
        // changing size between reading dimensions and accessing elements.
        var cells = args.Cells;
        var modeInfo = this.editorClient.ModeInfo;
        int rows = cells.GetLength(0);
        int cols = cells.GetLength(1);

        // Paint backgrounds
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                if (cells[i, j].BackgroundColor != args.BackgroundColor || cells[i, j].Reverse)
                {
                    float x = j * this.textParam.CharWidth;
                    float y = i * this.textParam.LineHeight;
                    int color = cells[i, j].Reverse
                        ? cells[i, j].ForegroundColor
                        : cells[i, j].BackgroundColor;
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
                Cell startCell = cells[i, cellRangeStart];
                while (cellRangeEnd < cols)
                {
                    Cell cell = cells[i, cellRangeEnd];
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

                this.DrawCellRange(canvas, cells, i, cellRangeStart, cellRangeEnd);
            }
        }

        // Draw cursor
        this.DrawCursor(canvas, cells, args, modeInfo);

        // Draw preedit (IME composition) overlay
        this.DrawPreedit(canvas, args);
    }

    private void DrawCellRange(SKCanvas canvas, Cell[,] cells, int row, int colStart, int colEnd)
    {
        bool bold = cells[row, colStart].Bold;
        bool italic = cells[row, colStart].Italic;
        bool underline = cells[row, colStart].Underline;
        bool undercurl = cells[row, colStart].Undercurl;
        int foregroundColor = cells[row, colStart].Reverse
            ? cells[row, colStart].BackgroundColor
            : cells[row, colStart].ForegroundColor;

        var weight = bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        var slant = italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        var styledTypeface = this.fontChain.GetStyledTypeface(weight, slant);
        this.textPaint.Color = Helpers.GetSkColor(foregroundColor);
        this.textPaint.Typeface = styledTypeface;

        float baselineY = (row * this.textParam.LineHeight) + (this.textParam.LineHeight * 0.8f);

        if (this.EnableLigature)
        {
            this.textPaint.FakeBoldText = false;
            this.DrawLigatureTextRange(canvas, cells, row, colStart, colEnd, weight, slant, baselineY, bold);
        }
        else
        {
            this.textPaint.FakeBoldText = bold;
            this.DrawPlainTextRange(canvas, cells, row, colStart, colEnd, styledTypeface, weight, slant, baselineY);
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
            int specialColor = cells[row, colStart].SpecialColor;
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

    private void DrawCursor(SKCanvas canvas, Cell[,] cells, EditorScreen args, ModeInfo? modeInfo)
    {
        if (!this.ShouldDrawCursor(modeInfo))
        {
            return;
        }

        var cursorPercentage = modeInfo is { CursorStyleEnabled: true }
            ? Math.Clamp(modeInfo.CellPercentage, 1, 100)
            : 100;
        var cursorShape = modeInfo is { CursorStyleEnabled: true }
            ? modeInfo.CursorShape
            : CursorShape.Block;
        int cellWidth = this.GetCharWidth(cells, args.CursorPosition.Row, args.CursorPosition.Col);

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

    private void DrawPlainTextRange(
        SKCanvas canvas,
        Cell[,] cells,
        int row,
        int colStart,
        int colEnd,
        SKTypeface styledTypeface,
        SKFontStyleWeight weight,
        SKFontStyleSlant slant,
        float baselineY)
    {
        int cellIndex = colStart;
        while (cellIndex < colEnd)
        {
            var cell = cells[row, cellIndex];
            if (cell.Character is null)
            {
                cellIndex++;
                continue;
            }

            string text = cell.Character;
            int codePoint = char.ConvertToUtf32(text, 0);
            float x = cellIndex * this.textParam.CharWidth;

            this.textPaint.Typeface = this.fontChain.GetTypefaceForGlyph(codePoint, text, weight, slant);
            canvas.DrawText(text, x, baselineY, this.textPaint);

            // Restore the styled typeface for subsequent characters.
            this.textPaint.Typeface = styledTypeface;

            int charWidth = this.GetCharWidth(cells, row, cellIndex);
            cellIndex += charWidth;
        }
    }

    private void DrawLigatureTextRange(
        SKCanvas canvas,
        Cell[,] cells,
        int row,
        int colStart,
        int colEnd,
        SKFontStyleWeight weight,
        SKFontStyleSlant slant,
        float baselineY,
        bool embolden)
    {
        foreach (var run in this.BuildResolvedTypefaceRuns(cells, row, colStart, colEnd, weight, slant))
        {
            var shapedRun = this.ligatureTextShaper.ShapeText(run.Typeface, this.textParam.SkiaFontSize, run.Text);
            if (shapedRun is null || !this.DrawAnchoredShapedRun(canvas, run, shapedRun, baselineY, embolden))
            {
                this.textPaint.Typeface = run.Typeface;
                this.textPaint.FakeBoldText = embolden;
                this.DrawPlainTextRange(canvas, cells, row, run.StartColumn, run.EndColumn, run.Typeface, weight, slant, baselineY);
                this.textPaint.FakeBoldText = false;
            }
        }
    }

    private bool DrawAnchoredShapedRun(
        SKCanvas canvas,
        ResolvedTypefaceRun run,
        LigatureTextShaper.ShapedTextRun shapedRun,
        float baselineY,
        bool embolden)
    {
        int glyphStart = 0;
        while (glyphStart < shapedRun.GlyphCount)
        {
            uint clusterStart = shapedRun.Clusters[glyphStart];
            int glyphEnd = glyphStart + 1;
            while (glyphEnd < shapedRun.GlyphCount && shapedRun.Clusters[glyphEnd] == clusterStart)
            {
                glyphEnd++;
            }

            int clusterEnd = glyphEnd < shapedRun.GlyphCount
                ? checked((int)shapedRun.Clusters[glyphEnd])
                : run.Text.Length;
            if (!this.TryGetClusterStartColumn(run.CellSpans, checked((int)clusterStart), clusterEnd, out int startColumn))
            {
                return false;
            }

            ushort[] glyphIds = new ushort[glyphEnd - glyphStart];
            SKPoint[] points = new SKPoint[glyphIds.Length];
            float clusterOriginX = shapedRun.Points[glyphStart].X;
            for (int i = 0; i < glyphIds.Length; i++)
            {
                glyphIds[i] = shapedRun.GlyphIds[glyphStart + i];
                var point = shapedRun.Points[glyphStart + i];
                points[i] = new SKPoint(point.X - clusterOriginX, point.Y);
            }

            using var blob = this.ligatureTextShaper.CreateTextBlob(run.Typeface, this.textParam.SkiaFontSize, glyphIds, points, embolden);
            if (blob is null)
            {
                return false;
            }

            canvas.DrawText(blob, startColumn * this.textParam.CharWidth, baselineY, this.textPaint);
            glyphStart = glyphEnd;
        }

        return true;
    }

    private bool TryGetClusterStartColumn(TextCellSpan[] cellSpans, int clusterStart, int clusterEnd, out int startColumn)
    {
        foreach (var cellSpan in cellSpans)
        {
            bool overlaps = cellSpan.TextStart < clusterEnd && clusterStart < (cellSpan.TextStart + cellSpan.TextLength);
            if (overlaps)
            {
                startColumn = cellSpan.ColumnStart;
                return true;
            }
        }

        startColumn = 0;
        return false;
    }

    private List<ResolvedTypefaceRun> BuildResolvedTypefaceRuns(
        Cell[,] cells,
        int row,
        int colStart,
        int colEnd,
        SKFontStyleWeight weight,
        SKFontStyleSlant slant)
    {
        List<ResolvedTypefaceRun> runs = new();
        SKTypeface? currentTypeface = null;
        StringBuilder? currentText = null;
        List<TextCellSpan>? currentCellSpans = null;
        int runStart = colStart;
        int runEnd = colStart;

        void FlushCurrentRun()
        {
            if (currentTypeface is null || currentText is null || currentText.Length == 0 || currentCellSpans is null)
            {
                return;
            }

            runs.Add(new ResolvedTypefaceRun(runStart, runEnd, currentText.ToString(), currentTypeface, currentCellSpans.ToArray()));
        }

        int cellIndex = colStart;
        while (cellIndex < colEnd)
        {
            var cell = cells[row, cellIndex];
            if (cell.Character is null)
            {
                cellIndex++;
                continue;
            }

            string text = cell.Character;
            int charWidth = this.GetCharWidth(cells, row, cellIndex);
            int codePoint = char.ConvertToUtf32(text, 0);
            var typeface = this.fontChain.GetTypefaceForGlyph(codePoint, text, weight, slant);

            if (currentTypeface is null || currentTypeface.Handle != typeface.Handle)
            {
                FlushCurrentRun();
                currentTypeface = typeface;
                currentText = new StringBuilder();
                currentCellSpans = new List<TextCellSpan>();
                runStart = cellIndex;
            }

            int textStart = currentText!.Length;
            currentText!.Append(text);
            currentCellSpans!.Add(new TextCellSpan(textStart, text.Length, cellIndex, charWidth));
            runEnd = cellIndex + charWidth;
            cellIndex += charWidth;
        }

        FlushCurrentRun();
        return runs;
    }

    private void DisposeCachedResources()
    {
        if (this.cursorBlinkTimer is not null)
        {
            this.cursorBlinkTimer.Stop();
            this.cursorBlinkTimer.Tick -= this.OnCursorBlinkTick;
            this.cursorBlinkTimer = null;
        }

        this.Cursor = null;
        this.DisposePointerCursor();
        this.ligatureTextShaper.Dispose();
        this.fontChain.Dispose();
        this.backgroundPaint.Dispose();
        this.textPaint.Dispose();
        this.underlinePaint.Dispose();
        this.undercurlPaint.Dispose();
        this.cursorPaint.Dispose();
        this.preeditUnderlinePaint.Dispose();
    }

    private void RebuildFontChain(IReadOnlyList<string>? guiFontNames = null)
    {
        this.ligatureTextShaper.ClearCache();
        this.fontChain.Rebuild(
            guiFontNames ?? this.currentGuiFontNames,
            this.currentUserFallbackFonts,
            Utilities.Helpers.GetDefaultFallbackFontNames());
    }

    private void ApplyEditorUiState(ModeInfo? modeInfo, bool resetCursorBlink)
    {
        if (!this.editorClient.MouseEnabled)
        {
            this.pressedMouseButton = null;
        }

        this.UpdatePointerCursor(modeInfo);
        this.UpdateCursorBlink(modeInfo, resetCursorBlink);
    }

    private void UpdatePointerCursor(ModeInfo? modeInfo)
    {
        var pointerCursorType = this.ResolvePointerCursorType(modeInfo);
        if (this.resolvedPointerCursorType == pointerCursorType)
        {
            return;
        }

        this.resolvedPointerCursorType = pointerCursorType;
        this.Cursor = null;
        this.DisposePointerCursor();
        if (pointerCursorType is null)
        {
            return;
        }

        if (Application.Current is null)
        {
            return;
        }

        this.pointerCursor = new Cursor(pointerCursorType.Value);
        this.Cursor = this.pointerCursor;
    }

    private void UpdateCursorBlink(ModeInfo? modeInfo, bool resetCursorBlink)
    {
        if (!this.ShouldBlinkCursor(modeInfo))
        {
            this.StopCursorBlink();
            return;
        }

        if (!resetCursorBlink && this.cursorBlinkTimer is not null && this.cursorBlinkTimer.IsEnabled)
        {
            return;
        }

        this.cursorBlinkVisible = true;
        this.cursorBlinkStarted = modeInfo!.CursorBlinking != CursorBlinking.BlinkWait;
        this.EnsureCursorBlinkTimer();
        this.cursorBlinkTimer!.Interval = this.cursorBlinkStarted ? DefaultCursorBlinkInterval : DefaultCursorBlinkWait;
        this.cursorBlinkTimer.Start();
    }

    private void EnsureCursorBlinkTimer()
    {
        if (this.cursorBlinkTimer is not null)
        {
            return;
        }

        this.cursorBlinkTimer = new DispatcherTimer();
        this.cursorBlinkTimer.Tick += this.OnCursorBlinkTick;
    }

    private void OnCursorBlinkTick(object? sender, EventArgs e)
    {
        var modeInfo = this.editorClient.ModeInfo;
        if (!this.ShouldBlinkCursor(modeInfo))
        {
            bool wasHidden = !this.cursorBlinkVisible;
            this.StopCursorBlink();
            if (wasHidden)
            {
                this.InvalidateVisual();
            }

            return;
        }

        if (!this.cursorBlinkStarted)
        {
            this.cursorBlinkStarted = true;
            this.cursorBlinkVisible = false;
            this.cursorBlinkTimer!.Interval = DefaultCursorBlinkInterval;
        }
        else
        {
            this.cursorBlinkVisible = !this.cursorBlinkVisible;
        }

        this.InvalidateVisual();
    }

    private void StopCursorBlink()
    {
        if (this.cursorBlinkTimer is not null)
        {
            this.cursorBlinkTimer.Stop();
        }

        this.cursorBlinkStarted = false;
        this.cursorBlinkVisible = true;
    }

    private bool ShouldBlinkCursor(ModeInfo? modeInfo)
    {
        return modeInfo is { CursorVisible: true, CursorStyleEnabled: true }
            && modeInfo.CursorBlinking != CursorBlinking.BlinkOff;
    }

    private bool ShouldDrawCursor(ModeInfo? modeInfo)
    {
        if (modeInfo is { CursorVisible: false })
        {
            return false;
        }

        return !this.ShouldBlinkCursor(modeInfo) || this.cursorBlinkVisible;
    }

    private StandardCursorType? ResolvePointerCursorType(ModeInfo? modeInfo)
    {
        if (modeInfo is null)
        {
            return null;
        }

        if (this.ShouldHidePointer(modeInfo))
        {
            return StandardCursorType.None;
        }

        return this.MapPointerShape(modeInfo.PointerShape);
    }

    private bool ShouldHidePointer(ModeInfo modeInfo)
    {
        return modeInfo.PointerMode switch
        {
            1 => !this.editorClient.MouseEnabled,
            2 => true,

            // Avalonia cannot keep the cursor hidden after it leaves the control,
            // so "always hide even on leave" degrades to "always hide over the editor".
            3 => true,
            _ => false,
        };
    }

    private void DisposePointerCursor()
    {
        if (this.pointerCursor is not null)
        {
            this.pointerCursor.Dispose();
            this.pointerCursor = null;
        }
    }

    private StandardCursorType? MapPointerShape(string? pointerShape)
    {
        if (string.IsNullOrWhiteSpace(pointerShape))
        {
            return null;
        }

        return pointerShape.Trim().ToLowerInvariant() switch
        {
            "arrow" => StandardCursorType.Arrow,
            "beam" or "ibeam" or "text" => StandardCursorType.Ibeam,
            "hand" or "pointer" => StandardCursorType.Hand,
            "cross" or "crosshair" => StandardCursorType.Cross,
            "help" => StandardCursorType.Help,
            "wait" or "busy" => StandardCursorType.Wait,
            "move" or "sizeall" => StandardCursorType.SizeAll,
            "no" or "forbidden" or "not-allowed" => StandardCursorType.No,
            "ew-resize" or "col-resize" or "sizewe" => StandardCursorType.SizeWestEast,
            "ns-resize" or "row-resize" or "sizens" => StandardCursorType.SizeNorthSouth,
            "n-resize" => StandardCursorType.TopSide,
            "s-resize" => StandardCursorType.BottomSide,
            "w-resize" => StandardCursorType.LeftSide,
            "e-resize" => StandardCursorType.RightSide,
            "nw-resize" => StandardCursorType.TopLeftCorner,
            "ne-resize" => StandardCursorType.TopRightCorner,
            "sw-resize" => StandardCursorType.BottomLeftCorner,
            "se-resize" => StandardCursorType.BottomRightCorner,
            "copy" => StandardCursorType.DragCopy,
            "link" => StandardCursorType.DragLink,
            _ => null,
        };
    }

    private readonly struct ResolvedTypefaceRun
    {
        public ResolvedTypefaceRun(int startColumn, int endColumn, string text, SKTypeface typeface, TextCellSpan[] cellSpans)
        {
            this.StartColumn = startColumn;
            this.EndColumn = endColumn;
            this.Text = text;
            this.Typeface = typeface;
            this.CellSpans = cellSpans;
        }

        public int StartColumn { get; }

        public int EndColumn { get; }

        public string Text { get; }

        public SKTypeface Typeface { get; }

        public TextCellSpan[] CellSpans { get; }
    }

    private readonly struct TextCellSpan
    {
        public TextCellSpan(int textStart, int textLength, int columnStart, int columnWidth)
        {
            this.TextStart = textStart;
            this.TextLength = textLength;
            this.ColumnStart = columnStart;
            this.ColumnWidth = columnWidth;
        }

        public int TextStart { get; }

        public int TextLength { get; }

        public int ColumnStart { get; }

        public int ColumnWidth { get; }
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
