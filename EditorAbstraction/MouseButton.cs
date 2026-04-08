// <copyright file="MouseButton.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor;

/// <summary>
/// Identifies the mouse button for input events.
/// </summary>
public enum MouseButton
{
    /// <summary>
    /// Left mouse button.
    /// </summary>
    Left,

    /// <summary>
    /// Middle mouse button.
    /// </summary>
    Middle,

    /// <summary>
    /// Right mouse button.
    /// </summary>
    Right,

    /// <summary>
    /// Mouse wheel (scroll).
    /// </summary>
    Wheel,

    /// <summary>
    /// Mouse movement with no button pressed.
    /// </summary>
    Move,
}
