// <copyright file="ColorUtility.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor.Utilities;

/// <summary>
/// Utilities for deriving readable foreground colors from a given background.
/// </summary>
public static class ColorUtility
{
    /// <summary>
    /// Soft white foreground used on dark backgrounds (avoids harsh pure-white glare).
    /// </summary>
    public const int SoftWhite = 0xF0F0F0;

    /// <summary>
    /// Soft black foreground used on light backgrounds (avoids harsh pure-black on pastels).
    /// </summary>
    public const int SoftBlack = 0x1A1A1A;

    /// <summary>
    /// Derives a readable, high-contrast foreground color from a background color.
    /// Uses WCAG relative luminance to determine whether the background is light
    /// or dark, then returns a complementary foreground that guarantees good readability.
    /// </summary>
    /// <param name="bgColor">Background color as an RGB integer: (R &lt;&lt; 16) | (G &lt;&lt; 8) | B.</param>
    /// <returns>A foreground color as an RGB integer that is readable against the given background.</returns>
    public static int DeriveReadableForeground(int bgColor)
    {
        double luminance = RelativeLuminance(bgColor);
        return luminance < 0.18 ? SoftWhite : SoftBlack;
    }

    /// <summary>
    /// Computes the WCAG 2.1 relative luminance of an RGB color.
    /// See https://www.w3.org/TR/WCAG21/#dfn-relative-luminance.
    /// </summary>
    /// <param name="color">Color as an RGB integer: (R &lt;&lt; 16) | (G &lt;&lt; 8) | B.</param>
    /// <returns>Relative luminance in the range [0, 1].</returns>
    public static double RelativeLuminance(int color)
    {
        double r = LinearizeSrgb(((color >> 16) & 0xFF) / 255.0);
        double g = LinearizeSrgb(((color >> 8) & 0xFF) / 255.0);
        double b = LinearizeSrgb((color & 0xFF) / 255.0);
        return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
    }

    /// <summary>
    /// Linearizes an sRGB channel value (applies inverse gamma).
    /// </summary>
    private static double LinearizeSrgb(double channel)
    {
        return channel <= 0.04045
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
    }
}
