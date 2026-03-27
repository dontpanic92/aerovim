// <copyright file="VimClient.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.VimClient
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using AeroVim.Editor;
    using AeroVim.Editor.Utilities;
    using Pty.Net;

    /// <summary>
    /// A Vim editor client that communicates with Vim through a PTY using
    /// VT escape sequences. Implements <see cref="IEditorClient"/>.
    /// </summary>
    public sealed class VimClient : IEditorClient
    {
        private readonly string vimPath;
        private readonly string workingDirectory;
        private readonly TerminalBuffer buffer;
        private readonly VtParser parser;
        private readonly object screenLock = new object();

        private IPtyConnection ptyConnection;
        private bool disposed;
        private ModeInfo currentModeInfo;

        /// <summary>
        /// Initializes a new instance of the <see cref="VimClient"/> class.
        /// </summary>
        /// <param name="vimPath">Path to the Vim executable.</param>
        /// <param name="workingDirectory">Optional working directory for Vim.</param>
        public VimClient(string vimPath, string workingDirectory = null)
        {
            this.vimPath = vimPath ?? throw new ArgumentNullException(nameof(vimPath));
            this.workingDirectory = workingDirectory;
            this.buffer = new TerminalBuffer(80, 24);
            this.parser = new VtParser(this.buffer, this.OnTitleChanged);
            this.currentModeInfo = new ModeInfo(CursorShape.Block, 100, CursorBlinking.BlinkOff);
        }

        /// <summary>
        /// Gets or sets the title changed event handler.
        /// </summary>
        public TitleChangedHandler TitleChanged { get; set; }

        /// <summary>
        /// Gets or sets the redraw event handler.
        /// </summary>
        public RedrawHandler Redraw { get; set; }

        /// <summary>
        /// Gets or sets the editor exited event handler.
        /// </summary>
        public EditorExitedHandler EditorExited { get; set; }

        /// <summary>
        /// Gets or sets the foreground color changed event handler.
        /// </summary>
        public ColorChangedHandler ForegroundColorChanged { get; set; }

        /// <summary>
        /// Gets or sets the background color changed event handler.
        /// </summary>
        public ColorChangedHandler BackgroundColorChanged { get; set; }

        /// <summary>
        /// Gets or sets the font changed event handler.
        /// </summary>
        public FontChangedHandler FontChanged { get; set; }

        /// <summary>
        /// Gets the current mode info (cursor shape, size, blink state).
        /// </summary>
        public ModeInfo ModeInfo => this.currentModeInfo;

        /// <summary>
        /// Gets the current font settings.
        /// </summary>
        public FontSettings FontSettings { get; private set; }

        /// <summary>
        /// Try to resize the editor screen. The first call spawns the Vim process.
        /// </summary>
        /// <param name="width">Column count.</param>
        /// <param name="height">Row count.</param>
        public void TryResize(uint width, uint height)
        {
            if (this.ptyConnection == null)
            {
                this.SpawnVim(width, height);
            }
            else
            {
                lock (this.screenLock)
                {
                    this.buffer.Resize((int)width, (int)height);
                }

                this.ptyConnection.Resize((int)width, (int)height);
            }
        }

        /// <summary>
        /// Send keyboard input to Vim using Vim notation.
        /// </summary>
        /// <param name="text">The input key sequence in Vim notation.</param>
        public void Input(string text)
        {
            if (this.ptyConnection == null || this.disposed)
            {
                return;
            }

            string encoded = TerminalInputEncoder.Encode(text);
            this.ptyConnection.Write(encoded);
        }

        /// <summary>
        /// Send a mouse event to Vim using SGR mouse encoding.
        /// </summary>
        /// <param name="button">Mouse button: "left", "right", "middle", "wheel", or "move".</param>
        /// <param name="action">Action: "press", "drag", "release" for buttons; "up", "down", "left", "right" for wheel.</param>
        /// <param name="modifier">Modifier keys string, e.g. "", "S", "C", "A", "C-S".</param>
        /// <param name="grid">Grid id (unused for Vim, kept for interface compatibility).</param>
        /// <param name="row">Zero-based grid row.</param>
        /// <param name="col">Zero-based grid column.</param>
        public void InputMouse(string button, string action, string modifier, int grid, int row, int col)
        {
            if (this.ptyConnection == null || this.disposed)
            {
                return;
            }

            string encoded = EncodeSgrMouse(button, action, modifier, row, col);
            if (encoded != null)
            {
                this.ptyConnection.Write(encoded);
            }
        }

        /// <summary>
        /// Execute a Vim command by entering command-line mode.
        /// </summary>
        /// <param name="command">The command string (without leading colon).</param>
        public void Command(string command)
        {
            this.Input("\x1B");
            this.Input(":" + command + "\r");
        }

        /// <summary>
        /// Get the current screen state for rendering.
        /// </summary>
        /// <returns>The current screen state, or null if not yet initialized.</returns>
        public Screen GetScreen()
        {
            lock (this.screenLock)
            {
                return this.buffer.GetScreen();
            }
        }

        /// <summary>
        /// Dispose of resources and shut down the PTY connection.
        /// </summary>
        public void Dispose()
        {
            if (!this.disposed)
            {
                this.disposed = true;
                if (this.ptyConnection is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        private static string EncodeSgrMouse(string button, string action, string modifier, int row, int col)
        {
            int cb;

            switch (button)
            {
                case "left":
                    cb = 0;
                    break;
                case "middle":
                    cb = 1;
                    break;
                case "right":
                    cb = 2;
                    break;
                case "wheel":
                    switch (action)
                    {
                        case "up":
                            cb = 64;
                            break;
                        case "down":
                            cb = 65;
                            break;
                        case "left":
                            cb = 66;
                            break;
                        case "right":
                            cb = 67;
                            break;
                        default:
                            return null;
                    }

                    AddModifierBits(ref cb, modifier);
                    return $"\x1B[<{cb};{col + 1};{row + 1}M";
                case "move":
                    cb = 35;
                    AddModifierBits(ref cb, modifier);
                    return $"\x1B[<{cb};{col + 1};{row + 1}M";
                default:
                    return null;
            }

            if (action == "drag")
            {
                cb += 32;
            }

            AddModifierBits(ref cb, modifier);
            char finalChar = action == "release" ? 'm' : 'M';
            return $"\x1B[<{cb};{col + 1};{row + 1}{finalChar}";
        }

        private static void AddModifierBits(ref int cb, string modifier)
        {
            if (modifier == null)
            {
                return;
            }

            if (modifier.Contains('S'))
            {
                cb += 4;
            }

            if (modifier.Contains('A'))
            {
                cb += 8;
            }

            if (modifier.Contains('C'))
            {
                cb += 16;
            }
        }

        private void SpawnVim(uint cols, uint rows)
        {
            Environment.SetEnvironmentVariable("TERM", "xterm-256color");
            Environment.SetEnvironmentVariable("COLORTERM", "truecolor");

            string cwd = this.workingDirectory
                ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            lock (this.screenLock)
            {
                this.buffer.Resize((int)cols, (int)rows);
            }

            this.ptyConnection = PtyProvider.Spawn(
                this.vimPath,
                (int)cols,
                (int)rows,
                cwd,
                BackendOptions.Default);

            this.ptyConnection.PtyData += this.OnPtyData;
            this.ptyConnection.PtyDisconnected += this.OnPtyDisconnected;
        }

        private void OnPtyData(object sender, string data)
        {
            if (this.disposed)
            {
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(data);

            lock (this.screenLock)
            {
                this.parser.Process(bytes.AsSpan());
            }

            this.Redraw?.Invoke();
        }

        private void OnPtyDisconnected(object sender)
        {
            if (!this.disposed)
            {
                this.EditorExited?.Invoke(0);
            }
        }

        private void OnTitleChanged(string title)
        {
            this.TitleChanged?.Invoke(title);
        }
    }
}
