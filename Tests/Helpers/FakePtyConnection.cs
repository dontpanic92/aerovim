// <copyright file="FakePtyConnection.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests.Helpers;

using System.Text;
using AeroVim.VimClient;

/// <summary>
/// Test double for <see cref="IPtyConnection"/>.
/// </summary>
internal sealed class FakePtyConnection : IPtyConnection
{
    private readonly MemoryStream writerStream = new MemoryStream();

    /// <inheritdoc />
    public event EventHandler? ProcessExited;

    /// <inheritdoc />
    public int Pid { get; set; } = 1234;

    /// <inheritdoc />
    public int ExitCode { get; set; }

    /// <inheritdoc />
    public int ExitSignalNumber { get; set; }

    /// <inheritdoc />
    public Stream ReaderStream { get; } = Stream.Null;

    /// <inheritdoc />
    public Stream WriterStream => this.writerStream;

    /// <summary>
    /// Gets a value indicating whether the connection was disposed.
    /// </summary>
    public bool Disposed { get; private set; }

    /// <summary>
    /// Gets a value indicating whether <see cref="Kill"/> was called.
    /// </summary>
    public bool Killed { get; private set; }

    /// <summary>
    /// Gets the last resize request.
    /// </summary>
    public (int Cols, int Rows)? LastResize { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="WaitForExit"/> should report the process as exited.
    /// </summary>
    public bool HasExited { get; set; }

    /// <inheritdoc />
    public bool WaitForExit(int milliseconds)
    {
        return this.HasExited;
    }

    /// <inheritdoc />
    public void Resize(int cols, int rows)
    {
        this.LastResize = (cols, rows);
    }

    /// <inheritdoc />
    public void Kill()
    {
        this.Killed = true;
    }

    /// <summary>
    /// Gets the UTF-8 text written to the PTY.
    /// </summary>
    /// <returns>The written text.</returns>
    public string GetWrittenText()
    {
        return Encoding.UTF8.GetString(this.writerStream.ToArray());
    }

    /// <summary>
    /// Raises the process-exited event.
    /// </summary>
    public void RaiseProcessExited()
    {
        this.ProcessExited?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.Disposed = true;
        this.writerStream.Dispose();
    }
}
