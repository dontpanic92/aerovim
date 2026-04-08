// <copyright file="ITerminalCapabilities.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor.Capabilities;

/// <summary>
/// Capability interface for backends that communicate via a terminal PTY
/// and support terminal-specific features (e.g. the Vim backend).
/// </summary>
public interface ITerminalCapabilities
{
    /// <summary>
    /// Gets a value indicating whether bracketed paste mode is enabled.
    /// When true, the frontend should wrap pasted text in
    /// <c>ESC[200~</c> … <c>ESC[201~</c>.
    /// </summary>
    bool BracketedPasteEnabled { get; }

    /// <summary>
    /// Gets a value indicating whether focus event reporting is enabled.
    /// When true, the frontend should send <c>ESC[I</c> on focus-in and
    /// <c>ESC[O</c> on focus-out.
    /// </summary>
    bool FocusEventsEnabled { get; }

    /// <summary>
    /// Gets a value indicating whether synchronized output mode is active.
    /// When true, the frontend should defer rendering until the mode is
    /// cleared.
    /// </summary>
    bool SynchronizedOutput { get; }
}
