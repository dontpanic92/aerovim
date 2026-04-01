// <copyright file="IPtyConnection.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.VimClient;

/// <summary>
/// Defines the minimal PTY surface needed by <see cref="VimClient"/>.
/// </summary>
internal interface IPtyConnection : IDisposable
{
    /// <summary>
    /// Occurs when the child process has exited.
    /// </summary>
    event EventHandler? ProcessExited;

    /// <summary>
    /// Gets the child process ID.
    /// </summary>
    int Pid { get; }

    /// <summary>
    /// Gets the child process exit code.
    /// </summary>
    int ExitCode { get; }

    /// <summary>
    /// Gets the signal that terminated the child process, or 0 when unavailable.
    /// </summary>
    int ExitSignalNumber { get; }

    /// <summary>
    /// Gets the PTY output stream.
    /// </summary>
    Stream ReaderStream { get; }

    /// <summary>
    /// Gets the PTY input stream.
    /// </summary>
    Stream WriterStream { get; }

    /// <summary>
    /// Waits for the child process to exit.
    /// </summary>
    /// <param name="milliseconds">The time to wait in milliseconds.</param>
    /// <returns><c>true</c> if the process has exited; otherwise, <c>false</c>.</returns>
    bool WaitForExit(int milliseconds);

    /// <summary>
    /// Resizes the PTY.
    /// </summary>
    /// <param name="cols">The new column count.</param>
    /// <param name="rows">The new row count.</param>
    void Resize(int cols, int rows);

    /// <summary>
    /// Terminates the child process.
    /// </summary>
    void Kill();
}
