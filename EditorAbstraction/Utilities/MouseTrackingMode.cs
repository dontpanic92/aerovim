// <copyright file="MouseTrackingMode.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor.Utilities;

/// <summary>
/// Describes the current terminal mouse tracking mode.
/// </summary>
public enum MouseTrackingMode
{
    /// <summary>
    /// Mouse reporting is disabled.
    /// </summary>
    None,

    /// <summary>
    /// Button press/release events are reported.
    /// </summary>
    Normal,

    /// <summary>
    /// Button events and drag motion are reported.
    /// </summary>
    ButtonEvent,

    /// <summary>
    /// All mouse motion is reported, even with no button pressed.
    /// </summary>
    AnyEvent,
}
