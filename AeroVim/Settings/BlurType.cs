// <copyright file="BlurType.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Settings;

/// <summary>
/// Specifies the window transparency blur effect type.
/// </summary>
public enum BlurType
{
    /// <summary>
    /// Gaussian blur (Windows 10 only).
    /// </summary>
    Gaussian = 0,

    /// <summary>
    /// Acrylic blur effect.
    /// </summary>
    Acrylic = 1,

    /// <summary>
    /// Mica effect (Windows 11 22H2+).
    /// </summary>
    Mica = 2,

    /// <summary>
    /// Plain transparent background without blur.
    /// </summary>
    Transparent = 3,
}
