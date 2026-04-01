// <copyright file="TerminalBuffer.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.VimClient;

using AeroVim.Editor;

/// <summary>
/// Maintains the terminal cell grid state that the VT parser modifies.
/// </summary>
public class TerminalBuffer
{
    private readonly object screenLock = new();
    private readonly Screen screen = new() { Cells = new Cell[0, 0] };

    private Cell[,] cells;
    private Cell[,]? altCells;
    private bool usingAltBuffer;

    private int cursorRow;
    private int cursorCol;
    private int savedCursorRow;
    private int savedCursorCol;

    private int scrollTop;
    private int scrollBottom;

    private int currentFg = -1;
    private int currentBg = -1;
    private int currentSpecial;
    private bool bold;
    private bool italic;
    private bool underline;
    private bool undercurl;
    private bool reverse;

    private bool[] dirtyRows;
    private bool allDirty;

    private int defaultFg = 0x000000;
    private int defaultBg = 0xFFFFFF;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalBuffer"/> class.
    /// </summary>
    /// <param name="cols">Initial column count.</param>
    /// <param name="rows">Initial row count.</param>
    public TerminalBuffer(int cols, int rows)
    {
        this.Rows = rows;
        this.Cols = cols;
        this.cells = new Cell[rows, cols];
        this.dirtyRows = new bool[rows];
        this.scrollBottom = rows - 1;
        this.ClearRegion(0, 0, rows - 1, cols - 1);
        this.allDirty = true;
    }

    /// <summary>
    /// Gets the row count.
    /// </summary>
    public int Rows { get; private set; }

    /// <summary>
    /// Gets the column count.
    /// </summary>
    public int Cols { get; private set; }

    /// <summary>
    /// Gets the current cursor row.
    /// </summary>
    public int CursorRow => this.cursorRow;

    /// <summary>
    /// Gets the current cursor column.
    /// </summary>
    public int CursorCol => this.cursorCol;

