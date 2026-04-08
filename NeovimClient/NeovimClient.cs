// <copyright file="NeovimClient.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient;

using AeroVim.Editor;
using AeroVim.Editor.Utilities;
using AeroVim.NeovimClient.Events;

/// <summary>
/// Highlevel neovim client.
/// </summary>
public sealed class NeovimClient : IEditorClient
{
    private const int DefaultForegroundColor = 0x000000;
    private const int DefaultBackgroundColor = 0xFFFFFF;
    private const int DefaultSpecialColor = 0x000000;

    private readonly DefaultNeovimRpcClient? neovim;
    private readonly object screenLock = new();
    private readonly Screen screen = new() { Cells = new Cell[0, 0] };
    private readonly Dictionary<int, HighlightAttributes> highlightTable = new();

    private int foregroundColor = DefaultForegroundColor;
    private int backgroundColor = DefaultBackgroundColor;
    private int specialColor = DefaultSpecialColor;
    private HighlightSetEvent highlightSetEvent = new();

    private (int Left, int Top, int Right, int Bottom) scrollRegion;
    private bool initialized = false;

    private IList<ModeInfo> modeInfo = new List<ModeInfo>();
    private int modeIndex = 0;
    private string title = string.Empty;
    private Cell[,]? cells;
    private bool[]? dirtyRows;
    private bool allDirty;
    private (int Row, int Col) cursorPosition = (0, 0);
    private PopupMenuItem[]? popupItems;
    private int popupSelected = -1;
    private (int Row, int Col)? popupAnchor;
    private CmdlineState? cmdlineState;

