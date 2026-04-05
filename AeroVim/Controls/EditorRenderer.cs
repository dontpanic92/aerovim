// <copyright file="EditorRenderer.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Controls;

using System.Text;
using AeroVim.Editor;
using AeroVim.Editor.Utilities;
using AeroVim.Utilities;
using SkiaSharp;
using EditorScreen = AeroVim.Editor.Screen;

/// <summary>
/// Paints the editor grid, cursor, and IME preedit overlay onto a Skia canvas.
/// </summary>
internal sealed class EditorRenderer : IDisposable
{
    private readonly FontFallbackChain fontChain;
    private readonly LigatureTextShaper ligatureTextShaper;
    private readonly EditorTextInputMethodClient imeClient;
    private readonly SKPaint backgroundPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };
    private readonly SKPaint textPaint = new() { IsAntialias = true, IsLinearText = false, SubpixelText = true };
    private readonly SKPaint underlinePaint = new() { StrokeWidth = 1, IsAntialias = true };
    private readonly SKPaint undercurlPaint = new() { StrokeWidth = 1, IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint cursorPaint = new() { BlendMode = SKBlendMode.Difference, Color = SKColors.White };
    private readonly SKPaint preeditUnderlinePaint = new() { StrokeWidth = 2, IsAntialias = true, Color = SKColors.White };
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="EditorRenderer"/> class.
    /// </summary>
    /// <param name="fontChain">The font fallback chain.</param>
    /// <param name="ligatureTextShaper">The ligature text shaper.</param>
    /// <param name="imeClient">The IME client for preedit state.</param>
    public EditorRenderer(
        FontFallbackChain fontChain,
        LigatureTextShaper ligatureTextShaper,
        EditorTextInputMethodClient imeClient)
    {
        this.fontChain = fontChain;
        this.ligatureTextShaper = ligatureTextShaper;
        this.imeClient = imeClient;
    }

    /// <summary>
    /// Renders the editor grid onto the given canvas.
    /// </summary>
    /// <param name="canvas">The Skia canvas to paint on.</param>
    /// <param name="screen">The current screen snapshot.</param>
    /// <param name="textParam">The current text layout parameters.</param>
    /// <param name="modeInfo">The current mode info for cursor rendering.</param>
    /// <param name="enableLigature">Whether ligature shaping is enabled.</param>
    /// <param name="backgroundAlpha">The alpha channel for the default background.</param>
    /// <param name="shouldDrawCursor">Whether the cursor should be drawn.</param>
    public void Render(
        SKCanvas canvas,
        EditorScreen screen,
        TextLayoutParameters textParam,
        ModeInfo? modeInfo,
        bool enableLigature,
        byte backgroundAlpha,
        bool shouldDrawCursor)
    {
        canvas.Clear(Helpers.GetSkColor(screen.BackgroundColor, backgroundAlpha));

        var cells = screen.Cells;
        int rows = cells.GetLength(0);
        int cols = cells.GetLength(1);

        this.textPaint.TextSize = textParam.SkiaFontSize;

        // Paint backgrounds
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                if (cells[i, j].BackgroundColor != screen.BackgroundColor || cells[i, j].Reverse)
                {
                    float x = j * textParam.CharWidth;
                    float y = i * textParam.LineHeight;
                    int color = cells[i, j].Reverse
                        ? cells[i, j].ForegroundColor
                        : cells[i, j].BackgroundColor;
                    this.backgroundPaint.Color = Helpers.GetSkColor(color);
                    canvas.DrawRect(x, y, textParam.CharWidth, textParam.LineHeight, this.backgroundPaint);
                }
            }
        }

        // Paint foreground text
        for (int i = 0; i < rows; i++)
        {
            int j = 0;
            while (j < cols)
            {
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

                this.DrawCellRange(canvas, cells, i, cellRangeStart, cellRangeEnd, textParam, enableLigature);
            }
        }

        // Draw cursor
        if (shouldDrawCursor)
        {
            this.DrawCursor(canvas, cells, screen, modeInfo, textParam);
        }

        // Draw preedit (IME composition) overlay
        this.DrawPreedit(canvas, screen, textParam);
    }

    /// <summary>
    /// Discards any cached rendering state. Currently a no-op since the
    /// renderer paints directly on the canvas, but retained for API
    /// compatibility with <see cref="EditorControl"/>.
    /// </summary>
    public void DiscardBackbuffer()
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!this.isDisposed)
        {
            this.backgroundPaint.Dispose();
            this.textPaint.Dispose();
            this.underlinePaint.Dispose();
            this.undercurlPaint.Dispose();
            this.cursorPaint.Dispose();
            this.preeditUnderlinePaint.Dispose();
            this.isDisposed = true;
        }
    }

    private static bool TryGetClusterStartColumn(TextCellSpan[] cellSpans, int clusterStart, int clusterEnd, out int startColumn)
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

    private static int GetCharWidth(Cell[,] screen, int row, int col)
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

    private void DrawCellRange(
        SKCanvas canvas,
        Cell[,] cells,
        int row,
        int colStart,
        int colEnd,
        TextLayoutParameters textParam,
        bool enableLigature)
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

        float baselineY = (row * textParam.LineHeight) + (textParam.LineHeight * 0.8f);

        if (enableLigature)
        {
            this.textPaint.FakeBoldText = false;
            this.DrawLigatureTextRange(canvas, cells, row, colStart, colEnd, weight, slant, baselineY, bold, textParam);
        }
        else
        {
            this.textPaint.FakeBoldText = bold;
            this.DrawPlainTextRange(canvas, cells, row, colStart, colEnd, styledTypeface, weight, slant, baselineY, textParam);
        }

        // Draw underline
        if (underline)
        {
            this.underlinePaint.Color = Helpers.GetSkColor(foregroundColor);
            float ulY = ((row + 1) * textParam.LineHeight) - 1;
            canvas.DrawLine(colStart * textParam.CharWidth, ulY, colEnd * textParam.CharWidth, ulY, this.underlinePaint);
        }

        // Draw undercurl
        if (undercurl)
        {
            int specialColor = cells[row, colStart].SpecialColor;
            this.undercurlPaint.Color = Helpers.GetSkColor(specialColor);
            float curlY = ((row + 1) * textParam.LineHeight) - 2;
            using var path = new SKPath();
            float startX = colStart * textParam.CharWidth;
            float endX = colEnd * textParam.CharWidth;
            path.MoveTo(startX, curlY);
            for (float cx = startX; cx < endX; cx += 4)
            {
                path.QuadTo(cx + 2, curlY - 2, cx + 4, curlY);
            }

            canvas.DrawPath(path, this.undercurlPaint);
        }
    }

    private void DrawCursor(SKCanvas canvas, Cell[,] cells, EditorScreen screen, ModeInfo? modeInfo, TextLayoutParameters textParam)
    {
        var cursorPercentage = modeInfo is { CursorStyleEnabled: true }
            ? Math.Clamp(modeInfo.CellPercentage, 1, 100)
            : 100;
        var cursorShape = modeInfo is { CursorStyleEnabled: true }
            ? modeInfo.CursorShape
            : CursorShape.Block;
        int cellWidth = GetCharWidth(cells, screen.CursorPosition.Row, screen.CursorPosition.Col);

        float left, top, right, bottom;
        switch (cursorShape)
        {
            case CursorShape.Vertical:
                left = screen.CursorPosition.Col * textParam.CharWidth;
                top = screen.CursorPosition.Row * textParam.LineHeight;
                right = (screen.CursorPosition.Col + (cursorPercentage / 100f)) * textParam.CharWidth;
                bottom = (screen.CursorPosition.Row + 1) * textParam.LineHeight;
                break;
            case CursorShape.Horizontal:
                float topMargin = textParam.LineHeight * (100 - cursorPercentage) / 100f;
                left = screen.CursorPosition.Col * textParam.CharWidth;
                top = (screen.CursorPosition.Row * textParam.LineHeight) + topMargin;
                right = (screen.CursorPosition.Col + cellWidth) * textParam.CharWidth;
                bottom = (screen.CursorPosition.Row + 1) * textParam.LineHeight;
                break;
            default: // Block
                left = screen.CursorPosition.Col * textParam.CharWidth;
                top = screen.CursorPosition.Row * textParam.LineHeight;
                right = (screen.CursorPosition.Col + cellWidth) * textParam.CharWidth;
                bottom = (screen.CursorPosition.Row + 1) * textParam.LineHeight;
                break;
        }

        var cursorRect = new SKRect(left, top, right, bottom);
        canvas.DrawRect(cursorRect, this.cursorPaint);
    }

    private void DrawPreedit(SKCanvas canvas, EditorScreen screen, TextLayoutParameters textParam)
    {
        string? preedit = this.imeClient.PreeditText;
        if (preedit is null)
        {
            return;
        }

        float x = screen.CursorPosition.Col * textParam.CharWidth;
        float y = screen.CursorPosition.Row * textParam.LineHeight;
        float baselineY = y + (textParam.LineHeight * 0.8f);

        float textWidth = this.textPaint.MeasureText(preedit);
        this.backgroundPaint.Color = Helpers.GetSkColor(screen.BackgroundColor);
        canvas.DrawRect(x, y, textWidth, textParam.LineHeight, this.backgroundPaint);

        this.textPaint.Color = Helpers.GetSkColor(screen.ForegroundColor);
        canvas.DrawText(preedit, x, baselineY, this.textPaint);

        float underlineY = y + textParam.LineHeight - 1;
        this.preeditUnderlinePaint.Color = Helpers.GetSkColor(screen.ForegroundColor);
        canvas.DrawLine(x, underlineY, x + textWidth, underlineY, this.preeditUnderlinePaint);
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
        float baselineY,
        TextLayoutParameters textParam)
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
            float x = cellIndex * textParam.CharWidth;

            this.textPaint.Typeface = this.fontChain.GetTypefaceForGlyph(codePoint, text, weight, slant);
            canvas.DrawText(text, x, baselineY, this.textPaint);

            this.textPaint.Typeface = styledTypeface;

            int charWidth = GetCharWidth(cells, row, cellIndex);
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
        bool embolden,
        TextLayoutParameters textParam)
    {
        foreach (var run in this.BuildResolvedTypefaceRuns(cells, row, colStart, colEnd, weight, slant))
        {
            var shapedRun = this.ligatureTextShaper.ShapeText(run.Typeface, textParam.SkiaFontSize, run.Text);
            if (shapedRun is null || !this.DrawAnchoredShapedRun(canvas, run, shapedRun, baselineY, embolden, textParam))
            {
                this.textPaint.Typeface = run.Typeface;
                this.textPaint.FakeBoldText = embolden;
                this.DrawPlainTextRange(canvas, cells, row, run.StartColumn, run.EndColumn, run.Typeface, weight, slant, baselineY, textParam);
                this.textPaint.FakeBoldText = false;
            }
        }
    }

    private bool DrawAnchoredShapedRun(
        SKCanvas canvas,
        ResolvedTypefaceRun run,
        LigatureTextShaper.ShapedTextRun shapedRun,
        float baselineY,
        bool embolden,
        TextLayoutParameters textParam)
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
            if (!TryGetClusterStartColumn(run.CellSpans, checked((int)clusterStart), clusterEnd, out int startColumn))
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

            using var blob = this.ligatureTextShaper.CreateTextBlob(run.Typeface, textParam.SkiaFontSize, glyphIds, points, embolden);
            if (blob is null)
            {
                return false;
            }

            canvas.DrawText(blob, startColumn * textParam.CharWidth, baselineY, this.textPaint);
            glyphStart = glyphEnd;
        }

        return true;
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
            int charWidth = GetCharWidth(cells, row, cellIndex);
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
}
