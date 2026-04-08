// <copyright file="IExternalCmdline.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor.Capabilities;

/// <summary>
/// Capability interface for backends that support an externalized command
/// line (e.g. Neovim's <c>ext_cmdline</c>).
/// </summary>
public interface IExternalCmdline
{
    /// <summary>
    /// Gets the externalized command line state, or <c>null</c> if the
    /// command line is not currently active.
    /// </summary>
    CmdlineState? Cmdline { get; }
}
