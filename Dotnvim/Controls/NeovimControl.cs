// <copyright file="NeovimControl.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls
{
    using System;
    using System.Collections.Concurrent;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Media;
    using Avalonia.Platform;
    using Avalonia.Rendering.SceneGraph;
    using Avalonia.Skia;
    using Avalonia.Threading;
    using Dotnvim.Utilities;
    using SkiaSharp;
    using Cell = Dotnvim.NeovimClient.NeovimClient.Cell;
    using CursorShape = Dotnvim.NeovimClient.Utilities.CursorShape;
    using FontSettings = Dotnvim.NeovimClient.Utilities.FontSettings;
    using NeovimScreen = Dotnvim.NeovimClient.NeovimClient.Screen;

    /// <summary>
    /// The Neovim control using Avalonia custom rendering with SkiaSharp.
    /// </summary>
    public class NeovimControl : Control, IDisposable
    {
        private readonly NeovimClient.NeovimClient neovimClient;
        private readonly ConcurrentQueue<Action> pendingActions = new ConcurrentQueue<Action>();

        private TextLayoutParameters textParam;
        private SKTypeface primaryTypeface;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="NeovimControl"/> class.
        /// </summary>
        /// <param name="neovimClient">The neovim client.</param>
        public NeovimControl(NeovimClient.NeovimClient neovimClient)
        {
            this.neovimClient = neovimClient;
            this.neovimClient.Redraw += this.OnRedraw;
            this.neovimClient.FontChanged += this.OnFontChanged;

            this.ClipToBounds = true;
            this.Focusable = true;

            var defaultFont = Utilities.Helpers.GetDefaultMonospaceFontName();
            this.primaryTypeface = this.CreateValidatedTypeface(defaultFont);
            this.textParam = new TextLayoutParameters(this.primaryTypeface.FamilyName, 11);
        }

        /// <summary>
        /// Gets or sets a value indicating whether font ligature is enabled.
        /// </summary>
        public bool EnableLigature { get; set; }

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
        /// Input text to neovim.
        /// </summary>
        /// <param name="text">The input text sequence.</param>
        public void Input(string text)
        {
            this.neovimClient.Input(text);
        }

        /// <inheritdoc />
        public override void Render(DrawingContext context)
        {
            var customOp = new NeovimDrawOperation(this, new Rect(0, 0, this.Bounds.Width, this.Bounds.Height));
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
                this.neovimClient.TryResize(this.DesiredColCount, this.DesiredRowCount);
            }
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
                    this.neovimClient.Redraw -= this.OnRedraw;
                    this.neovimClient.FontChanged -= this.OnFontChanged;
                    this.primaryTypeface?.Dispose();
                }

                this.isDisposed = true;
            }
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
                $"Dotnvim: Font \"{fontName}\" not found (resolved to \"{typeface.FamilyName}\"). Using SkiaSharp default.");
            typeface.Dispose();
            return SKTypeface.Default;
        }

        private void OnRedraw()
        {
            Dispatcher.UIThread.Post(() => this.InvalidateVisual());
        }

        private void OnFontChanged(FontSettings font)
        {
            this.pendingActions.Enqueue(() =>
            {
                var weight = font.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
                var slant = font.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
                var newTypeface = SKTypeface.FromFamilyName(
                    font.FontName, weight, SKFontStyleWidth.Normal, slant);

                if (!string.Equals(newTypeface?.FamilyName, font.FontName, StringComparison.OrdinalIgnoreCase))
                {
                    newTypeface?.Dispose();
                    this.neovimClient.WriteErrorMessage(
                        $"Dotnvim: Font \"{font.FontName}\" not found (resolved to \"{newTypeface?.FamilyName}\"). Keeping current font.");
                    return;
                }

                this.primaryTypeface?.Dispose();
                this.primaryTypeface = newTypeface;
                this.textParam = new TextLayoutParameters(font.FontName, font.FontPointSize);
                this.neovimClient.TryResize(this.DesiredColCount, this.DesiredRowCount);
            });

            Dispatcher.UIThread.Post(() => this.InvalidateVisual());
        }

        private void RenderWithSkia(SKCanvas canvas)
        {
            while (this.pendingActions.TryDequeue(out var action))
            {
                action();
            }

            var args = this.neovimClient.GetScreen();
            if (args == null)
            {
                return;
            }

            canvas.Clear(Helpers.GetSkColor(args.BackgroundColor, this.BackgroundAlpha));

            int rows = args.Cells.GetLength(0);
            int cols = args.Cells.GetLength(1);

            using var bgPaint = new SKPaint();
            bgPaint.IsAntialias = false;
            bgPaint.Style = SKPaintStyle.Fill;

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
                        bgPaint.Color = Helpers.GetSkColor(color);
                        canvas.DrawRect(x, y, this.textParam.CharWidth, this.textParam.LineHeight, bgPaint);
                    }
                }
            }

            // Paint foreground text
            using var textPaint = new SKPaint();
            textPaint.IsAntialias = true;
            textPaint.TextSize = this.textParam.SkiaFontSize;
            textPaint.IsLinearText = false;
            textPaint.SubpixelText = true;

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
                        if (cell.Character != null
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

                    this.DrawCellRange(canvas, textPaint, args, i, cellRangeStart, cellRangeEnd);
                }
            }

            // Draw cursor
            this.DrawCursor(canvas, args);
        }

        private void DrawCellRange(SKCanvas canvas, SKPaint textPaint, NeovimScreen args, int row, int colStart, int colEnd)
        {
            bool bold = args.Cells[row, colStart].Bold;
            bool italic = args.Cells[row, colStart].Italic;
            bool underline = args.Cells[row, colStart].Underline;
            bool undercurl = args.Cells[row, colStart].Undercurl;
            int foregroundColor = args.Cells[row, colStart].Reverse
                ? args.Cells[row, colStart].BackgroundColor
                : args.Cells[row, colStart].ForegroundColor;

            textPaint.Color = Helpers.GetSkColor(foregroundColor);
            textPaint.FakeBoldText = bold;

            var weight = bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
            var slant = italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
            using var styledTypeface = SKTypeface.FromFamilyName(
                this.textParam.FontName, weight, SKFontStyleWidth.Normal, slant);
            textPaint.Typeface = styledTypeface ?? this.primaryTypeface;

            float baselineY = (row * this.textParam.LineHeight) + (this.textParam.LineHeight * 0.8f);

            // Draw each cell's character individually at its fixed position
            // This preserves the terminal grid alignment while still supporting proper glyph rendering
            int cellIndex = colStart;
            while (cellIndex < colEnd)
            {
                var cell = args.Cells[row, cellIndex];
                if (cell.Character == null)
                {
                    cellIndex++;
                    continue;
                }

                int codePoint = cell.Character.Value;
                string text = char.ConvertFromUtf32(codePoint);
                float x = cellIndex * this.textParam.CharWidth;

                // Check if the primary font has the glyph, otherwise use fallback
                if (textPaint.Typeface != null && !textPaint.ContainsGlyphs(text))
                {
                    using var fallback = SKFontManager.Default.MatchCharacter(codePoint);
                    if (fallback != null)
                    {
                        textPaint.Typeface = fallback;
                    }
                }

                canvas.DrawText(text, x, baselineY, textPaint);

                // Restore the styled typeface for subsequent characters
                textPaint.Typeface = styledTypeface ?? this.primaryTypeface;

                int charWidth = this.GetCharWidth(args.Cells, row, cellIndex);
                cellIndex += charWidth;
            }

            // Draw underline
            if (underline)
            {
                using var ulPaint = new SKPaint();
                ulPaint.Color = Helpers.GetSkColor(foregroundColor);
                ulPaint.StrokeWidth = 1;
                ulPaint.IsAntialias = true;
                float ulY = ((row + 1) * this.textParam.LineHeight) - 1;
                canvas.DrawLine(colStart * this.textParam.CharWidth, ulY, colEnd * this.textParam.CharWidth, ulY, ulPaint);
            }

            // Draw undercurl
            if (undercurl)
            {
                int specialColor = args.Cells[row, colStart].SpecialColor;
                using var curlPaint = new SKPaint();
                curlPaint.Color = Helpers.GetSkColor(specialColor);
                curlPaint.StrokeWidth = 1;
                curlPaint.IsAntialias = true;
                curlPaint.Style = SKPaintStyle.Stroke;
                float curlY = ((row + 1) * this.textParam.LineHeight) - 2;
                using var path = new SKPath();
                float startX = colStart * this.textParam.CharWidth;
                float endX = colEnd * this.textParam.CharWidth;
                path.MoveTo(startX, curlY);
                for (float cx = startX; cx < endX; cx += 4)
                {
                    path.QuadTo(cx + 2, curlY - 2, cx + 4, curlY);
                }

                canvas.DrawPath(path, curlPaint);
            }
        }

        private void DrawCursor(SKCanvas canvas, NeovimScreen args)
        {
            var cursorPercentage = this.neovimClient.ModeInfo?.CellPercentage ?? 100;
            var cursorShape = this.neovimClient.ModeInfo?.CursorShape ?? CursorShape.Block;
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
            using var cursorPaint = new SKPaint();
            cursorPaint.BlendMode = SKBlendMode.Difference;
            cursorPaint.Color = SKColors.White;
            canvas.DrawRect(cursorRect, cursorPaint);
        }

        private int GetCharWidth(Cell[,] screen, int row, int col)
        {
            if (col >= screen.GetLength(1) - 1)
            {
                return 1;
            }

            if (screen[row, col + 1].Character == null)
            {
                return 2;
            }

            return 1;
        }

        /// <summary>
        /// Custom draw operation for rendering the Neovim grid with SkiaSharp.
        /// </summary>
        private sealed class NeovimDrawOperation : ICustomDrawOperation
        {
            private readonly NeovimControl control;

            public NeovimDrawOperation(NeovimControl control, Rect bounds)
            {
                this.control = control;
                this.Bounds = bounds;
            }

            public Rect Bounds { get; }

            public void Dispose()
            {
            }

            public bool Equals(ICustomDrawOperation other) => false;

            public bool HitTest(Point p) => this.Bounds.Contains(p);

            public void Render(ImmediateDrawingContext context)
            {
                var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
                if (leaseFeature == null)
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
}
