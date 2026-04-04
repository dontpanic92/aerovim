// <copyright file="DuplexStreamPair.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests.Helpers;

using System.IO.Pipelines;

/// <summary>
/// Provides a connected pair of full-duplex streams for in-memory protocol tests.
/// </summary>
internal sealed class DuplexStreamPair : IDisposable
{
    private readonly Pipe clientToServer = new Pipe();
    private readonly Pipe serverToClient = new Pipe();

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplexStreamPair"/> class.
    /// </summary>
    public DuplexStreamPair()
    {
        this.ClientWriter = this.clientToServer.Writer.AsStream();
        this.ClientReader = this.serverToClient.Reader.AsStream();
        this.ServerWriter = this.serverToClient.Writer.AsStream();
        this.ServerReader = this.clientToServer.Reader.AsStream();
    }

    /// <summary>
    /// Gets the writer used by the client side.
    /// </summary>
    public Stream ClientWriter { get; }

    /// <summary>
    /// Gets the reader used by the client side.
    /// </summary>
    public Stream ClientReader { get; }

    /// <summary>
    /// Gets the writer used by the server side.
    /// </summary>
    public Stream ServerWriter { get; }

    /// <summary>
    /// Gets the reader used by the server side.
    /// </summary>
    public Stream ServerReader { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        this.ClientWriter.Dispose();
        this.ClientReader.Dispose();
        this.ServerWriter.Dispose();
        this.ServerReader.Dispose();
    }
}
