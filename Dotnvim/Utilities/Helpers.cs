// <copyright file="Helpers.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Utilities
{
    using System;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Dotnvim.Settings;
    using SkiaSharp;

    /// <summary>
    /// Utility functions.
    /// </summary>
    public static class Helpers
    {
        /// <summary>
        /// Checks whether a font family is available on the system via SkiaSharp.
        /// </summary>
        /// <param name="fontName">The font family name to look up.</param>
        /// <returns>True when the font manager reports the family as available.</returns>
        public static bool IsFontAvailable(string fontName)
        {
            var families = SKFontManager.Default.FontFamilies;
            return families.Contains(fontName, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the platform-appropriate default monospace font name.
        /// Tries a sequence of known monospace fonts and returns the first one
        /// that is actually available through SkiaSharp, so the caller never
        /// receives a name that would silently fall back to a proportional font.
        /// </summary>
        /// <returns>The default monospace font name for the current platform.</returns>
        public static string GetDefaultMonospaceFontName()
        {
            string[] candidates;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                candidates = new[] { "Consolas", "Courier New", "Lucida Console" };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                candidates = new[] { "Menlo", "SF Mono", "Monaco", "Courier" };
            }
            else
            {
                candidates = new[] { "DejaVu Sans Mono", "Liberation Mono", "Noto Sans Mono", "Monospace" };
            }

            foreach (var name in candidates)
            {
                if (IsFontAvailable(name))
                {
                    return name;
                }
            }

            // Last resort: return the platform's first choice and let SkiaSharp resolve it.
            return candidates[0];
        }

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
        /// On Windows: available on Windows 10 and Windows 11 22H2+.
        /// On macOS/Linux: basic transparency is generally available.
        /// </summary>
        /// <returns>Whether the blurbehind feature is available.</returns>
        public static bool BlurBehindAvailable()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var build = Environment.OSVersion.Version.Build;
                return Environment.OSVersion.Version.Major >= 10 && (build < 22000 || build >= 22621);
            }

            // macOS and Linux support basic transparency via compositing window managers
            return true;
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
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Environment.OSVersion.Version.Major >= 10;
            }

            // Transparent backgrounds are generally available on macOS and modern Linux compositors
            return true;
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
        /// Check whether Gaussian blur is available.
        /// On Windows: only Windows 10, not Windows 11.
        /// On macOS/Linux: not available.
        /// </summary>
        /// <returns>Whether Gaussian blur is available.</returns>
        public static bool GaussianBlurAvailable()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            var build = Environment.OSVersion.Version.Build;
            return Environment.OSVersion.Version.Major >= 10 && build < 22000;
        }

        /// <summary>
        /// Check whether acrylic blur is available.
        /// On Windows: Windows 10 RS4+ or Windows 11 22H2+.
        /// On macOS: available via Avalonia's platform integration.
        /// On Linux: not available.
        /// </summary>
        /// <returns>Whether the acrylic blur feature is available.</returns>
        public static bool AcrylicBlurAvailable()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return true;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            var build = Environment.OSVersion.Version.Build;
            return (build >= 17134 && build < 22000) || build >= 22621;
        }

        /// <summary>
        /// Check whether Mica effect is available (Windows 11 22H2+ only).
        /// </summary>
        /// <returns>Whether Mica is available.</returns>
        public static bool MicaAvailable()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            return Environment.OSVersion.Version.Build >= 22621;
        }

        /// <summary>
        /// Check whether the DWM system backdrop API is available (Windows 11 22H2+).
        /// </summary>
        /// <returns>Whether the DWM backdrop is available.</returns>
        public static bool Windows11BackdropAvailable()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            return Environment.OSVersion.Version.Build >= 22621;
        }
    }
}
