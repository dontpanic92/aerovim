// <copyright file="VimClient.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.VimClient;

using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using AeroVim.Editor;
using AeroVim.Editor.Capabilities;
using AeroVim.Editor.Diagnostics;
using AeroVim.Editor.Utilities;

/// <summary>
/// A Vim editor client that communicates with Vim through a PTY using
/// VT escape sequences. Implements <see cref="IEditorClient"/>.
/// </summary>
public sealed class VimClient : IEditorClient, ITerminalCapabilities, IStartupDiagnostics
{
    private readonly string vimPath;
    private readonly string? workingDirectory;
    private readonly IReadOnlyList<string>? fileArgs;
    private readonly IComponentLogger log;
    private readonly TerminalBuffer buffer;
    private readonly VtParser parser;
    private readonly object screenLock = new();
    private readonly object writeLock = new();
    private readonly Queue<string> pendingCommands = new();
    private byte[] inputBuffer = new byte[256];

    private IPtyConnection? ptyConnection;
    private Task? spawnTask;
    private int deferredCols;
    private int deferredRows;
    private bool disposed;
    private bool contentReceived;
    private int processExitHandled;
    private string? lastStartupError;
    private ModeInfo currentModeInfo;
    private ColorChangedHandler? foregroundColorChanged;
    private ColorChangedHandler? backgroundColorChanged;
    private FontChangedHandler? fontChanged;
    private int lastReportedFg = 0x000000;
    private int lastReportedBg = 0xFFFFFF;

