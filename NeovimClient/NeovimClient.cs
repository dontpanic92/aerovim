// <copyright file="NeovimClient.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
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

        private readonly DefaultNeovimRpcClient neovim;
        private readonly object screenLock = new object();
        private readonly Screen screen = new Screen();

        private int foregroundColor = DefaultForegroundColor;
        private int backgroundColor = DefaultBackgroundColor;
        private int specialColor = DefaultSpecialColor;

        private (int Left, int Top, int Right, int Bottom) scrollRegion;
        private bool initialized = false;

        private IList<ModeInfo> modeInfo = new List<ModeInfo>();
        private int modeIndex = 0;
        private string title;
        private string iconTitle;
        private Cell[,] cells;
        private bool[] dirtyRows;
        private bool allDirty;
        private (int Row, int Col) cursorPosition = (0, 0);

        /// <summary>
        /// Initializes a new instance of the <see cref="NeovimClient"/> class.
        /// </summary>
        /// <param name="neovimPath">Neovim.</param>
        public NeovimClient(string neovimPath)
        {
            this.neovim = new DefaultNeovimRpcClient(neovimPath);
            this.neovim.Redraw += this.OnNeovimRedraw;
            this.neovim.NeovimExited += (int exitCode) =>
            {
                this.EditorExited?.Invoke(exitCode);
            };
        }

        /// <summary>
        /// Gets or sets the titleChanged event.
        /// </summary>
        public TitleChangedHandler TitleChanged { get; set; }

        /// <summary>
        /// Gets or sets the Redraw event.
        /// </summary>
        public RedrawHandler Redraw { get; set; }

        /// <summary>
        /// Gets or sets the EditorExited event.
        /// </summary>
        public EditorExitedHandler EditorExited { get; set; }

        /// <summary>
        /// Gets or sets the ForgroundColorChanged event.
        /// </summary>
        public ColorChangedHandler ForegroundColorChanged { get; set; }

        /// <summary>
        /// Gets or sets the BackgroundColorChanged event.
        /// </summary>
        public ColorChangedHandler BackgroundColorChanged { get; set; }

        /// <summary>
        /// Gets or sets the FontChanged event.
        /// </summary>
        public FontChangedHandler FontChanged { get; set; }

        /// <summary>
        /// Gets the Font settings.
        /// </summary>
        public FontSettings FontSettings { get; private set; }

        /// <summary>
        /// Gets the mode info.
        /// </summary>
        public ModeInfo ModeInfo => this.modeInfo?.Count > this.modeIndex ? this.modeInfo[this.modeIndex] : null;

        private int Height => this.cells.GetLength(0);

        private int Width => this.cells.GetLength(1);

        /// <summary>
        /// Try to resize the screen.
        /// </summary>
        /// <param name="width">Column count.</param>
        /// <param name="height">Row count.</param>
        public void TryResize(uint width, uint height)
        {
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
            this.neovim.Global.Input(text);
        }

        /// <summary>
        /// Set a global (g:) variable.
        /// </summary>
        /// <param name="name">Variable name.</param>
        /// <param name="value">Variable value.</param>
        public void SetVariable(string name, string value)
        {
            this.neovim.Global.SetGlobalVariable(name, value);
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
            this.neovim.Global.InputMouse(button, action, modifier, grid, row, col);
        }

        /// <summary>
        /// Execute a Neovim command.
        /// </summary>
        /// <param name="command">The command string.</param>
        public void Command(string command)
        {
            this.neovim.Global.Command(command);
        }

        /// <summary>
        /// Write an error message to the vim error buffer.
        /// </summary>
        /// <param name="message">The message.</param>
        public void WriteErrorMessage(string message)
        {
            this.neovim.Global.WriteErrorMessage(message);
        }

        /// <summary>
        /// Get the screen.
        /// </summary>
        /// <returns>The Screen.</returns>
        public Screen GetScreen()
        {
            lock (this.screenLock)
            {
                if (this.cells == null)
                {
                    return null;
                }

                if (this.screen.Cells == null
                    || this.screen.Cells.GetLength(0) != this.cells.GetLength(0)
                    || this.screen.Cells.GetLength(1) != this.cells.GetLength(1))
                {
                    this.screen.Cells = (Cell[,])this.cells.Clone();
                }
                else if (this.allDirty)
                {
                    for (int i = 0; i < this.cells.GetLength(0); i++)
                    {
                        for (int j = 0; j < this.cells.GetLength(1); j++)
                        {
                            this.screen.Cells[i, j] = this.cells[i, j];
                        }
                    }
                }
                else if (this.dirtyRows != null)
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

                this.allDirty = false;
                if (this.dirtyRows != null)
                {
                    Array.Clear(this.dirtyRows, 0, this.dirtyRows.Length);
                }

                this.screen.CursorPosition = this.cursorPosition;
                this.screen.BackgroundColor = this.backgroundColor;
                this.screen.ForegroundColor = this.foregroundColor;
            }

            return this.screen;
        }

        private void OnNeovimRedraw(IList<IRedrawEvent> events)
        {
            var actions = new List<Action>();

            lock (this.screenLock)
            {
                HighlightSetEvent highlightSetEvent = new HighlightSetEvent();
                foreach (var ev in events)
                {
                    switch (ev)
                    {
                        case ResizeEvent e:
                            this.Resize((int)e.Col, (int)e.Row);
                            break;
                        case ClearEvent e:
                            this.Clear();
                            break;
                        case EolClearEvent e:
                            this.EolClear();
                            break;
                        case CursorGotoEvent e:
                            this.cursorPosition = ((int)e.Row, (int)e.Col);
                            break;
                        case SetTitleEvent e:
                            this.title = e.Title;
                            actions.Add(() => this.TitleChanged?.Invoke(e.Title));
                            break;
                        case SetIconTitleEvent e:
                            this.iconTitle = e.Title;
                            break;
                        case PutEvent e:
                            this.Put(
                                e.Text,
                                highlightSetEvent.Foreground ?? this.foregroundColor,
                                highlightSetEvent.Background ?? this.backgroundColor,
                                highlightSetEvent.Special ?? this.specialColor,
                                highlightSetEvent.Reverse,
                                highlightSetEvent.Italic,
                                highlightSetEvent.Bold,
                                highlightSetEvent.Underline,
                                highlightSetEvent.Undercurl);
                            break;
                        case HighlightSetEvent e:
                            highlightSetEvent = e;
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
                    }
                }
            }

            foreach (var action in actions)
            {
                action.Invoke();
            }

            this.Redraw?.Invoke();
        }

        private void Resize(int width, int height)
        {
            this.cells = new Cell[height, width];
            this.dirtyRows = new bool[height];
            this.Clear();

            this.scrollRegion = (0, 0, width - 1, height - 1);
        }

        private void Clear()
        {
            for (int i = 0; i < this.Height; i++)
            {
                for (int j = 0; j < this.Width; j++)
                {
                    this.ClearCell(ref this.cells[i, j]);
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
                this.ClearCell(ref this.cells[row, j]);
            }

            if (this.dirtyRows != null)
            {
                this.dirtyRows[row] = true;
            }
        }

        private void Put(IList<int?> text, int foreground, int background, int special, bool reverse, bool italic, bool bold, bool underline, bool undercurl)
        {
            if (this.dirtyRows != null)
            {
                this.dirtyRows[this.cursorPosition.Row] = true;
            }

            foreach (var ch in text)
            {
                this.cells[this.cursorPosition.Row, this.cursorPosition.Col].Set(ch, foreground, background, special, reverse, italic, bold, underline, undercurl);
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
                    this.cells[destBegin + deltaRow, j] = this.cells[srcBegin + deltaRow, j];
                }

                for (int i = 0; i < Math.Abs(count); i++)
                {
                    int deltaRow = -i * Math.Sign(count);
                    this.ClearCell(ref this.cells[clearBegin + deltaRow, j]);
                }
            }

            if (this.dirtyRows != null)
            {
                for (int row = this.scrollRegion.Top; row <= this.scrollRegion.Bottom; row++)
                {
                    this.dirtyRows[row] = true;
                }
            }
        }

        private void ClearCell(ref Cell cell)
        {
           cell.Clear(this.foregroundColor, this.backgroundColor, this.specialColor);
        }
    }
}
