// <copyright file="Helpers.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Utilities
{
    using System;
    using Dotnvim.Settings;
    using SkiaSharp;

    /// <summary>
    /// Utility functions.
    /// </summary>
    public static class Helpers
    {
        /// <summary>
        /// Convert font Pt size to DIP size.
        /// </summary>
        /// <param name="pt">Font point.</param>
        /// <returns>DIP size.</returns>
        public static float GetFontSize(float pt)
        {
            return pt * 96 / 72;
        }

        /// <summary>
        /// Convert int-based color to Avalonia Color.
        /// </summary>
        /// <param name="color">Color in int.</param>
        /// <param name="alpha">Alpha (0-1).</param>
        /// <returns>Avalonia Color.</returns>
        public static Avalonia.Media.Color GetAvaloniaColor(int color, float alpha = 1)
        {
            byte b = (byte)(color % 256);
            color /= 256;
            byte g = (byte)(color % 256);
            color /= 256;
            byte r = (byte)(color % 256);

            return Avalonia.Media.Color.FromArgb((byte)(alpha * 255), r, g, b);
        }

        /// <summary>
        /// Convert int-based color to SKColor.
        /// </summary>
        /// <param name="color">Color in int.</param>
        /// <param name="alpha">Alpha (0-255).</param>
        /// <returns>SkiaSharp Color.</returns>
        public static SKColor GetSkColor(int color, byte alpha = 255)
        {
            byte b = (byte)(color % 256);
            color /= 256;
            byte g = (byte)(color % 256);
            color /= 256;
            byte r = (byte)(color % 256);

            return new SKColor(r, g, b, alpha);
        }

        /// <summary>
        /// Check whether we are running on a platform that supports blur effects.
        /// Available on Windows 10 and Windows 11 22H2+.
        /// Not available on Windows 11 21H2 (builds 22000-22620) where the old API is broken
        /// and the new DWM backdrop API is not yet available.
        /// </summary>
        /// <returns>Whether the blurbehind feature is available.</returns>
        public static bool BlurBehindAvailable()
        {
            var build = Environment.OSVersion.Version.Build;
            return Environment.OSVersion.Version.Major >= 10 && (build < 22000 || build >= 22621);
        }

        /// <summary>
        /// Check whether the blur behind is enabled.
        /// </summary>
        /// <returns>Whether the blur behind is enabled.</returns>
        public static bool BlurBehindEnabled()
        {
            return AppSettings.Default.EnableBlurBehind && BlurBehindAvailable();
        }

        /// <summary>
        /// Check whether transparent window background is available.
        /// </summary>
        /// <returns>Whether transparent background is available.</returns>
        public static bool TransparentAvailable()
        {
            return Environment.OSVersion.Version.Major >= 10;
        }

        /// <summary>
        /// Check whether any Avalonia window transparency level is available.
        /// </summary>
        /// <returns>Whether any window transparency level is available.</returns>
        public static bool WindowTransparencyAvailable()
        {
            return TransparentAvailable() || GaussianBlurAvailable() || AcrylicBlurAvailable() || MicaAvailable();
        }

        /// <summary>
        /// Check whether Gaussian blur is available (Windows 10 only, not Windows 11).
        /// </summary>
        /// <returns>Whether Gaussian blur is available.</returns>
        public static bool GaussianBlurAvailable()
        {
            var build = Environment.OSVersion.Version.Build;
            return Environment.OSVersion.Version.Major >= 10 && build < 22000;
        }

        /// <summary>
        /// Check whether we are running in Windows 10 RS4+ or Windows 11 22H2+.
        /// </summary>
        /// <returns>Whether the acrylic blur feature is available.</returns>
        public static bool AcrylicBlurAvailable()
        {
            var build = Environment.OSVersion.Version.Build;
            return (build >= 17134 && build < 22000) || build >= 22621;
        }

        /// <summary>
        /// Check whether Mica effect is available (Windows 11 22H2+).
        /// </summary>
        /// <returns>Whether Mica is available.</returns>
        public static bool MicaAvailable()
        {
            return Environment.OSVersion.Version.Build >= 22621;
        }

        /// <summary>
        /// Check whether the DWM system backdrop API is available (Windows 11 22H2+).
        /// </summary>
        /// <returns>Whether the DWM backdrop is available.</returns>
        public static bool Windows11BackdropAvailable()
        {
            return Environment.OSVersion.Version.Build >= 22621;
        }
    }
}