    /// <summary>
    /// Initializes a new instance of the <see cref="VimClient"/> class.
    /// </summary>
    /// <param name="vimPath">Path to the Vim executable.</param>
    /// <param name="logger">Application logger.</param>
    /// <param name="workingDirectory">Optional working directory for Vim.</param>
    /// <param name="initialBackgroundColor">Initial default background color in RGB format, e.g. from saved settings.</param>
    /// <param name="fileArgs">Optional file paths to open on startup.</param>
    public VimClient(string vimPath, IAppLogger logger, string? workingDirectory = null, int initialBackgroundColor = 0xFFFFFF, IReadOnlyList<string>? fileArgs = null)
    {
        this.vimPath = vimPath ?? throw new ArgumentNullException(nameof(vimPath));
        this.log = logger.For<VimClient>();
        this.workingDirectory = workingDirectory;
        this.fileArgs = fileArgs;
        this.buffer = new TerminalBuffer(80, 24);
        this.buffer.SetDetectedBackground(initialBackgroundColor);
        this.buffer.SetTerminalDefaultBackground(initialBackgroundColor);
        this.lastReportedBg = initialBackgroundColor;
        this.parser = new VtParser(this.buffer, this.OnTitleChanged, this.WriteToPty);
        this.currentModeInfo = new ModeInfo(CursorShape.Block, 100, CursorBlinking.BlinkOff);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VimClient"/> class with an attached PTY test double.
    /// </summary>
    /// <param name="vimPath">Path to the Vim executable.</param>
    /// <param name="ptyConnection">The PTY connection to attach.</param>
    /// <param name="logger">Application logger.</param>
    /// <param name="workingDirectory">Optional working directory for Vim.</param>
    /// <param name="initialBackgroundColor">Initial background color in RGB format.</param>
    internal VimClient(string vimPath, IPtyConnection ptyConnection, IAppLogger logger, string? workingDirectory = null, int initialBackgroundColor = 0xFFFFFF)
        : this(vimPath, logger, workingDirectory, initialBackgroundColor)
    {
        ArgumentNullException.ThrowIfNull(ptyConnection);
        this.AttachPtyConnection(ptyConnection);
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
    /// Gets the current mode info (cursor shape, pointer shape, visibility, and blink state).
    /// When DECSCUSR has been received, the requested cursor shape overrides the default.
    /// </summary>
    public ModeInfo ModeInfo
    {
        get
        {
            var shape = this.buffer.RequestedCursorShape ?? this.currentModeInfo.CursorShape;
            var cursorBlinking = this.buffer.RequestedCursorBlinking;
            return new ModeInfo(
                shape,
                this.currentModeInfo.CellPercentage,
                cursorBlinking,
                pointerShape: this.buffer.PointerShape,
                cursorVisible: this.buffer.CursorVisible,
                cursorStyleEnabled: true,
                pointerMode: (PointerMode)this.buffer.PointerMode);
        }
    }

    /// <summary>
    /// Gets a value indicating whether mouse input is enabled by the editor.
    /// For the Vim backend, mouse mode is tracked via the SGR mouse mode escape sequence.
    /// </summary>
    public bool MouseEnabled => this.buffer.SgrMouseEnabled;

    /// <summary>
    /// Gets a value indicating whether bracketed paste mode is enabled.
    /// When true, the frontend should wrap pasted text in ESC[200~ ... ESC[201~.
    /// </summary>
    public bool BracketedPasteEnabled => this.buffer.BracketedPasteEnabled;

    /// <summary>
    /// Gets a value indicating whether focus event reporting is enabled.
    /// When true, the frontend should send ESC[I on focus-in and ESC[O on focus-out.
    /// </summary>
    public bool FocusEventsEnabled => this.buffer.FocusEventsEnabled;

    /// <summary>
    /// Gets a value indicating whether synchronized output mode is active.
    /// When true, the frontend should defer rendering until the mode is cleared.
    /// </summary>
    public bool SynchronizedOutput => this.buffer.SynchronizedOutput;

    /// <summary>
    /// Gets the current font settings.
    /// </summary>
    public FontSettings FontSettings { get; private set; } = new FontSettings();

    /// <summary>
    /// Gets a classified error message describing why the last startup
    /// attempt failed, or <c>null</c> if startup succeeded or has not
    /// been attempted. This is set when <see cref="EditorExited"/> fires
    /// with exit code -1, allowing callers to distinguish "not found"
    /// from "crashed" from "PTY failure".
    /// </summary>
    public string? LastStartupError => this.lastStartupError;

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
            else
            {
                this.deferredCols = (int)width;
                this.deferredRows = (int)height;
            }

            return;
        }

        bool sizeChanged;
        lock (this.screenLock)
        {
            sizeChanged = (int)width != this.buffer.Cols || (int)height != this.buffer.Rows;
            if (sizeChanged)
            {
                this.buffer.Resize((int)width, (int)height);
            }
        }

        if (sizeChanged)
        {
            this.ptyConnection.Resize((int)width, (int)height);
        }
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

        string encoded = TerminalInputEncoder.Encode(text, this.buffer.ApplicationCursorKeys);
        lock (this.writeLock)
        {
            int byteCount = Encoding.UTF8.GetByteCount(encoded);
            if (byteCount > this.inputBuffer.Length)
            {
                this.inputBuffer = new byte[byteCount];
            }

            int written = Encoding.UTF8.GetBytes(encoded, 0, encoded.Length, this.inputBuffer, 0);
            this.ptyConnection.WriterStream.Write(this.inputBuffer, 0, written);
            this.ptyConnection.WriterStream.Flush();
        }
    }

    /// <summary>
    /// Send a mouse event to Vim using SGR mouse encoding.
    /// </summary>
    /// <param name="button">The mouse button.</param>
    /// <param name="action">The mouse action.</param>
    /// <param name="modifier">Modifier keys string, e.g. "", "S", "C", "A", "C-S".</param>
    /// <param name="grid">Grid id (unused for Vim, kept for interface compatibility).</param>
    /// <param name="row">Zero-based grid row.</param>
    /// <param name="col">Zero-based grid column.</param>
    public void InputMouse(MouseButton button, MouseAction action, string modifier, int grid, int row, int col)
    {
        if (this.ptyConnection is null || this.disposed)
        {
            return;
        }

        string? encoded = EncodeSgrMouse(button, action, modifier, row, col);
        if (encoded is not null)
        {
            lock (this.writeLock)
            {
                int byteCount = Encoding.UTF8.GetByteCount(encoded);
                if (byteCount > this.inputBuffer.Length)
                {
                    this.inputBuffer = new byte[byteCount];
                }

                int written = Encoding.UTF8.GetBytes(encoded, 0, encoded.Length, this.inputBuffer, 0);
                this.ptyConnection.WriterStream.Write(this.inputBuffer, 0, written);
                this.ptyConnection.WriterStream.Flush();
            }
        }
    }

