// <copyright file="TextLayoutParameters.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Controls;

using SkiaSharp;

/// <summary>
/// Font metrics for the terminal grid.
/// </summary>
internal sealed class TextLayoutParameters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TextLayoutParameters"/> class.
    /// </summary>
    /// <param name="fontName">The primary font family name.</param>
    /// <param name="pointSize">The font size in points.</param>
    public TextLayoutParameters(string fontName, float pointSize)
    {
        this.FontName = fontName;
        this.PointSize = pointSize;
        this.SkiaFontSize = pointSize * 96f / 72f;

        using var typeface = SKTypeface.FromFamilyName(fontName);
        using var paint = new SKPaint();
        paint.Typeface = typeface;
        paint.TextSize = this.SkiaFontSize;
        paint.IsAntialias = true;

        var metrics = paint.FontMetrics;
        this.LineHeight = (float)Math.Ceiling(-metrics.Ascent + metrics.Descent + metrics.Leading);
        this.CharWidth = paint.MeasureText("A");

        // Round to pixels to prevent sub-pixel artifacts in the grid
        this.LineHeight = (float)Math.Ceiling(this.LineHeight);
        this.CharWidth = (float)Math.Ceiling(this.CharWidth);
    }

    /// <summary>
    /// Gets the primary font family name.
    /// </summary>
    public string FontName { get; }

    /// <summary>
    /// Gets the font size in points.
    /// </summary>
    public float PointSize { get; }

    /// <summary>
    /// Gets the font size in Skia units (pixels at 96 DPI).
    /// </summary>
    public float SkiaFontSize { get; }

    /// <summary>
    /// Gets the line height in pixels.
    /// </summary>
    public float LineHeight { get; }

    /// <summary>
    /// Gets the character width in pixels.
    /// </summary>
    public float CharWidth { get; }
}
