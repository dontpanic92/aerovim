// <copyright file="AcrylicMaterialHelper.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Utilities;

using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Media;

/// <summary>
/// Shared helper for applying platform-specific <see cref="ExperimentalAcrylicMaterial"/>
/// to overlay controls (cmdline popup, completion menu, etc.).
/// </summary>
internal static class AcrylicMaterialHelper
{
    /// <summary>
    /// Applies a default platform-specific acrylic material to the given border.
    /// </summary>
    /// <param name="border">The acrylic border to configure.</param>
    public static void ApplyPlatformDefaults(ExperimentalAcrylicBorder border)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SetMaterial(border, Color.FromRgb(0x1E, 0x1E, 0x1E), tintOpacity: 0.85, materialOpacity: 0.75);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            SetMaterial(border, Color.FromRgb(0x2D, 0x2D, 0x2D), tintOpacity: 0.70, materialOpacity: 0.65);
        }
        else
        {
            SetMaterial(border, Color.FromRgb(0x1E, 0x1E, 0x1E), tintOpacity: 0.92, materialOpacity: 0.92);
        }
    }

    /// <summary>
    /// Updates the acrylic tint color to match the editor's current background,
    /// applying platform-specific opacity values.
    /// </summary>
    /// <param name="border">The acrylic border to update.</param>
    /// <param name="bgColor">The editor background color.</param>
    public static void UpdateTint(ExperimentalAcrylicBorder border, Color bgColor)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            double luma = (0.299 * bgColor.R) + (0.587 * bgColor.G) + (0.114 * bgColor.B);
            bool isDark = luma < 128;
            var tint = isDark
                ? Color.FromRgb(
                    (byte)Math.Min(bgColor.R + 20, 255),
                    (byte)Math.Min(bgColor.G + 20, 255),
                    (byte)Math.Min(bgColor.B + 20, 255))
                : Color.FromRgb(
                    (byte)Math.Max(bgColor.R - 20, 0),
                    (byte)Math.Max(bgColor.G - 20, 0),
                    (byte)Math.Max(bgColor.B - 20, 0));
            SetMaterial(border, tint, tintOpacity: 0.70, materialOpacity: 0.65);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SetMaterial(border, bgColor, tintOpacity: 0.85, materialOpacity: 0.75);
        }
        else
        {
            SetMaterial(border, bgColor, tintOpacity: 0.92, materialOpacity: 0.92);
        }
    }

    private static void SetMaterial(ExperimentalAcrylicBorder border, Color tintColor, double tintOpacity, double materialOpacity)
    {
        border.Material = new ExperimentalAcrylicMaterial
        {
            BackgroundSource = AcrylicBackgroundSource.Digger,
            TintColor = tintColor,
            TintOpacity = tintOpacity,
            MaterialOpacity = materialOpacity,
        };
    }
}