    /// <summary>
    /// Execute a Vim command by entering command-line mode.
    /// If the PTY is not yet connected, the command is passed as a
    /// <c>--cmd</c> argument to vim at startup, which is executed
    /// reliably during vim's initialization — no race condition.
    /// </summary>
    /// <param name="command">The command string (without leading colon).</param>
    public void Command(string command)
    {
        if (this.ptyConnection is null && !this.disposed)
        {
            this.pendingCommands.Enqueue(command);
            return;
        }

        this.SendCommandViaPty(command);
    }

    /// <summary>
    /// Get the current screen state for rendering.
    /// </summary>
    /// <returns>The current screen state, or null if not yet initialized.</returns>
    public Screen? GetScreen()
    {
        Screen? screen;
        ColorChangedHandler? fireFg = null;
        ColorChangedHandler? fireBg = null;
        int newFg = 0;
        int newBg = 0;

        lock (this.screenLock)
        {
            if (!this.contentReceived)
            {
                return null;
            }

            screen = this.buffer.GetScreenNoLock();
            if (screen is not null)
            {
                if (screen.ForegroundColor != this.lastReportedFg)
                {
                    this.lastReportedFg = screen.ForegroundColor;
                    newFg = screen.ForegroundColor;
                    fireFg = this.foregroundColorChanged;
                }

                if (screen.BackgroundColor != this.lastReportedBg)
                {
                    this.lastReportedBg = screen.BackgroundColor;
                    newBg = screen.BackgroundColor;
                    fireBg = this.backgroundColorChanged;
                }
            }
        }

        fireFg?.Invoke(newFg);
        fireBg?.Invoke(newBg);

        return screen;
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

    /// <summary>
    /// Processes PTY output directly and raises redraw for deterministic tests.
    /// </summary>
    /// <param name="data">The PTY output bytes to process.</param>
    internal void ProcessOutputForTesting(ReadOnlySpan<byte> data)
    {
        lock (this.screenLock)
        {
            this.parser.Process(data);
            this.contentReceived = true;
        }

        this.Redraw?.Invoke();
    }

    private static string? EncodeSgrMouse(MouseButton button, MouseAction action, string modifier, int row, int col)
    {
        int cb;

        switch (button)
        {
            case MouseButton.Left:
                cb = 0;
                break;
            case MouseButton.Middle:
                cb = 1;
                break;
            case MouseButton.Right:
                cb = 2;
                break;
            case MouseButton.Wheel:
                switch (action)
                {
                    case MouseAction.ScrollUp:
                        cb = 64;
                        break;
                    case MouseAction.ScrollDown:
                        cb = 65;
                        break;
                    case MouseAction.ScrollLeft:
                        cb = 66;
                        break;
                    case MouseAction.ScrollRight:
                        cb = 67;
                        break;
                    default:
                        return null;
                }

                AddModifierBits(ref cb, modifier);
                return $"\x1B[<{cb};{col + 1};{row + 1}M";
            case MouseButton.Move:
                cb = 35;
                AddModifierBits(ref cb, modifier);
                return $"\x1B[<{cb};{col + 1};{row + 1}M";
            default:
                return null;
        }

        if (action == MouseAction.Drag)
        {
            cb += 32;
        }

        AddModifierBits(ref cb, modifier);
        char finalChar = action == MouseAction.Release ? 'm' : 'M';
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

    private void WriteToPty(byte[] data)
    {
        if (this.ptyConnection is not null && !this.disposed)
        {
            lock (this.writeLock)
            {
                this.ptyConnection.WriterStream.Write(data, 0, data.Length);
                this.ptyConnection.WriterStream.Flush();
            }
        }
    }

    private async Task SpawnVimAsync(uint cols, uint rows)
    {
        try
        {
            if (string.IsNullOrEmpty(this.vimPath))
            {
                this.lastStartupError = "Vim executable path is not configured. Please set the path in Settings.";
                throw new InvalidOperationException("Vim executable path is not configured.");
            }

            if (this.vimPath.IndexOf(Path.DirectorySeparatorChar) >= 0 && !File.Exists(this.vimPath))
            {
                this.lastStartupError = $"Vim executable was not found at:\n{this.vimPath}\n\nPlease verify the path in Settings or use Detect to find it.";
                throw new FileNotFoundException(
                    $"Vim executable not found at '{this.vimPath}'.");
            }

            this.log.Info(
                $"Spawning Vim at '{this.vimPath}' ({cols}x{rows})");

            var env = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                env[(string)entry.Key] = (string?)entry.Value ?? string.Empty;
            }

            env["TERM"] = "xterm-256color";
            env["COLORTERM"] = "truecolor";

            // Remove terminal-emulator-specific variables that the parent
            // shell may have set.  Vim and other TUI apps detect these and
            // enable protocol extensions (e.g. iTerm2 key encoding, Kitty
            // keyboard protocol) that AeroVim's VT parser does not support,
            // causing garbled key display and wrong cursor positioning.
            env.Remove("TERM_PROGRAM");
            env.Remove("TERM_PROGRAM_VERSION");
            env.Remove("TERM_SESSION_ID");
            env.Remove("TERM_FEATURES");
            env.Remove("LC_TERMINAL");
            env.Remove("LC_TERMINAL_VERSION");
            env.Remove("ITERM_SESSION_ID");
            env.Remove("ITERM_PROFILE");
            env.Remove("KITTY_WINDOW_ID");
            env.Remove("KITTY_PID");
            env.Remove("KITTY_INSTALLATION_DIR");
            env.Remove("KONSOLE_DBUS_SESSION");
            env.Remove("KONSOLE_VERSION");
            env.Remove("WEZTERM_PANE");
            env.Remove("WEZTERM_EXECUTABLE");
            env.Remove("VTE_VERSION");
            env.Remove("WT_SESSION");
            env.Remove("WT_PROFILE_ID");
            env.Remove("ALACRITTY_WINDOW_ID");
            env.Remove("ALACRITTY_LOG");
            env.Remove("TERMINFO_DIRS");

            string cwd = this.workingDirectory
                ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            lock (this.screenLock)
            {
                this.buffer.Resize((int)cols, (int)rows);
            }

            var vimArgsList = new List<string>();

            // Drain pending commands into --cmd arguments so they execute
            // during vim's own initialization, avoiding the race condition
            // of sending ESC + :cmd over the PTY before vim is ready.
            while (this.pendingCommands.Count > 0)
            {
                string cmd = this.pendingCommands.Dequeue();
                vimArgsList.Add("--cmd");
                vimArgsList.Add(cmd);
            }

            if (this.fileArgs is not null)
            {
                foreach (var f in this.fileArgs)
                {
                    vimArgsList.Add(f);
                }
            }

            var vimArgs = vimArgsList.ToArray();

            this.ptyConnection = PtyConnectionFactory.Create(
                this.vimPath,
                vimArgs,
                env,
                cwd,
                (int)rows,
                (int)cols);

            this.AttachPtyConnection(this.ptyConnection);

            // Guard against the process having exited before the handler was attached.
            if (this.ptyConnection.WaitForExit(0))
            {
                this.OnProcessExited(this.ptyConnection, EventArgs.Empty);
                return;
            }

            _ = Task.Run(() => this.ReadLoopAsync());

            // Pre-spawn commands were already passed as --cmd arguments.
            // Any commands queued during the spawn window (unlikely) are
            // replayed from the read loop after vim outputs its first frame.

            // Apply any resize that arrived while the PTY was still being spawned.
            int dc = this.deferredCols;
            int dr = this.deferredRows;
            if (dc > 0 && dr > 0 && (dc != (int)cols || dr != (int)rows))
            {
                lock (this.screenLock)
                {
                    this.buffer.Resize(dc, dr);
                }

                this.ptyConnection.Resize(dc, dr);
            }

            this.deferredCols = 0;
            this.deferredRows = 0;

            this.log.Info(
                $"Vim process started successfully (pid={this.ptyConnection.Pid})");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            this.lastStartupError = $"Vim could not be started from:\n{this.vimPath}\n\n{ex.Message}";
            this.log.Error(
                $"Failed to spawn Vim at '{this.vimPath}'.",
                ex);
            this.EditorExited?.Invoke(-1);
        }
        catch (Exception ex)
        {
            this.lastStartupError ??= $"Vim failed to start from '{this.vimPath}': {ex.Message}";
            this.log.Error(
                $"Failed to spawn Vim at '{this.vimPath}'.",
                ex);
            this.EditorExited?.Invoke(-1);
        }
    }

    private async Task ReadLoopAsync()
    {
        var readBuffer = new byte[4096];
        try
        {
            var readTask = this.ptyConnection!.ReaderStream.ReadAsync(readBuffer, 0, readBuffer.Length);

            while (!this.disposed)
            {
                int bytesRead = await readTask.ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                lock (this.screenLock)
                {
                    this.parser.Process(readBuffer.AsSpan(0, bytesRead));
                    if (!this.contentReceived)
                    {
                        this.contentReceived = true;

                        // Vim has drawn its first frame — now safe to send
                        // queued commands (e.g. "set mouse=a").
                        this.ReplayPendingCommands();
                    }
                }

                // Start the next read immediately. If data is already
                // buffered the task completes synchronously and we
                // process it before notifying the renderer. This
                // coalesces rapid PTY writes into fewer render frames,
                // preventing partial screen updates such as ghost
                // status-bar rows during split resize.
                readTask = this.ptyConnection.ReaderStream.ReadAsync(readBuffer, 0, readBuffer.Length);
                if (!readTask.IsCompleted && !this.buffer.SynchronizedOutput)
                {
                    this.Redraw?.Invoke();
                }
            }
        }
        catch (Exception) when (this.disposed)
        {
            // Expected during cleanup
        }
    }

    private void AttachPtyConnection(IPtyConnection ptyConnection)
    {
        this.ptyConnection = ptyConnection;
        this.ptyConnection.ProcessExited += this.OnProcessExited;
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (!this.disposed && Interlocked.CompareExchange(ref this.processExitHandled, 1, 0) == 0)
        {
            int exitCode = this.ptyConnection?.ExitCode ?? 0;
            int signal = this.ptyConnection?.ExitSignalNumber ?? 0;
            int pid = this.ptyConnection?.Pid ?? 0;
            this.log.Info(
                $"Vim process exited (pid={pid}, code={exitCode}, signal={signal})");
            this.EditorExited?.Invoke(exitCode);
        }
    }

    private void ReplayPendingCommands()
    {
        while (this.pendingCommands.Count > 0)
        {
            string command = this.pendingCommands.Dequeue();
            this.SendCommandViaPty(command);
        }
    }

    private void SendCommandViaPty(string command)
    {
        this.Input("\x1B");
        this.Input($":{command}\r");
    }

    private void OnTitleChanged(string title)
    {
        this.TitleChanged?.Invoke(title);
    }
}
