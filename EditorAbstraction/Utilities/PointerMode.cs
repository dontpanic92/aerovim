// <copyright file="PointerMode.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor.Utilities;

/// <summary>
/// Specifies the pointer auto-hide behavior using terminal-style semantics.
/// </summary>
public enum PointerMode
{
    /// <summary>
    /// Never hide the pointer.
    /// </summary>
    NeverHide = 0,

    /// <summary>
    /// Hide the pointer when mouse tracking is disabled.
    /// </summary>
    HideWhenTrackingDisabled = 1,

    /// <summary>
    /// Always hide the pointer.
    /// </summary>
    AlwaysHide = 2,

    /// <summary>
    /// Always hide the pointer, even on mouse leave events.
    /// </summary>
    AlwaysHideOnLeave = 3,
}
