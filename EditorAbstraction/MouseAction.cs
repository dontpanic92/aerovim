// <copyright file="MouseAction.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor;

/// <summary>
/// Identifies the mouse action for input events.
/// </summary>
public enum MouseAction
{
    /// <summary>
    /// Button press.
    /// </summary>
    Press,

    /// <summary>
    /// Button release.
    /// </summary>
    Release,

    /// <summary>
    /// Mouse drag (move while button pressed).
    /// </summary>
    Drag,

    /// <summary>
    /// Mouse move (no button pressed).
    /// </summary>
    Move,

    /// <summary>
    /// Scroll up.
    /// </summary>
    ScrollUp,

    /// <summary>
    /// Scroll down.
    /// </summary>
    ScrollDown,

    /// <summary>
    /// Scroll left.
    /// </summary>
    ScrollLeft,

    /// <summary>
    /// Scroll right.
    /// </summary>
    ScrollRight,
}
