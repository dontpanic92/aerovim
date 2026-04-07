// <copyright file="DefaultNeovimRpcClient.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient;

using AeroVim.Editor.Diagnostics;

/// <summary>
/// The default neovim client.
/// </summary>
public class DefaultNeovimRpcClient : NeovimRpcClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultNeovimRpcClient"/> class.
    /// </summary>
    /// <param name="path">The path to neovim.</param>
    /// <param name="logger">Application logger.</param>
    /// <param name="workingDirectory">Optional working directory for Neovim.</param>
    /// <param name="fileArgs">Optional file paths to open on startup.</param>
    public DefaultNeovimRpcClient(string path, IAppLogger logger, string? workingDirectory = null, IReadOnlyList<string>? fileArgs = null)
        : base(path, logger, workingDirectory, fileArgs)
    {
    }
}
