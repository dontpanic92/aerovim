// <copyright file="NativePtyConnection.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.VimClient
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;

    /// <summary>
    /// Managed wrapper around the native PTY helper library.
    /// Uses a small C helper (<c>libaerovim_pty</c>) to perform
    /// <c>forkpty</c> + <c>execve</c> entirely in native code,
    /// avoiding the .NET runtime's fork-safety issues.
    /// </summary>
    internal sealed class NativePtyConnection : IDisposable
    {
        private const string LibName = "libaerovim_pty";
        private const string LibSystem = "libSystem.dylib";

        private readonly int masterFd;
        private readonly Thread watcherThread;
        private int exitCode;
        private int exitSignal;
        private bool exited;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="NativePtyConnection"/> class
        /// by spawning a process inside a new PTY.
        /// </summary>
        /// <param name="app">Absolute path to the executable.</param>
        /// <param name="args">Command-line arguments (excluding argv[0]).</param>
        /// <param name="environment">Environment variables, or null to inherit.</param>
        /// <param name="cwd">Working directory, or null to inherit.</param>
        /// <param name="rows">Initial terminal rows.</param>
        /// <param name="cols">Initial terminal columns.</param>
        public NativePtyConnection(
            string app,
            string[] args,
            IDictionary<string, string> environment,
            string cwd,
            int rows,
            int cols)
        {
            IntPtr appPtr = IntPtr.Zero;
            IntPtr argvPtr = IntPtr.Zero;
            IntPtr envpPtr = IntPtr.Zero;
            IntPtr cwdPtr = IntPtr.Zero;
            var allocations = new List<IntPtr>();

            try
            {
                appPtr = MarshalString(app, allocations);
                argvPtr = MarshalArgv(app, args, allocations);
                envpPtr = environment != null
                    ? MarshalEnvironment(environment, allocations)
                    : IntPtr.Zero;
                cwdPtr = cwd != null ? MarshalString(cwd, allocations) : IntPtr.Zero;

                int masterFd = 0;
                int pid = NativeMethods.SpawnInPty(
                    appPtr,
                    argvPtr,
                    envpPtr,
                    cwdPtr,
                    (ushort)rows,
                    (ushort)cols,
                    ref masterFd);

                if (pid < 0)
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException(
                        $"spawn_in_pty failed for '{app}' (errno={err}).");
                }

                this.Pid = pid;
                this.masterFd = masterFd;
                this.ReaderStream = new FileStream(
                    new Microsoft.Win32.SafeHandles.SafeFileHandle((IntPtr)masterFd, ownsHandle: false),
                    FileAccess.Read,
                    bufferSize: 4096,
                    isAsync: false);
                this.WriterStream = new FileStream(
                    new Microsoft.Win32.SafeHandles.SafeFileHandle((IntPtr)masterFd, ownsHandle: false),
                    FileAccess.Write,
                    bufferSize: 0,
                    isAsync: false);

                this.watcherThread = new Thread(this.WatcherThreadProc)
                {
                    IsBackground = true,
                    Name = $"PTY watcher for pid {pid}",
                };
                this.watcherThread.Start();
            }
            finally
            {
                foreach (var ptr in allocations)
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
        }

        /// <summary>
        /// Occurs when the child process has exited.
        /// </summary>
        public event EventHandler ProcessExited;

        /// <summary>
        /// Gets the child process ID.
        /// </summary>
        public int Pid { get; }

        /// <summary>
        /// Gets the exit code of the child process.
        /// </summary>
        public int ExitCode => this.exitCode;

        /// <summary>
        /// Gets the signal that killed the child process, or 0 for a normal exit.
        /// </summary>
        public int ExitSignalNumber => this.exitSignal;

        /// <summary>
        /// Gets the reader stream (output from the PTY).
        /// </summary>
        public Stream ReaderStream { get; }

        /// <summary>
        /// Gets the writer stream (input to the PTY).
        /// </summary>
        public Stream WriterStream { get; }

        /// <summary>
        /// Returns true if the child has already exited.
        /// </summary>
        /// <param name="milliseconds">Time to wait (0 for non-blocking).</param>
        /// <returns>True if exited.</returns>
        public bool WaitForExit(int milliseconds)
        {
            if (this.exited)
            {
                return true;
            }

            if (milliseconds == 0)
            {
                return this.exited;
            }

            this.watcherThread.Join(milliseconds);
            return this.exited;
        }

        /// <summary>
        /// Resize the PTY terminal.
        /// </summary>
        /// <param name="cols">New column count.</param>
        /// <param name="rows">New row count.</param>
        public void Resize(int cols, int rows)
        {
            if (this.disposed)
            {
                return;
            }

            NativeMethods.SetWinSize(this.masterFd, (ushort)rows, (ushort)cols);
        }

        /// <summary>
        /// Send SIGHUP to the child process.
        /// </summary>
        public void Kill()
        {
            if (!this.exited)
            {
                NativeMethods.KillProcess(this.Pid, 1); // SIGHUP
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (!this.disposed)
            {
                this.disposed = true;
                this.Kill();

                try
                {
                    this.WriterStream?.Dispose();
                }
                catch (IOException)
                {
                    // May fail if slave side already closed.
                }

                try
                {
                    this.ReaderStream?.Dispose();
                }
                catch (IOException)
                {
                    // May fail if slave side already closed.
                }

                NativeMethods.Close(this.masterFd);
            }
        }

        private static IntPtr MarshalString(string value, List<IntPtr> allocations)
        {
            IntPtr ptr = Marshal.StringToHGlobalAnsi(value);
            allocations.Add(ptr);
            return ptr;
        }

        private static IntPtr MarshalArgv(string app, string[] args, List<IntPtr> allocations)
        {
            int count = 1 + (args?.Length ?? 0);
            IntPtr argv = Marshal.AllocHGlobal(IntPtr.Size * (count + 1));
            allocations.Add(argv);

            IntPtr appPtr = Marshal.StringToHGlobalAnsi(app);
            allocations.Add(appPtr);
            Marshal.WriteIntPtr(argv, 0, appPtr);

            for (int i = 0; i < (args?.Length ?? 0); i++)
            {
                IntPtr argPtr = Marshal.StringToHGlobalAnsi(args[i]);
                allocations.Add(argPtr);
                Marshal.WriteIntPtr(argv, IntPtr.Size * (i + 1), argPtr);
            }

            Marshal.WriteIntPtr(argv, IntPtr.Size * count, IntPtr.Zero);
            return argv;
        }

        private static IntPtr MarshalEnvironment(
            IDictionary<string, string> env, List<IntPtr> allocations)
        {
            IntPtr envp = Marshal.AllocHGlobal(IntPtr.Size * (env.Count + 1));
            allocations.Add(envp);

            int i = 0;
            foreach (var kv in env)
            {
                IntPtr entry = Marshal.StringToHGlobalAnsi(kv.Key + "=" + kv.Value);
                allocations.Add(entry);
                Marshal.WriteIntPtr(envp, IntPtr.Size * i, entry);
                i++;
            }

            Marshal.WriteIntPtr(envp, IntPtr.Size * i, IntPtr.Zero);
            return envp;
        }

        private void WatcherThreadProc()
        {
            // Block until the child exits using a blocking waitpid.
            int status = 0;
            int result = NativeMethods.WaitPidBlocking(this.Pid, ref status, 0);
            if (result > 0)
            {
                // WIFEXITED: (status & 0x7F) == 0
                int sig = status & 0x7F;
                if (sig == 0)
                {
                    this.exitCode = (status >> 8) & 0xFF;
                    this.exitSignal = 0;
                }
                else
                {
                    this.exitCode = 0;
                    this.exitSignal = sig;
                }
            }

            this.exited = true;
            this.ProcessExited?.Invoke(this, EventArgs.Empty);
        }

        private static class NativeMethods
        {
            [DllImport(LibName, EntryPoint = "spawn_in_pty", SetLastError = true)]
            public static extern int SpawnInPty(
                IntPtr app,
                IntPtr argv,
                IntPtr envp,
                IntPtr cwd,
                ushort rows,
                ushort cols,
                ref int masterFd);

            [DllImport(LibSystem, EntryPoint = "waitpid", SetLastError = true)]
            public static extern int WaitPidBlocking(int pid, ref int status, int options);

            [DllImport(LibSystem, EntryPoint = "kill")]
            public static extern int KillProcess(int pid, int sig);

            [DllImport(LibSystem, EntryPoint = "close")]
            public static extern int Close(int fd);

            public static void SetWinSize(int fd, ushort rows, ushort cols)
            {
                var ws = new WinSize { Rows = rows, Cols = cols };
                ulong tiocswinsz = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? 0x80087467UL
                    : 0x5414UL;
                Ioctl(fd, tiocswinsz, ref ws);
            }

            [DllImport(LibSystem, EntryPoint = "ioctl")]
            private static extern int Ioctl(int fd, ulong request, ref WinSize ws);

            [StructLayout(LayoutKind.Sequential)]
            public struct WinSize
            {
                public ushort Rows;
                public ushort Cols;
                public ushort XPixel;
                public ushort YPixel;
            }
        }
    }
}
