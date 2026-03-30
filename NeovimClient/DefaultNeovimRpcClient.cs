// <copyright file="DefaultNeovimRpcClient.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient;

/// <summary>
/// The default neovim client.
/// </summary>
public class DefaultNeovimRpcClient : NeovimRpcClient<Events.IRedrawEvent>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultNeovimRpcClient"/> class.
    /// </summary>
    /// <param name="path">The path to neovim.</param>
    public DefaultNeovimRpcClient(string path)
        : base(path, new DefaultRedrawEventFactory())
    {
    }
}