    /// <summary>
    /// Gets or sets a value indicating whether the cursor is visible.
    /// </summary>
    public bool CursorVisible { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether SGR mouse mode is enabled.
    /// </summary>
    public bool SgrMouseEnabled { get; set; }

    /// <summary>
    /// Gets the default foreground color.
    /// </summary>
    public int DefaultForeground => this.defaultFg;

    /// <summary>
    /// Gets the default background color.
    /// </summary>
    public int DefaultBackground => this.defaultBg;

    /// <summary>
    /// Resize the terminal buffer.
    /// </summary>
    /// <param name="cols">New column count.</param>
    /// <param name="rows">New row count.</param>
    public void Resize(int cols, int rows)
    {
        lock (this.screenLock)
        {
            var newCells = new Cell[rows, cols];
            int copyRows = Math.Min(rows, this.Rows);
            int copyCols = Math.Min(cols, this.Cols);

            for (int i = 0; i < copyRows; i++)
            {
                for (int j = 0; j < copyCols; j++)
                {
                    newCells[i, j] = this.cells[i, j];
                }
            }

            // Clear new cells
            for (int i = 0; i < rows; i++)
            {
                int startCol = i < copyRows ? copyCols : 0;
                for (int j = startCol; j < cols; j++)
                {
                    newCells[i, j].Clear(this.ResolveFg(), this.ResolveBg(), this.currentSpecial);
                }
            }

            this.cells = newCells;
            this.Rows = rows;
            this.Cols = cols;
            this.dirtyRows = new bool[rows];
            this.scrollTop = 0;
            this.scrollBottom = rows - 1;
            this.allDirty = true;

            if (this.cursorRow >= rows)
            {
                this.cursorRow = rows - 1;
            }

            if (this.cursorCol >= cols)
            {
                this.cursorCol = cols - 1;
            }
        }
    }

    /// <summary>
    /// Write a character at the cursor position and advance the cursor.
    /// </summary>
    /// <param name="codePoint">The Unicode code point to write.</param>
    public void PutChar(int codePoint)
    {
        lock (this.screenLock)
        {
            if (this.cursorCol >= this.Cols)
            {
                this.cursorCol = 0;
                this.LineFeed();
            }

            this.cells[this.cursorRow, this.cursorCol].Set(
                codePoint,
                this.ResolveFg(),
                this.ResolveBg(),
                this.currentSpecial,
                this.reverse,
                this.italic,
                this.bold,
                this.underline,
                this.undercurl);
            this.MarkDirty(this.cursorRow);
            this.cursorCol++;
        }
    }

    /// <summary>
    /// Set cursor position (0-based).
    /// </summary>
    /// <param name="row">Row index.</param>
    /// <param name="col">Column index.</param>
    public void SetCursorPosition(int row, int col)
    {
        lock (this.screenLock)
        {
            this.cursorRow = Math.Clamp(row, 0, this.Rows - 1);
            this.cursorCol = Math.Clamp(col, 0, this.Cols - 1);
        }
    }

    /// <summary>
    /// Move cursor up by n rows.
    /// </summary>
    /// <param name="n">Number of rows to move.</param>
    public void MoveCursorUp(int n)
    {
        lock (this.screenLock)
        {
            this.cursorRow = Math.Max(this.scrollTop, this.cursorRow - n);
        }
    }

    /// <summary>
    /// Move cursor down by n rows.
    /// </summary>
    /// <param name="n">Number of rows to move.</param>
    public void MoveCursorDown(int n)
    {
        lock (this.screenLock)
        {
            this.cursorRow = Math.Min(this.scrollBottom, this.cursorRow + n);
        }
    }

    /// <summary>
    /// Move cursor forward (right) by n columns.
    /// </summary>
    /// <param name="n">Number of columns to move.</param>
    public void MoveCursorForward(int n)
    {
        lock (this.screenLock)
        {
            this.cursorCol = Math.Min(this.Cols - 1, this.cursorCol + n);
        }
    }

    /// <summary>
    /// Move cursor back (left) by n columns.
    /// </summary>
    /// <param name="n">Number of columns to move.</param>
    public void MoveCursorBack(int n)
    {
        lock (this.screenLock)
        {
            this.cursorCol = Math.Max(0, this.cursorCol - n);
        }
    }

    /// <summary>
    /// Perform a line feed: move cursor down, scrolling if needed.
    /// </summary>
    public void LineFeed()
    {
        lock (this.screenLock)
        {
            if (this.cursorRow >= this.scrollBottom)
            {
                this.ScrollUpInternal(1);
            }
            else
            {
                this.cursorRow++;
            }
        }
    }

    /// <summary>
    /// Perform a reverse index: move cursor up, scrolling down if at top.
    /// </summary>
    public void ReverseIndex()
    {
        lock (this.screenLock)
        {
            if (this.cursorRow <= this.scrollTop)
            {
                this.ScrollDownInternal(1);
            }
            else
            {
                this.cursorRow--;
            }
        }
    }

    /// <summary>
    /// Perform a carriage return: move cursor to column 0.
    /// </summary>
    public void CarriageReturn()
    {
        lock (this.screenLock)
        {
            this.cursorCol = 0;
        }
    }

    /// <summary>
    /// Erase in display.
    /// </summary>
    /// <param name="mode">0=below, 1=above, 2=all, 3=scrollback.</param>
    public void EraseInDisplay(int mode)
    {
        lock (this.screenLock)
        {
            switch (mode)
            {
                case 0:
                    this.ClearRegion(this.cursorRow, this.cursorCol, this.cursorRow, this.Cols - 1);
                    this.ClearRegion(this.cursorRow + 1, 0, this.Rows - 1, this.Cols - 1);
                    break;
                case 1:
                    this.ClearRegion(0, 0, this.cursorRow - 1, this.Cols - 1);
                    this.ClearRegion(this.cursorRow, 0, this.cursorRow, this.cursorCol);
                    break;
                case 2:
                case 3:
                    this.ClearRegion(0, 0, this.Rows - 1, this.Cols - 1);
                    this.allDirty = true;
                    break;
            }
        }
    }

    /// <summary>
    /// Erase in line.
    /// </summary>
    /// <param name="mode">0=to right, 1=to left, 2=entire line.</param>
    public void EraseInLine(int mode)
    {
        lock (this.screenLock)
        {
            switch (mode)
            {
                case 0:
                    this.ClearRegion(this.cursorRow, this.cursorCol, this.cursorRow, this.Cols - 1);
                    break;
                case 1:
                    this.ClearRegion(this.cursorRow, 0, this.cursorRow, this.cursorCol);
                    break;
                case 2:
                    this.ClearRegion(this.cursorRow, 0, this.cursorRow, this.Cols - 1);
                    break;
            }
        }
    }

    /// <summary>
    /// Erase n characters at cursor position.
    /// </summary>
    /// <param name="n">Number of characters to erase.</param>
    public void EraseCharacters(int n)
    {
        lock (this.screenLock)
        {
            int end = Math.Min(this.cursorCol + n - 1, this.Cols - 1);
            this.ClearRegion(this.cursorRow, this.cursorCol, this.cursorRow, end);
        }
    }

    /// <summary>
    /// Insert n blank characters at cursor, shifting existing chars right.
    /// </summary>
    /// <param name="n">Number of characters to insert.</param>
    public void InsertCharacters(int n)
    {
        lock (this.screenLock)
        {
            for (int j = this.Cols - 1; j >= this.cursorCol + n; j--)
            {
                this.cells[this.cursorRow, j] = this.cells[this.cursorRow, j - n];
            }

            this.ClearRegion(this.cursorRow, this.cursorCol, this.cursorRow, Math.Min(this.cursorCol + n - 1, this.Cols - 1));
        }
    }

    /// <summary>
    /// Delete n characters at cursor, shifting remaining chars left.
    /// </summary>
    /// <param name="n">Number of characters to delete.</param>
    public void DeleteCharacters(int n)
    {
        lock (this.screenLock)
        {
            for (int j = this.cursorCol; j < this.Cols - n; j++)
            {
                this.cells[this.cursorRow, j] = this.cells[this.cursorRow, j + n];
            }

            this.ClearRegion(this.cursorRow, this.Cols - n, this.cursorRow, this.Cols - 1);
        }
    }

    /// <summary>
    /// Insert n blank lines at cursor row, shifting existing lines down.
    /// </summary>
    /// <param name="n">Number of lines to insert.</param>
    public void InsertLines(int n)
    {
        lock (this.screenLock)
        {
            for (int i = this.scrollBottom; i >= this.cursorRow + n; i--)
            {
                for (int j = 0; j < this.Cols; j++)
                {
                    this.cells[i, j] = this.cells[i - n, j];
                }

                this.MarkDirty(i);
            }

            for (int i = this.cursorRow; i < Math.Min(this.cursorRow + n, this.scrollBottom + 1); i++)
            {
                this.ClearRow(i);
            }
        }
    }

    /// <summary>
    /// Delete n lines at cursor row, shifting lines below up.
    /// </summary>
    /// <param name="n">Number of lines to delete.</param>
    public void DeleteLines(int n)
    {
        lock (this.screenLock)
        {
            for (int i = this.cursorRow; i <= this.scrollBottom - n; i++)
            {
                for (int j = 0; j < this.Cols; j++)
                {
                    this.cells[i, j] = this.cells[i + n, j];
                }

                this.MarkDirty(i);
            }

            for (int i = Math.Max(this.cursorRow, this.scrollBottom - n + 1); i <= this.scrollBottom; i++)
            {
                this.ClearRow(i);
            }
        }
    }

    /// <summary>
    /// Scroll up n lines within the scroll region.
    /// </summary>
    /// <param name="n">Number of lines to scroll.</param>
    public void ScrollUp(int n)
    {
        lock (this.screenLock)
        {
            this.ScrollUpInternal(n);
        }
    }

    /// <summary>
    /// Scroll down n lines within the scroll region.
    /// </summary>
    /// <param name="n">Number of lines to scroll.</param>
    public void ScrollDown(int n)
    {
        lock (this.screenLock)
        {
            this.ScrollDownInternal(n);
        }
    }

    /// <summary>
    /// Set the scrolling region (0-based, inclusive).
    /// </summary>
    /// <param name="top">Top row of scroll region.</param>
    /// <param name="bottom">Bottom row of scroll region.</param>
    public void SetScrollRegion(int top, int bottom)
    {
        lock (this.screenLock)
        {
            this.scrollTop = Math.Clamp(top, 0, this.Rows - 1);
            this.scrollBottom = Math.Clamp(bottom, 0, this.Rows - 1);
            this.cursorRow = 0;
            this.cursorCol = 0;
        }
    }

    /// <summary>
    /// Save cursor position.
    /// </summary>
    public void SaveCursor()
    {
        lock (this.screenLock)
        {
            this.savedCursorRow = this.cursorRow;
            this.savedCursorCol = this.cursorCol;
        }
    }

    /// <summary>
    /// Restore cursor position.
    /// </summary>
    public void RestoreCursor()
    {
        lock (this.screenLock)
        {
            this.cursorRow = Math.Clamp(this.savedCursorRow, 0, this.Rows - 1);
            this.cursorCol = Math.Clamp(this.savedCursorCol, 0, this.Cols - 1);
        }
    }

    /// <summary>
    /// Switch to alternate screen buffer.
    /// </summary>
    public void SwitchToAlternateBuffer()
    {
        lock (this.screenLock)
        {
            if (!this.usingAltBuffer)
            {
                this.altCells = this.cells;
                this.cells = new Cell[this.Rows, this.Cols];
                this.ClearRegion(0, 0, this.Rows - 1, this.Cols - 1);
                this.usingAltBuffer = true;
                this.allDirty = true;
            }
        }
    }

    /// <summary>
    /// Switch back to main screen buffer.
    /// </summary>
    public void SwitchToMainBuffer()
    {
        lock (this.screenLock)
        {
            if (this.usingAltBuffer && this.altCells is not null)
            {
                this.cells = this.altCells;
                this.altCells = null!;
                this.usingAltBuffer = false;
                this.allDirty = true;
            }
        }
    }

    /// <summary>
    /// Full terminal reset.
    /// </summary>
    public void Reset()
    {
        lock (this.screenLock)
        {
            this.ResetAttributes();
            this.cursorRow = 0;
            this.cursorCol = 0;
            this.scrollTop = 0;
            this.scrollBottom = this.Rows - 1;
            this.usingAltBuffer = false;
            this.altCells = null!;
            this.ClearRegion(0, 0, this.Rows - 1, this.Cols - 1);
            this.allDirty = true;
        }
    }

    /// <summary>
    /// Reset all text attributes to defaults.
    /// </summary>
    public void ResetAttributes()
    {
        this.currentFg = -1;
        this.currentBg = -1;
        this.currentSpecial = 0;
        this.bold = false;
        this.italic = false;
        this.underline = false;
        this.undercurl = false;
        this.reverse = false;
    }

    /// <summary>
    /// Set bold attribute.
    /// </summary>
    /// <param name="on">True to enable bold.</param>
    public void SetBold(bool on) => this.bold = on;

    /// <summary>
    /// Set italic attribute.
    /// </summary>
    /// <param name="on">True to enable italic.</param>
    public void SetItalic(bool on) => this.italic = on;

    /// <summary>
    /// Set underline attribute.
    /// </summary>
    /// <param name="on">True to enable underline.</param>
    public void SetUnderline(bool on) => this.underline = on;

    /// <summary>
    /// Set undercurl attribute.
    /// </summary>
    /// <param name="on">True to enable undercurl.</param>
    public void SetUndercurl(bool on) => this.undercurl = on;

    /// <summary>
    /// Set reverse attribute.
    /// </summary>
    /// <param name="on">True to enable reverse video.</param>
    public void SetReverse(bool on) => this.reverse = on;

    /// <summary>
    /// Set foreground color (RGB format).
    /// </summary>
    /// <param name="color">Color value in RGB format.</param>
    public void SetForegroundColor(int color) => this.currentFg = color;

    /// <summary>
    /// Set background color (RGB format).
    /// </summary>
    /// <param name="color">Color value in RGB format.</param>
    public void SetBackgroundColor(int color) => this.currentBg = color;

    /// <summary>
    /// Reset foreground color to default.
    /// </summary>
    public void SetDefaultForeground() => this.currentFg = -1;

    /// <summary>
    /// Reset background color to default.
    /// </summary>
    public void SetDefaultBackground() => this.currentBg = -1;

    /// <summary>
    /// Set the terminal default foreground color (e.g. from OSC 10).
    /// </summary>
    /// <param name="color">Color value in RGB format.</param>
    public void SetTerminalDefaultForeground(int color)
    {
        this.defaultFg = color;
    }

    /// <summary>
    /// Set the terminal default background color (e.g. from OSC 11 or screen analysis).
    /// </summary>
    /// <param name="color">Color value in RGB format.</param>
    public void SetTerminalDefaultBackground(int color)
    {
        this.defaultBg = color;
    }

    /// <summary>
    /// Get the current screen state for rendering.
    /// </summary>
    /// <returns>A screen snapshot.</returns>
    public Screen? GetScreen()
    {
        lock (this.screenLock)
        {
            if (this.cells is null)
            {
                return null;
            }

            if (this.screen.Cells is null
                || this.screen.Cells.GetLength(0) != this.Rows
                || this.screen.Cells.GetLength(1) != this.Cols)
            {
                this.screen.Cells = (Cell[,])this.cells.Clone();
            }
            else if (this.allDirty)
            {
                for (int i = 0; i < this.Rows; i++)
                {
                    for (int j = 0; j < this.Cols; j++)
                    {
                        this.screen.Cells[i, j] = this.cells[i, j];
                    }
                }
            }
            else if (this.dirtyRows is not null)
            {
                for (int i = 0; i < this.dirtyRows.Length; i++)
                {
                    if (this.dirtyRows[i])
                    {
                        for (int j = 0; j < this.Cols; j++)
                        {
                            this.screen.Cells[i, j] = this.cells[i, j];
                        }
                    }
                }
            }

            this.allDirty = false;
            if (this.dirtyRows is not null)
            {
                Array.Clear(this.dirtyRows, 0, this.dirtyRows.Length);
            }

            this.DetectPredominantBackground();
            this.screen.CursorPosition = (this.cursorRow, this.cursorCol);
            this.screen.ForegroundColor = this.defaultFg;
            this.screen.BackgroundColor = this.defaultBg;
        }

        return this.screen;
    }

    private int ResolveFg() => this.currentFg == -1 ? this.defaultFg : this.currentFg;

    private int ResolveBg() => this.currentBg == -1 ? this.defaultBg : this.currentBg;

    /// <summary>
    /// Detect the most common background color on screen and update defaultBg
    /// if a single color dominates (>50% of cells). Called on full redraws.
    /// </summary>
    private void DetectPredominantBackground()
    {
        int totalCells = this.Rows * this.Cols;
        if (totalCells == 0)
        {
            return;
        }

        // Track the top candidate and a runner-up count to avoid allocating a dictionary
        // in the common case where one color dominates.
        int bestColor = this.cells[0, 0].BackgroundColor;
        int bestCount = 0;

        // Use a small dictionary since terminal screens rarely have many distinct bg colors.
        var counts = new Dictionary<int, int>(16);
        for (int i = 0; i < this.Rows; i++)
        {
            for (int j = 0; j < this.Cols; j++)
            {
                int bg = this.cells[i, j].BackgroundColor;
                int count;
                if (counts.TryGetValue(bg, out count))
                {
                    count++;
                }
                else
                {
                    count = 1;
                }

                counts[bg] = count;
                if (count > bestCount)
                {
                    bestCount = count;
                    bestColor = bg;
                }
            }
        }

        if (bestCount > totalCells / 2 && bestColor != this.defaultBg)
        {
            this.defaultBg = bestColor;
        }
    }

    private void MarkDirty(int row)
    {
        if (this.dirtyRows is not null && row >= 0 && row < this.dirtyRows.Length)
        {
            this.dirtyRows[row] = true;
        }
    }

    private void ClearRow(int row)
    {
        for (int j = 0; j < this.Cols; j++)
        {
            this.cells[row, j].Clear(this.ResolveFg(), this.ResolveBg(), this.currentSpecial);
        }

        this.MarkDirty(row);
    }

    private void ClearRegion(int rowStart, int colStart, int rowEnd, int colEnd)
    {
        rowStart = Math.Max(0, rowStart);
        colStart = Math.Max(0, colStart);
        rowEnd = Math.Min(this.Rows - 1, rowEnd);
        colEnd = Math.Min(this.Cols - 1, colEnd);

        for (int i = rowStart; i <= rowEnd; i++)
        {
            int jStart = i == rowStart ? colStart : 0;
            int jEnd = i == rowEnd ? colEnd : this.Cols - 1;
            for (int j = jStart; j <= jEnd; j++)
            {
                this.cells[i, j].Clear(this.ResolveFg(), this.ResolveBg(), this.currentSpecial);
            }

            this.MarkDirty(i);
        }
    }

    private void ScrollUpInternal(int n)
    {
        for (int i = this.scrollTop; i <= this.scrollBottom - n; i++)
        {
            for (int j = 0; j < this.Cols; j++)
            {
                this.cells[i, j] = this.cells[i + n, j];
            }

            this.MarkDirty(i);
        }

        for (int i = Math.Max(this.scrollTop, this.scrollBottom - n + 1); i <= this.scrollBottom; i++)
        {
            this.ClearRow(i);
        }
    }

    private void ScrollDownInternal(int n)
    {
        for (int i = this.scrollBottom; i >= this.scrollTop + n; i--)
        {
            for (int j = 0; j < this.Cols; j++)
            {
                this.cells[i, j] = this.cells[i - n, j];
            }

            this.MarkDirty(i);
        }

        for (int i = this.scrollTop; i < Math.Min(this.scrollTop + n, this.scrollBottom + 1); i++)
        {
            this.ClearRow(i);
        }
    }
}