    /// <summary>
    /// Initializes a new instance of the <see cref="NeovimClient"/> class.
    /// </summary>
    /// <param name="neovimPath">Neovim.</param>
    /// <param name="logger">Application logger.</param>
    /// <param name="workingDirectory">Optional working directory for Neovim.</param>
    /// <param name="fileArgs">Optional file paths to open on startup.</param>
    public NeovimClient(string neovimPath, AeroVim.Editor.Diagnostics.IAppLogger logger, string? workingDirectory = null, IReadOnlyList<string>? fileArgs = null)
    {
        this.neovim = new DefaultNeovimRpcClient(neovimPath, logger, workingDirectory, fileArgs);
        this.neovim.Redraw += this.OnNeovimRedraw;
        this.neovim.NeovimExited += (int exitCode) =>
        {
            this.EditorExited?.Invoke(exitCode);
        };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NeovimClient"/> class without starting a backend.
    /// This is used by the test suite to drive redraw state deterministically.
    /// </summary>
    internal NeovimClient()
    {
    }

    /// <summary>
    /// Raised when the title changes.
    /// </summary>
    public event TitleChangedHandler? TitleChanged;

    /// <summary>
    /// Raised when the editor should redraw.
    /// </summary>
    public event RedrawHandler? Redraw;

    /// <summary>
    /// Raised when the editor process exits.
    /// </summary>
    public event EditorExitedHandler? EditorExited;

    /// <summary>
    /// Raised when the foreground color changes.
    /// </summary>
    public event ColorChangedHandler? ForegroundColorChanged;

    /// <summary>
    /// Raised when the background color changes.
    /// </summary>
    public event ColorChangedHandler? BackgroundColorChanged;

    /// <summary>
    /// Raised when the font changes.
    /// </summary>
    public event FontChangedHandler? FontChanged;

    /// <summary>
    /// Gets the Font settings.
    /// </summary>
    public FontSettings FontSettings { get; private set; } = new FontSettings();

    /// <summary>
    /// Gets the mode info.
    /// </summary>
    public ModeInfo? ModeInfo => this.modeInfo?.Count > this.modeIndex ? this.modeInfo[this.modeIndex] : null;

    /// <summary>
    /// Gets a value indicating whether mouse input is enabled by the editor.
    /// </summary>
    public bool MouseEnabled { get; private set; } = true;

    /// <summary>
    /// Gets the current mouse tracking mode.
    /// </summary>
    public MouseTrackingMode MouseTrackingMode => this.MouseEnabled ? MouseTrackingMode.ButtonEvent : MouseTrackingMode.None;

    private int Height => this.cells!.GetLength(0);

    private int Width => this.cells!.GetLength(1);

    /// <summary>
    /// Try to resize the screen.
    /// </summary>
    /// <param name="width">Column count.</param>
    /// <param name="height">Row count.</param>
    public void TryResize(uint width, uint height)
    {
        if (this.neovim is null)
        {
            return;
        }

        if (this.initialized)
        {
            this.neovim.UI.TryResize(width, height);
        }
        else
        {
            this.neovim.UI.Attach(width, height);
            this.neovim.Global.Command("set title");
            this.initialized = true;
        }
    }

    /// <summary>
    /// Dispose.
    /// </summary>
    public void Dispose()
    {
        this.neovim?.Dispose();
    }

    /// <summary>
    /// Input.
    /// </summary>
    /// <param name="text">input.</param>
    public void Input(string text)
    {
        this.neovim?.Global.Input(text);
    }

    /// <summary>
    /// Set a global (g:) variable.
    /// </summary>
    /// <param name="name">Variable name.</param>
    /// <param name="value">Variable value.</param>
    public void SetVariable(string name, string value)
    {
        this.neovim?.Global.SetGlobalVariable(name, value);
    }

    /// <summary>
    /// Send a mouse event to Neovim.
    /// </summary>
    /// <param name="button">Mouse button: "left", "right", "middle", "wheel", or "move".</param>
    /// <param name="action">Action: "press", "drag", "release" for buttons; "up", "down", "left", "right" for wheel.</param>
    /// <param name="modifier">Modifier keys string, e.g. "", "S", "C", "A", "C-S".</param>
    /// <param name="grid">Grid id (0 when multigrid is not enabled).</param>
    /// <param name="row">Zero-based grid row.</param>
    /// <param name="col">Zero-based grid column.</param>
    public void InputMouse(string button, string action, string modifier, int grid, int row, int col)
    {
        this.neovim?.Global.InputMouse(button, action, modifier, grid, row, col);
    }

    /// <summary>
    /// Execute a Neovim command.
    /// </summary>
    /// <param name="command">The command string.</param>
    public void Command(string command)
    {
        this.neovim?.Global.Command(command);
    }

    /// <summary>
    /// Write an error message to the vim error buffer.
    /// </summary>
    /// <param name="message">The message.</param>
    public void WriteErrorMessage(string message)
    {
        this.neovim?.Global.WriteErrorMessage(message);
    }

    /// <summary>
    /// Get the screen.
    /// </summary>
    /// <returns>The Screen.</returns>
    public Screen? GetScreen()
    {
        lock (this.screenLock)
        {
            if (this.cells is null)
            {
                return null;
            }

            int rows = this.cells.GetLength(0);
            bool sizeChanged = false;

            if (this.screen.Cells is null
                || this.screen.Cells.GetLength(0) != rows
                || this.screen.Cells.GetLength(1) != this.cells.GetLength(1))
            {
                sizeChanged = true;
                this.screen.Cells = new Cell[rows, this.cells.GetLength(1)];
                this.CopyAllCells(this.screen.Cells, this.cells);
            }
            else if (this.allDirty)
            {
                this.CopyAllCells(this.screen.Cells, this.cells);
            }
            else if (this.dirtyRows is not null)
            {
                int cols = this.cells.GetLength(1);
                for (int i = 0; i < this.dirtyRows.Length; i++)
                {
                    if (this.dirtyRows[i])
                    {
                        for (int j = 0; j < cols; j++)
                        {
                            this.screen.Cells[i, j] = this.cells[i, j];
                        }
                    }
                }
            }

            // Propagate dirty metadata to the screen before clearing.
            this.screen.AllDirty = sizeChanged || this.allDirty;
            if (this.dirtyRows is not null && !this.screen.AllDirty)
            {
                if (this.screen.DirtyRows is null || this.screen.DirtyRows.Length != rows)
                {
                    this.screen.DirtyRows = new bool[rows];
                }

                Array.Copy(this.dirtyRows, this.screen.DirtyRows, rows);
            }
            else
            {
                this.screen.DirtyRows = null;
            }

            this.allDirty = false;
            if (this.dirtyRows is not null)
            {
                Array.Clear(this.dirtyRows, 0, this.dirtyRows.Length);
            }

            this.screen.CursorPosition = this.cursorPosition;
            this.screen.BackgroundColor = this.backgroundColor;
            this.screen.ForegroundColor = this.foregroundColor;
            this.screen.PopupItems = this.popupItems;
            this.screen.PopupSelected = this.popupSelected;
            this.screen.PopupAnchor = this.popupAnchor;
            this.screen.Cmdline = this.cmdlineState;
        }

        return this.screen;
    }

    /// <summary>
    /// Applies redraw events directly without requiring a live Neovim process.
    /// </summary>
    /// <param name="events">The redraw events to apply.</param>
    internal void ProcessRedrawForTesting(IList<IRedrawEvent> events)
    {
        this.OnNeovimRedraw(events);
    }

    private void OnNeovimRedraw(IList<IRedrawEvent> events)
    {
        var actions = new List<Action>();
        bool flush = false;

        lock (this.screenLock)
        {
            foreach (var ev in events)
            {
                switch (ev)
                {
                    // ---- ui-grid-old events ----
                    case ResizeEvent e:
                        this.Resize((int)e.Col, (int)e.Row);
                        break;
                    case ClearEvent:
                        this.Clear();
                        break;
                    case EolClearEvent:
                        this.EolClear();
                        break;
                    case CursorGotoEvent e:
                        this.cursorPosition = ((int)e.Row, (int)e.Col);
                        break;
                    case PutEvent e:
                        this.Put(
                            e.Text,
                            this.highlightSetEvent.Foreground ?? this.foregroundColor,
                            this.highlightSetEvent.Background ?? this.backgroundColor,
                            this.highlightSetEvent.Special ?? this.specialColor,
                            this.highlightSetEvent.Reverse,
                            this.highlightSetEvent.Italic,
                            this.highlightSetEvent.Bold,
                            this.highlightSetEvent.Underline,
                            this.highlightSetEvent.Undercurl);
                        break;
                    case HighlightSetEvent e:
                        this.highlightSetEvent = e;
                        break;
                    case UpdateFgEvent e:
                        this.foregroundColor = e.Color == -1 ? DefaultForegroundColor : e.Color;
                        actions.Add(() => this.ForegroundColorChanged?.Invoke(this.foregroundColor));
                        break;
                    case UpdateBgEvent e:
                        this.backgroundColor = e.Color == -1 ? DefaultBackgroundColor : e.Color;
                        actions.Add(() => this.BackgroundColorChanged?.Invoke(this.backgroundColor));
                        break;
                    case UpdateSpEvent e:
                        this.specialColor = e.Color == -1 ? DefaultSpecialColor : e.Color;
                        break;
                    case SetScrollRegionEvent e:
                        this.scrollRegion = (e.Left, e.Top, e.Right, e.Bottom);
                        break;
                    case ScrollEvent e:
                        this.Scroll(e.Count);
                        break;

                    // ---- ext_linegrid events ----
                    case HlAttrDefineEvent e:
                        this.highlightTable[e.Id] = e.RgbAttrs;
                        break;
                    case DefaultColorsSetEvent e:
                        this.foregroundColor = e.RgbFg;
                        this.backgroundColor = e.RgbBg;
                        this.specialColor = e.RgbSp;
                        actions.Add(() =>
                        {
                            this.ForegroundColorChanged?.Invoke(e.RgbFg);
                            this.BackgroundColorChanged?.Invoke(e.RgbBg);
                        });
                        break;
                    case GridResizeEvent e:
                        this.Resize(e.Width, e.Height);
                        break;
                    case GridLineEvent e:
                        this.GridLine(e);
                        break;
                    case GridClearEvent:
                        this.Clear();
                        break;
                    case GridCursorGotoEvent e:
                        this.cursorPosition = (e.Row, e.Col);
                        break;
                    case GridScrollEvent e:
                        this.GridScroll(e);
                        break;
                    case FlushEvent:
                        flush = true;
                        break;

                    // ---- Global events (shared) ----
                    case SetTitleEvent e:
                        this.title = e.Title;
                        actions.Add(() => this.TitleChanged?.Invoke(e.Title));
                        break;
                    case SetIconTitleEvent:
                        break;
                    case GuiFontEvent e:
                        this.FontSettings = e.FontSettings;
                        actions.Add(() => this.FontChanged?.Invoke(this.FontSettings));
                        break;
                    case ModeInfoSetEvent e:
                        this.modeInfo = e.ModeInfo;
                        break;
                    case ModeChangeEvent e:
                        this.modeIndex = e.Index;
                        break;
                    case MouseOnEvent:
                        this.MouseEnabled = true;
                        break;
                    case MouseOffEvent:
                        this.MouseEnabled = false;
                        break;

                    // ---- ext_popupmenu events ----
                    case PopupmenuShowEvent e:
                        this.popupItems = e.Items;
                        this.popupSelected = e.Selected;
                        this.popupAnchor = (e.Row, e.Col);
                        break;
                    case PopupmenuSelectEvent e:
                        this.popupSelected = e.Selected;
                        break;
                    case PopupmenuHideEvent:
                        this.popupItems = null;
                        this.popupSelected = -1;
                        this.popupAnchor = null;
                        break;

                    // ---- ext_cmdline events ----
                    case CmdlineShowEvent e:
                        this.cmdlineState = new CmdlineState
                        {
                            Content = e.Content,
                            CursorPos = e.Pos,
                            FirstChar = e.Firstc,
                            Prompt = e.Prompt,
                            Indent = e.Indent,
                            Level = e.Level,
                        };
                        break;
                    case CmdlinePosEvent e:
                        if (this.cmdlineState is not null)
                        {
                            this.cmdlineState.CursorPos = e.Pos;
                        }

                        break;
                    case CmdlineHideEvent:
                        this.cmdlineState = null;
                        break;
                }
            }
        }

        foreach (var action in actions)
        {
            action.Invoke();
        }

        // With ext_linegrid, only refresh the UI on flush. Legacy batches
        // that never contain a flush event still trigger an immediate redraw.
        if (flush || !events.Any(e => e is FlushEvent or HlAttrDefineEvent or GridLineEvent or GridResizeEvent or GridClearEvent or GridCursorGotoEvent or GridScrollEvent or DefaultColorsSetEvent or PopupmenuShowEvent or PopupmenuSelectEvent or PopupmenuHideEvent or CmdlineShowEvent or CmdlinePosEvent or CmdlineHideEvent))
        {
            this.Redraw?.Invoke();
        }
    }

    private void Resize(int width, int height)
    {
        var newCells = new Cell[height, width];
        if (this.cells is not null)
        {
            int copyRows = Math.Min(height, this.cells.GetLength(0));
            int copyCols = Math.Min(width, this.cells.GetLength(1));

            for (int row = 0; row < copyRows; row++)
            {
                for (int col = 0; col < copyCols; col++)
                {
                    newCells[row, col] = this.cells[row, col];
                }
            }

            for (int row = 0; row < height; row++)
            {
                int startCol = row < copyRows ? copyCols : 0;
                for (int col = startCol; col < width; col++)
                {
                    this.ClearCell(ref newCells[row, col]);
                }
            }
        }
        else
        {
            this.cells = newCells;
            this.dirtyRows = new bool[height];
            this.Clear();
            this.scrollRegion = (0, 0, width - 1, height - 1);
            return;
        }

        this.cells = newCells;
        this.dirtyRows = new bool[height];
        this.allDirty = true;
        this.cursorPosition = (
            Math.Clamp(this.cursorPosition.Row, 0, height - 1),
            Math.Clamp(this.cursorPosition.Col, 0, width - 1));

        this.scrollRegion = (0, 0, width - 1, height - 1);
    }

    private void Clear()
    {
        for (int i = 0; i < this.Height; i++)
        {
            for (int j = 0; j < this.Width; j++)
            {
                this.ClearCell(ref this.cells![i, j]);
            }
        }

        this.allDirty = true;
        this.cursorPosition = (0, 0);
    }

    private void EolClear()
    {
        int row = this.cursorPosition.Row;
        for (int j = this.cursorPosition.Col; j < this.Width; j++)
        {
            this.ClearCell(ref this.cells![row, j]);
        }

        if (this.dirtyRows is not null)
        {
            this.dirtyRows[row] = true;
        }
    }

    private void Put(IList<string?> text, int foreground, int background, int special, bool reverse, bool italic, bool bold, bool underline, bool undercurl)
    {
        if (this.dirtyRows is not null)
        {
            this.dirtyRows[this.cursorPosition.Row] = true;
        }

        foreach (var ch in text)
        {
            this.cells![this.cursorPosition.Row, this.cursorPosition.Col].Set(ch, foreground, background, special, reverse, italic, bold, underline, undercurl);
            this.cursorPosition.Col++;
        }
    }

    private void Scroll(int count)
    {
        int srcBegin;
        int destBegin;
        int clearBegin;
        if (count > 0)
        {
            // Scroll Down
            srcBegin = this.scrollRegion.Top + count;
            destBegin = this.scrollRegion.Top;
            clearBegin = this.scrollRegion.Bottom;
        }
        else
        {
            // Scroll Up
            srcBegin = this.scrollRegion.Bottom + count;
            destBegin = this.scrollRegion.Bottom;
            clearBegin = this.scrollRegion.Top;
        }

        for (int j = this.scrollRegion.Left; j <= this.scrollRegion.Right; j++)
        {
            for (int i = 0; i < this.scrollRegion.Bottom - this.scrollRegion.Top + 1 - Math.Abs(count); i++)
            {
                int deltaRow = i * Math.Sign(count);
                this.cells![destBegin + deltaRow, j] = this.cells[srcBegin + deltaRow, j];
            }

            for (int i = 0; i < Math.Abs(count); i++)
            {
                int deltaRow = -i * Math.Sign(count);
                this.ClearCell(ref this.cells![clearBegin + deltaRow, j]);
            }
        }

        if (this.dirtyRows is not null)
        {
            for (int row = this.scrollRegion.Top; row <= this.scrollRegion.Bottom; row++)
            {
                this.dirtyRows[row] = true;
            }
        }
    }

    private void GridLine(GridLineEvent e)
    {
        if (this.cells is null)
        {
            return;
        }

        int row = e.Row;
        int col = e.ColStart;
        int lastHlId = 0;

        if (this.dirtyRows is not null && row < this.dirtyRows.Length)
        {
            this.dirtyRows[row] = true;
        }

        foreach (var cell in e.Cells)
        {
            if (cell.HlId.HasValue)
            {
                lastHlId = cell.HlId.Value;
            }

            this.ResolveHighlight(lastHlId, out int fg, out int bg, out int sp, out bool reverse, out bool italic, out bool bold, out bool underline, out bool undercurl);

            for (int r = 0; r < cell.Repeat; r++)
            {
                if (col >= this.Width)
                {
                    break;
                }

                string? text = cell.Text.Length > 0 ? cell.Text : null;
                this.cells[row, col].Set(text, fg, bg, sp, reverse, italic, bold, underline, undercurl);
                col++;
            }
        }
    }

    private void GridScroll(GridScrollEvent e)
    {
        if (this.cells is null)
        {
            return;
        }

        // ext_linegrid uses end-exclusive ranges (unlike legacy end-inclusive)
        int top = e.Top;
        int bot = e.Bot;
        int left = e.Left;
        int right = e.Right;
        int count = e.Rows;

        if (count > 0)
        {
            // Scroll up: move rows [top+count, bot) → [top, bot-count)
            for (int row = top; row < bot - count; row++)
            {
                for (int col = left; col < right; col++)
                {
                    this.cells[row, col] = this.cells[row + count, col];
                }
            }

            // Clear the vacated rows at the bottom
            for (int row = bot - count; row < bot; row++)
            {
                for (int col = left; col < right; col++)
                {
                    this.ClearCell(ref this.cells[row, col]);
                }
            }
        }
        else if (count < 0)
        {
            int absCount = -count;

            // Scroll down: move rows [top, bot+count) → [top-count, bot)
            for (int row = bot - 1; row >= top + absCount; row--)
            {
                for (int col = left; col < right; col++)
                {
                    this.cells[row, col] = this.cells[row - absCount, col];
                }
            }

            // Clear the vacated rows at the top
            for (int row = top; row < top + absCount; row++)
            {
                for (int col = left; col < right; col++)
                {
                    this.ClearCell(ref this.cells[row, col]);
                }
            }
        }

        if (this.dirtyRows is not null)
        {
            for (int row = top; row < bot; row++)
            {
                if (row < this.dirtyRows.Length)
                {
                    this.dirtyRows[row] = true;
                }
            }
        }
    }

    private void ResolveHighlight(int hlId, out int fg, out int bg, out int sp, out bool reverse, out bool italic, out bool bold, out bool underline, out bool undercurl)
    {
        if (hlId != 0 && this.highlightTable.TryGetValue(hlId, out var attrs))
        {
            fg = attrs.Foreground ?? this.foregroundColor;
            bg = attrs.Background ?? this.backgroundColor;
            sp = attrs.Special ?? this.specialColor;
            reverse = attrs.Reverse;
            italic = attrs.Italic;
            bold = attrs.Bold;
            underline = attrs.Underline;
            undercurl = attrs.Undercurl;
        }
        else
        {
            fg = this.foregroundColor;
            bg = this.backgroundColor;
            sp = this.specialColor;
            reverse = false;
            italic = false;
            bold = false;
            underline = false;
            undercurl = false;
        }
    }

    private void ClearCell(ref Cell cell)
    {
        cell.Clear(this.foregroundColor, this.backgroundColor, this.specialColor);
    }

    private void CopyAllCells(Cell[,] destination, Cell[,] source)
    {
        Array.Copy(source, destination, source.Length);
    }
}
