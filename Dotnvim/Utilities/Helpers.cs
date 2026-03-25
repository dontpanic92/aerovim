// <copyright file="Helpers.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Utilities
{
    using System;
    using System.Drawing;
    using System.Numerics;
    using Vortice.Mathematics;
    using D2D = Vortice.Direct2D1;
    using Size = System.Drawing.Size;
    using SizeF = System.Drawing.SizeF;

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
        /// Convert DIP size to Pixel size.
        /// </summary>
        /// <param name="size">Size in Dip.</param>
        /// <param name="dpi">Dpi.</param>
        /// <returns>Size in Pixel.</returns>
        public static Size GetPixelSize(SizeF size, SizeF dpi)
        {
            return new Size(
                (int)Math.Round(dpi.Width * size.Width / 96),
                (int)Math.Round(dpi.Height * size.Height / 96));
        }

        /// <summary>
        /// Convert Pixel size to Dip size.
        /// </summary>
        /// <param name="size">Size in pixel.</param>
        /// <param name="dpi">Dpi.</param>
        /// <returns>Size in DIP.</returns>
        public static SizeF GetDipSize(Size size, SizeF dpi)
        {
            (var fx, var fy) = GetDipSize(size.Width, size.Height, dpi);
            return new SizeF(fx, fy);
        }

        /// <summary>
        /// Convert Pixel point to DIP point.
        /// </summary>
        /// <param name="x">x in pixel.</param>
        /// <param name="y">y in pixel.</param>
        /// <param name="dpi">Dpi.</param>
        /// <returns>Vector in Dip.</returns>
        public static Vector2 GetDipPoint(int x, int y, SizeF dpi)
        {
            (var fx, var fy) = GetDipSize(x, y, dpi);
            return new Vector2(fx, fy);
        }

        /// <summary>
        /// Convert Pixel size to Dip size.
        /// </summary>
        /// <param name="x">x in pixel.</param>
        /// <param name="y">y in pixel.</param>
        /// <param name="dpi">Dpi.</param>
        /// <returns>Size in DIP.</returns>
        public static (float x, float y) GetDipSize(int x, int y, SizeF dpi)
        {
            return (x * 96 / dpi.Width, y * 96 / dpi.Height);
        }

        /// <summary>
        /// Convert rectangle in DIP to Pixel.
        /// </summary>
        /// <param name="rect">rRct in DIP.</param>
        /// <param name="dpi">Dpi.</param>
        /// <returns>Rect in Pixel.</returns>
        public static Rectangle GetRawRectangle(Vortice.RawRectF rect, SizeF dpi)
        {
            int top = (int)Math.Round(dpi.Height * rect.Top / 96);
            int bottom = (int)Math.Round(dpi.Height * rect.Bottom / 96);
            int left = (int)Math.Round(dpi.Width * rect.Left / 96);
            int right = (int)Math.Round(dpi.Width * rect.Right / 96);
            return new Rectangle(left, top, right - left, bottom - top);
        }

        /// <summary>
        /// Round DIP to make it represents an integral pixels.
        /// </summary>
        /// <param name="dip">DIP.</param>
        /// <param name="dpi">dpi.</param>
        /// <returns>DIP aligned.</returns>
        public static float AlignToPixel(float dip, float dpi)
        {
            int pixel = (int)Math.Round(dip / 96 * dpi);
            return pixel * 96.0f / dpi;
        }

        /// <summary>
        /// Convert int-based color to RawColor.
        /// </summary>
        /// <param name="color">Color in int.</param>
        /// <param name="alpha">Alpha.</param>
        /// <returns>ShartDX RawColor.</returns>
        public static Color4 GetColor(int color, float alpha = 1)
        {
            float b = color % 256;
            color /= 256;
            float g = color % 256;
            color /= 256;
            float r = color % 256;

            return new Color4(r / 256, g / 256, b / 256, alpha);
        }

        /// <summary>
        /// Copy a rect of bitmap into a new one.
        /// </summary>
        /// <param name="renderTarget">The render target, e.g. device context.</param>
        /// <param name="bitmap">Original bitmap.</param>
        /// <param name="rect">The area to be copied.</param>
        /// <param name="dpi">Dpi.</param>
        /// <returns>The new copied bitmap.</returns>
        public static D2D.ID2D1Bitmap CopyBitmap(D2D.ID2D1RenderTarget renderTarget, D2D.ID2D1Bitmap bitmap, Vortice.RawRectF rect, SizeF dpi)
        {
            var bitmapProperties = new D2D.BitmapProperties(
                bitmap.PixelFormat,
                dpi.Width,
                dpi.Height);

            var pixelRect = GetRawRectangle(rect, dpi);
            var pixelSize = new SizeI(pixelRect.Width, pixelRect.Height);
            var newBitmap = renderTarget.CreateBitmap(pixelSize, IntPtr.Zero, 0, bitmapProperties);
            newBitmap.CopyFromBitmap(new Point(0, 0), bitmap, pixelRect);
            return newBitmap;
        }

        /// <summary>
        /// Check whether we are running on a platform that supports blur effects.
        /// Available on Windows 10 and Windows 11 22H2+.
        /// Not available on Windows 11 21H2 (builds 22000-22620) where the old API is broken
        /// and the new DWM backdrop API is not yet available.
        /// </summary>
        /// <returns>whether the blurbehind feature is available.</returns>
        public static bool BlurBehindAvailable()
        {
            var build = Environment.OSVersion.Version.Build;
            return Environment.OSVersion.Version.Major >= 10 && (build < 22000 || build >= 22621);
        }

        /// <summary>
        /// Check whether the blur behind is enabled.
        /// </summary>
        /// <returns>whether the blur behind is enabled.</returns>
        public static bool BlurBehindEnabled()
        {
            return Properties.Settings.Default.EnableBlurBehind && BlurBehindAvailable();
        }

        /// <summary>
        /// Gets the desktop DPI as SizeF for backward compatibility.
        /// </summary>
        /// <param name="factory">The D2D factory.</param>
        /// <returns>DPI as SizeF.</returns>
        public static SizeF GetDesktopDpi(this D2D.ID2D1Factory factory)
        {
            var dpi = factory.DesktopDpi;
            return new SizeF(dpi.X, dpi.Y);
        }

        /// <summary>
        /// Check whether Gaussian blur is available (Windows 10 only, not Windows 11).
        /// </summary>
        /// <returns>whether Gaussian blur is available.</returns>
        public static bool GaussianBlurAvailable()
        {
            var build = Environment.OSVersion.Version.Build;
            return Environment.OSVersion.Version.Major >= 10 && build < 22000;
        }

        /// <summary>
        /// Check whether we are running in Windows 10 RS4+ or Windows 11 22H2+.
        /// </summary>
        /// <returns>whether the acrylic blur feature is available.</returns>
        public static bool AcrylicBlurAvailable()
        {
            var build = Environment.OSVersion.Version.Build;
            return (build >= 17134 && build < 22000) || build >= 22621;
        }

        /// <summary>
        /// Check whether Mica effect is available (Windows 11 22H2+).
        /// </summary>
        /// <returns>whether Mica is available.</returns>
        public static bool MicaAvailable()
        {
            return Environment.OSVersion.Version.Build >= 22621;
        }

        /// <summary>
        /// Check whether the DWM system backdrop API is available (Windows 11 22H2+).
        /// </summary>
        /// <returns>whether the DWM backdrop is available.</returns>
        public static bool Windows11BackdropAvailable()
        {
            return Environment.OSVersion.Version.Build >= 22621;
        }
    }
}
