// <copyright file="WindowsPtyConnection.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.VimClient;

using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

/// <summary>
/// Windows PTY backend built on the ConPTY APIs.
/// </summary>
internal sealed class WindowsPtyConnection : IPtyConnection
{
    private readonly Thread watcherThread;
    private readonly IntPtr processHandle;
    private readonly IntPtr threadHandle;
    private readonly IntPtr pseudoConsole;
    private int exitCode;
    private bool exited;
    private bool disposed;
    private int exitHandled;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsPtyConnection"/> class.
    /// </summary>
    /// <param name="app">Absolute path to the executable.</param>
    /// <param name="args">Command-line arguments excluding argv[0].</param>
    /// <param name="environment">Environment variables for the child process.</param>
    /// <param name="cwd">Working directory for the child process.</param>
    /// <param name="rows">Initial terminal rows.</param>
    /// <param name="cols">Initial terminal columns.</param>
    public WindowsPtyConnection(
        string app,
        string[] args,
        IDictionary<string, string> environment,
        string cwd,
        int rows,
        int cols)
    {
        IntPtr ptyInputRead = IntPtr.Zero;
        IntPtr terminalInputWrite = IntPtr.Zero;
        IntPtr terminalOutputRead = IntPtr.Zero;
        IntPtr ptyOutputWrite = IntPtr.Zero;
        IntPtr attributeList = IntPtr.Zero;
        IntPtr environmentBlock = IntPtr.Zero;
        NativeMethods.PROCESS_INFORMATION processInformation = default;

        try
        {
            var securityAttributes = new NativeMethods.SECURITY_ATTRIBUTES
            {
                nLength = Marshal.SizeOf<NativeMethods.SECURITY_ATTRIBUTES>(),
                bInheritHandle = true,
            };

            if (!NativeMethods.CreatePipe(out ptyInputRead, out terminalInputWrite, ref securityAttributes, 0))
            {
                throw CreateWin32Exception("Failed to create ConPTY input pipe.");
            }

            if (!NativeMethods.CreatePipe(out terminalOutputRead, out ptyOutputWrite, ref securityAttributes, 0))
            {
                throw CreateWin32Exception("Failed to create ConPTY output pipe.");
            }

            if (!NativeMethods.SetHandleInformation(terminalInputWrite, NativeMethods.HANDLE_FLAG_INHERIT, 0))
            {
                throw CreateWin32Exception("Failed to mark terminal input pipe as non-inheritable.");
            }

            if (!NativeMethods.SetHandleInformation(terminalOutputRead, NativeMethods.HANDLE_FLAG_INHERIT, 0))
            {
                throw CreateWin32Exception("Failed to mark terminal output pipe as non-inheritable.");
            }

            int createPseudoConsoleResult = NativeMethods.CreatePseudoConsole(
                new NativeMethods.COORD((short)cols, (short)rows),
                ptyInputRead,
                ptyOutputWrite,
                0,
                out this.pseudoConsole);
            if (createPseudoConsoleResult != 0)
            {
                Marshal.ThrowExceptionForHR(createPseudoConsoleResult);
            }

            NativeMethods.CloseHandle(ptyInputRead);
            ptyInputRead = IntPtr.Zero;
            NativeMethods.CloseHandle(ptyOutputWrite);
            ptyOutputWrite = IntPtr.Zero;

            IntPtr attributeListSize = IntPtr.Zero;
            NativeMethods.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attributeListSize);
            int initializeError = Marshal.GetLastWin32Error();
            if (initializeError != NativeMethods.ERROR_INSUFFICIENT_BUFFER)
            {
                throw new InvalidOperationException(
                    $"Failed to size the process attribute list (error={initializeError}).");
            }

            attributeList = Marshal.AllocHGlobal(attributeListSize);
            if (!NativeMethods.InitializeProcThreadAttributeList(attributeList, 1, 0, ref attributeListSize))
            {
                throw CreateWin32Exception("Failed to initialize the process attribute list.");
            }

            if (!NativeMethods.UpdateProcThreadAttribute(
                attributeList,
                0,
                (IntPtr)NativeMethods.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                this.pseudoConsole,
                (IntPtr)IntPtr.Size,
                IntPtr.Zero,
                IntPtr.Zero))
            {
                throw CreateWin32Exception("Failed to attach the pseudo console attribute.");
            }

            environmentBlock = BuildEnvironmentBlock(environment);
            var startupInfo = new NativeMethods.STARTUPINFOEX
            {
                StartupInfo = new NativeMethods.STARTUPINFO
                {
                    cb = Marshal.SizeOf<NativeMethods.STARTUPINFOEX>(),
                },
                lpAttributeList = attributeList,
            };

            string commandLine = BuildCommandLine(app, args);
            var commandLineBuilder = new StringBuilder(commandLine);
            if (!NativeMethods.CreateProcessW(
                app,
                commandLineBuilder,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                NativeMethods.EXTENDED_STARTUPINFO_PRESENT | NativeMethods.CREATE_UNICODE_ENVIRONMENT,
                environmentBlock,
                string.IsNullOrEmpty(cwd) ? null : cwd,
                ref startupInfo,
                out processInformation))
            {
                throw CreateWin32Exception($"Failed to create Vim process '{app}'.");
            }

            this.Pid = unchecked((int)processInformation.dwProcessId);
            this.processHandle = processInformation.hProcess;
            this.threadHandle = processInformation.hThread;
            this.ReaderStream = new FileStream(
                new SafeFileHandle(terminalOutputRead, ownsHandle: true),
                FileAccess.Read,
                bufferSize: 4096,
                isAsync: false);
            this.WriterStream = new FileStream(
                new SafeFileHandle(terminalInputWrite, ownsHandle: true),
                FileAccess.Write,
                bufferSize: 0,
                isAsync: false);
            terminalOutputRead = IntPtr.Zero;
            terminalInputWrite = IntPtr.Zero;

            this.watcherThread = new Thread(this.WatcherThreadProc)
            {
                IsBackground = true,
                Name = $"PTY watcher for pid {this.Pid}",
            };
            this.watcherThread.Start();
        }
        finally
        {
            if (environmentBlock != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(environmentBlock);
            }

            if (attributeList != IntPtr.Zero)
            {
                NativeMethods.DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }

            if (ptyInputRead != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(ptyInputRead);
            }

            if (ptyOutputWrite != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(ptyOutputWrite);
            }

            if (terminalOutputRead != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(terminalOutputRead);
            }

            if (terminalInputWrite != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(terminalInputWrite);
            }

            if (processInformation.hProcess != IntPtr.Zero && processInformation.hProcess != this.processHandle)
            {
                NativeMethods.CloseHandle(processInformation.hProcess);
            }

            if (processInformation.hThread != IntPtr.Zero && processInformation.hThread != this.threadHandle)
            {
                NativeMethods.CloseHandle(processInformation.hThread);
            }

            if (this.processHandle == IntPtr.Zero && this.pseudoConsole != IntPtr.Zero)
            {
                NativeMethods.ClosePseudoConsole(this.pseudoConsole);
            }
        }
    }

    /// <summary>
    /// Occurs when the child process has exited.
    /// </summary>
    public event EventHandler? ProcessExited;

    /// <summary>
    /// Gets the child process ID.
    /// </summary>
    public int Pid { get; }

    /// <summary>
    /// Gets the child process exit code.
    /// </summary>
    public int ExitCode => this.exitCode;

    /// <summary>
    /// Gets the child process termination signal.
    /// </summary>
    public int ExitSignalNumber => 0;

    /// <summary>
    /// Gets the PTY output stream.
    /// </summary>
    public Stream ReaderStream { get; }

    /// <summary>
    /// Gets the PTY input stream.
    /// </summary>
    public Stream WriterStream { get; }

    /// <summary>
    /// Wait for the child process to exit.
    /// </summary>
    /// <param name="milliseconds">Time to wait in milliseconds.</param>
    /// <returns><c>true</c> if the process has exited; otherwise, <c>false</c>.</returns>
    public bool WaitForExit(int milliseconds)
    {
        if (this.exited)
        {
            return true;
        }

        uint waitMilliseconds = milliseconds <= 0 ? 0U : unchecked((uint)milliseconds);
        uint result = NativeMethods.WaitForSingleObject(this.processHandle, waitMilliseconds);
        if (result == NativeMethods.WAIT_OBJECT_0)
        {
            this.MarkExited();
            return true;
        }

        if (result == NativeMethods.WAIT_TIMEOUT)
        {
            return this.exited;
        }

        throw CreateWin32Exception("Failed while waiting for the Vim process to exit.");
    }

    /// <summary>
    /// Resize the pseudo console.
    /// </summary>
    /// <param name="cols">The new column count.</param>
    /// <param name="rows">The new row count.</param>
    public void Resize(int cols, int rows)
    {
        if (this.disposed)
        {
            return;
        }

        int result = NativeMethods.ResizePseudoConsole(
            this.pseudoConsole,
            new NativeMethods.COORD((short)cols, (short)rows));
        if (result != 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }
    }

    /// <summary>
    /// Terminate the child process.
    /// </summary>
    public void Kill()
    {
        if (this.exited || this.processHandle == IntPtr.Zero)
        {
            return;
        }

        if (!NativeMethods.TerminateProcess(this.processHandle, 1))
        {
            int error = Marshal.GetLastWin32Error();
            if (error != NativeMethods.ERROR_ACCESS_DENIED && !this.exited)
            {
                throw new InvalidOperationException(
                    $"Failed to terminate Vim process {this.Pid} (error={error}).");
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.Kill();

        try
        {
            this.WriterStream.Dispose();
        }
        catch (IOException)
        {
            // May fail if the child side already closed the pipe.
        }

        try
        {
            this.ReaderStream.Dispose();
        }
        catch (IOException)
        {
            // May fail if the child side already closed the pipe.
        }

        if (this.threadHandle != IntPtr.Zero)
        {
            NativeMethods.CloseHandle(this.threadHandle);
        }

        if (this.processHandle != IntPtr.Zero)
        {
            NativeMethods.CloseHandle(this.processHandle);
        }

        if (this.pseudoConsole != IntPtr.Zero)
        {
            NativeMethods.ClosePseudoConsole(this.pseudoConsole);
        }
    }

    private static InvalidOperationException CreateWin32Exception(string message)
    {
        int error = Marshal.GetLastWin32Error();
        return new InvalidOperationException($"{message} (error={error}).");
    }

    private static IntPtr BuildEnvironmentBlock(IDictionary<string, string> environment)
    {
        var builder = new StringBuilder();
        foreach (var entry in environment.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(entry.Key);
            builder.Append('=');
            builder.Append(entry.Value);
            builder.Append('\0');
        }

        builder.Append('\0');
        return Marshal.StringToHGlobalUni(builder.ToString());
    }

    private static string BuildCommandLine(string app, string[] args)
    {
        var parts = new List<string>(1 + (args?.Length ?? 0))
        {
            QuoteArgument(app),
        };

        if (args is not null)
        {
            foreach (string arg in args)
            {
                parts.Add(QuoteArgument(arg));
            }
        }

        return string.Join(" ", parts);
    }

    private static string QuoteArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        bool requiresQuoting = value.Any(ch => char.IsWhiteSpace(ch) || ch == '"');
        if (!requiresQuoting)
        {
            return value;
        }

        var builder = new StringBuilder();
        builder.Append('"');

        int backslashCount = 0;
        foreach (char ch in value)
        {
            if (ch == '\\')
            {
                backslashCount++;
                continue;
            }

            if (ch == '"')
            {
                builder.Append('\\', (backslashCount * 2) + 1);
                builder.Append('"');
                backslashCount = 0;
                continue;
            }

            if (backslashCount > 0)
            {
                builder.Append('\\', backslashCount);
                backslashCount = 0;
            }

            builder.Append(ch);
        }

        if (backslashCount > 0)
        {
            builder.Append('\\', backslashCount * 2);
        }

        builder.Append('"');
        return builder.ToString();
    }

    private void WatcherThreadProc()
    {
        uint result = NativeMethods.WaitForSingleObject(this.processHandle, NativeMethods.INFINITE);
        if (result == NativeMethods.WAIT_OBJECT_0)
        {
            this.MarkExited();
        }
    }

    private void MarkExited()
    {
        if (Interlocked.CompareExchange(ref this.exitHandled, 1, 0) != 0)
        {
            this.exited = true;
            return;
        }

        if (!NativeMethods.GetExitCodeProcess(this.processHandle, out this.exitCode))
        {
            this.exitCode = 0;
        }

        this.exited = true;
        this.ProcessExited?.Invoke(this, EventArgs.Empty);
    }

    private static class NativeMethods
    {
        public const int ERROR_ACCESS_DENIED = 5;
        public const int ERROR_INSUFFICIENT_BUFFER = 122;
        public const int HANDLE_FLAG_INHERIT = 1;
        public const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;
        public const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        public const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        public const uint INFINITE = 0xFFFFFFFF;
        public const uint WAIT_OBJECT_0 = 0;
        public const uint WAIT_TIMEOUT = 258;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CreatePipe(
            out IntPtr hReadPipe,
            out IntPtr hWritePipe,
            ref SECURITY_ATTRIBUTES lpPipeAttributes,
            int nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetHandleInformation(
            IntPtr hObject,
            int dwMask,
            int dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int CreatePseudoConsole(
            COORD size,
            IntPtr hInput,
            IntPtr hOutput,
            uint dwFlags,
            out IntPtr phPC);

        [DllImport("kernel32.dll")]
        public static extern void ClosePseudoConsole(IntPtr hPC);

        [DllImport("kernel32.dll")]
        public static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool InitializeProcThreadAttributeList(
            IntPtr lpAttributeList,
            int dwAttributeCount,
            int dwFlags,
            ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UpdateProcThreadAttribute(
            IntPtr lpAttributeList,
            uint dwFlags,
            IntPtr attribute,
            IntPtr lpValue,
            IntPtr cbSize,
            IntPtr lpPreviousValue,
            IntPtr lpReturnSize);

        [DllImport("kernel32.dll")]
        public static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CreateProcessW(
            string lpApplicationName,
            StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string? lpCurrentDirectory,
            [In] ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetExitCodeProcess(IntPtr hProcess, out int lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        public struct COORD
        {
            /// <summary>
            /// Gets or sets the X coordinate.
            /// </summary>
            public short X;

            /// <summary>
            /// Gets or sets the Y coordinate.
            /// </summary>
            public short Y;

            /// <summary>
            /// Initializes a new instance of the <see cref="COORD"/> struct.
            /// </summary>
            /// <param name="x">The X coordinate.</param>
            /// <param name="y">The Y coordinate.</param>
            public COORD(short x, short y)
            {
                this.X = x;
                this.Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_ATTRIBUTES
        {
            /// <summary>
            /// Gets or sets the structure length.
            /// </summary>
            public int nLength;

            /// <summary>
            /// Gets or sets a pointer to the security descriptor.
            /// </summary>
            public IntPtr lpSecurityDescriptor;

            /// <summary>
            /// Gets or sets a value indicating whether the handle is inheritable.
            /// </summary>
            [MarshalAs(UnmanagedType.Bool)]
            public bool bInheritHandle;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            /// <summary>
            /// Gets or sets the structure size.
            /// </summary>
            public int cb;

            /// <summary>
            /// Gets or sets the reserved value.
            /// </summary>
            public string? lpReserved;

            /// <summary>
            /// Gets or sets the desktop name.
            /// </summary>
            public string? lpDesktop;

            /// <summary>
            /// Gets or sets the window title.
            /// </summary>
            public string? lpTitle;

            /// <summary>
            /// Gets or sets the X offset.
            /// </summary>
            public int dwX;

            /// <summary>
            /// Gets or sets the Y offset.
            /// </summary>
            public int dwY;

            /// <summary>
            /// Gets or sets the X size.
            /// </summary>
            public int dwXSize;

            /// <summary>
            /// Gets or sets the Y size.
            /// </summary>
            public int dwYSize;

            /// <summary>
            /// Gets or sets the X character count.
            /// </summary>
            public int dwXCountChars;

            /// <summary>
            /// Gets or sets the Y character count.
            /// </summary>
            public int dwYCountChars;

            /// <summary>
            /// Gets or sets the fill attribute.
            /// </summary>
            public int dwFillAttribute;

            /// <summary>
            /// Gets or sets the startup flags.
            /// </summary>
            public int dwFlags;

            /// <summary>
            /// Gets or sets the show-window value.
            /// </summary>
            public short wShowWindow;

            /// <summary>
            /// Gets or sets the reserved two value.
            /// </summary>
            public short cbReserved2;

            /// <summary>
            /// Gets or sets the reserved two pointer.
            /// </summary>
            public IntPtr lpReserved2;

            /// <summary>
            /// Gets or sets the standard input handle.
            /// </summary>
            public IntPtr hStdInput;

            /// <summary>
            /// Gets or sets the standard output handle.
            /// </summary>
            public IntPtr hStdOutput;

            /// <summary>
            /// Gets or sets the standard error handle.
            /// </summary>
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct STARTUPINFOEX
        {
            /// <summary>
            /// Gets or sets the startup info.
            /// </summary>
            public STARTUPINFO StartupInfo;

            /// <summary>
            /// Gets or sets the attribute list pointer.
            /// </summary>
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            /// <summary>
            /// Gets or sets the process handle.
            /// </summary>
            public IntPtr hProcess;

            /// <summary>
            /// Gets or sets the thread handle.
            /// </summary>
            public IntPtr hThread;

            /// <summary>
            /// Gets or sets the process identifier.
            /// </summary>
            public uint dwProcessId;

            /// <summary>
            /// Gets or sets the thread identifier.
            /// </summary>
            public uint dwThreadId;
        }
    }
}
