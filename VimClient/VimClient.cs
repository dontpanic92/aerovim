// <copyright file="VimClient.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.VimClient;

using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using AeroVim.Editor;
using AeroVim.Editor.Utilities;

/// <summary>
/// A Vim editor client that communicates with Vim through a PTY using
/// VT escape sequences. Implements <see cref="IEditorClient"/>.
/// </summary>
public sealed class VimClient : IEditorClient
{
    private readonly string vimPath;
    private readonly string? workingDirectory;
    private readonly TerminalBuffer buffer;
    private readonly VtParser parser;
    private readonly object screenLock = new();
    private readonly Queue<string> pendingCommands = new();

    private NativePtyConnection? ptyConnection;
    private Task? spawnTask;
    private bool disposed;
    private int processExitHandled;
    private ModeInfo currentModeInfo;
    private ColorChangedHandler? foregroundColorChanged;
    private ColorChangedHandler? backgroundColorChanged;
    private FontChangedHandler? fontChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="VimClient"/> class.
    /// </summary>
    /// <param name="vimPath">Path to the Vim executable.</param>
    /// <param name="workingDirectory">Optional working directory for Vim.</param>
    public VimClient(string vimPath, string? workingDirectory = null)
    {
        this.vimPath = vimPath ?? throw new ArgumentNullException(nameof(vimPath));
        this.workingDirectory = workingDirectory;
        this.buffer = new TerminalBuffer(80, 24);
        this.parser = new VtParser(this.buffer, this.OnTitleChanged);
        this.currentModeInfo = new ModeInfo(CursorShape.Block, 100, CursorBlinking.BlinkOff);
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
    public event ColorChangedHandler ForegroundColorChanged
    {
        add => this.foregroundColorChanged += value;
        remove => this.foregroundColorChanged -= value;
    }

    /// <summary>
    /// Raised when the background color changes.
    /// </summary>
    public event ColorChangedHandler BackgroundColorChanged
    {
        add => this.backgroundColorChanged += value;
        remove => this.backgroundColorChanged -= value;
    }

    /// <summary>
    /// Raised when the font changes.
    /// </summary>
    public event FontChangedHandler FontChanged
    {
        add => this.fontChanged += value;
        remove => this.fontChanged -= value;
    }

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
        if (this.ptyConnection is null)
        {
            if (this.spawnTask is null)
            {
                this.spawnTask = this.SpawnVimAsync(width, height);
            }

            return;
        }

        lock (this.screenLock)
        {
            this.buffer.Resize((int)width, (int)height);
        }

        this.ptyConnection.Resize((int)width, (int)height);
    }

    /// <summary>
    /// Send keyboard input to Vim using Vim notation.
    /// </summary>
    /// <param name="text">The input key sequence in Vim notation.</param>
    public void Input(string text)
    {
        if (this.ptyConnection is null || this.disposed)
        {
            return;
        }

        string encoded = TerminalInputEncoder.Encode(text);
        byte[] bytes = Encoding.UTF8.GetBytes(encoded);
        this.ptyConnection.WriterStream.Write(bytes, 0, bytes.Length);
        this.ptyConnection.WriterStream.Flush();
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
        if (this.ptyConnection is null || this.disposed)
        {
            return;
        }

        string? encoded = EncodeSgrMouse(button, action, modifier, row, col);
        if (encoded is not null)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(encoded);
            this.ptyConnection.WriterStream.Write(bytes, 0, bytes.Length);
            this.ptyConnection.WriterStream.Flush();
        }
    }

    /// <summary>
    /// Execute a Vim command by entering command-line mode.
    /// If the PTY is not yet connected, the command is queued for replay.
    /// </summary>
    /// <param name="command">The command string (without leading colon).</param>
    public void Command(string command)
    {
        if (this.ptyConnection is null && !this.disposed)
        {
            this.pendingCommands.Enqueue(command);
            return;
        }

        this.Input("\x1B");
        this.Input($":{command}\r");
    }

    /// <summary>
    /// Get the current screen state for rendering.
    /// </summary>
    /// <returns>The current screen state, or null if not yet initialized.</returns>
    public Screen? GetScreen()
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
            this.ptyConnection?.Dispose();
        }
    }

    private static string? EncodeSgrMouse(string button, string action, string modifier, int row, int col)
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
        if (modifier is null)
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

    private async Task SpawnVimAsync(uint cols, uint rows)
    {
        try
        {
            if (string.IsNullOrEmpty(this.vimPath))
            {
                throw new InvalidOperationException("Vim executable path is not configured.");
            }

            if (this.vimPath.IndexOf(Path.DirectorySeparatorChar) >= 0 && !File.Exists(this.vimPath))
            {
                throw new FileNotFoundException(
                    $"Vim executable not found at '{this.vimPath}'.");
            }

            Console.Error.WriteLine(
                "VimClient: Spawning Vim at '{0}' ({1}x{2})",
                this.vimPath,
                cols,
                rows);

            var env = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                env[(string)entry.Key] = (string?)entry.Value ?? string.Empty;
            }

            env["TERM"] = "xterm-256color";
            env["COLORTERM"] = "truecolor";

            string cwd = this.workingDirectory
                ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            lock (this.screenLock)
            {
                this.buffer.Resize((int)cols, (int)rows);
            }

            this.ptyConnection = new NativePtyConnection(
                this.vimPath,
                Array.Empty<string>(),
                env,
                cwd,
                (int)rows,
                (int)cols);

            this.ptyConnection.ProcessExited += this.OnProcessExited;

            // Guard against the process having exited before the handler was attached.
            if (this.ptyConnection.WaitForExit(0))
            {
                this.OnProcessExited(this.ptyConnection, EventArgs.Empty);
                return;
            }

            _ = Task.Run(() => this.ReadLoopAsync());

            this.ReplayPendingCommands();

            Console.Error.WriteLine(
                "VimClient: Vim process started successfully (pid={0})",
                this.ptyConnection.Pid);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "VimClient: Failed to spawn Vim at '{0}': {1}",
                this.vimPath,
                ex.Message);
            this.EditorExited?.Invoke(-1);
        }
    }

    private async Task ReadLoopAsync()
    {
        var readBuffer = new byte[4096];
        try
        {
            while (!this.disposed)
            {
                int bytesRead = await this.ptyConnection!.ReaderStream.ReadAsync(readBuffer, 0, readBuffer.Length).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                lock (this.screenLock)
                {
                    this.parser.Process(readBuffer.AsSpan(0, bytesRead));
                }

                this.Redraw?.Invoke();
            }
        }
        catch (Exception) when (this.disposed)
        {
            // Expected during cleanup
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (!this.disposed && Interlocked.CompareExchange(ref this.processExitHandled, 1, 0) == 0)
        {
            int exitCode = this.ptyConnection?.ExitCode ?? 0;
            int signal = this.ptyConnection?.ExitSignalNumber ?? 0;
            int pid = this.ptyConnection?.Pid ?? 0;
            Console.Error.WriteLine(
                "VimClient: Vim process exited (pid={0}, code={1}, signal={2})",
                pid,
                exitCode,
                signal);
            this.EditorExited?.Invoke(exitCode);
        }
    }

    private void ReplayPendingCommands()
    {
        while (this.pendingCommands.Count > 0)
        {
            string command = this.pendingCommands.Dequeue();
            this.Input("\x1B");
            this.Input($":{command}\r");
        }
    }

    private void OnTitleChanged(string title)
    {
        this.TitleChanged?.Invoke(title);
    }
}
