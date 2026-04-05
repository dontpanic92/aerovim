// <copyright file="ColorUtilityTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests;

using AeroVim.Editor.Utilities;
using NUnit.Framework;

/// <summary>
/// Tests for <see cref="ColorUtility"/>.
/// </summary>
public class ColorUtilityTests
{
    /// <summary>
    /// Pure black background should return soft white foreground.
    /// </summary>
    [Test]
    public void DeriveReadableForeground_BlackBackground_ReturnsSoftWhite()
    {
        Assert.That(ColorUtility.DeriveReadableForeground(0x000000), Is.EqualTo(ColorUtility.SoftWhite));
    }

    /// <summary>
    /// Pure white background should return soft black foreground.
    /// </summary>
    [Test]
    public void DeriveReadableForeground_WhiteBackground_ReturnsSoftBlack()
    {
        Assert.That(ColorUtility.DeriveReadableForeground(0xFFFFFF), Is.EqualTo(ColorUtility.SoftBlack));
    }

    /// <summary>
    /// Dark terminal background (e.g. typical dark theme) should return soft white.
    /// </summary>
    [Test]
    public void DeriveReadableForeground_DarkTerminalTheme_ReturnsSoftWhite()
    {
        // Typical dark themes: #1e1e2e, #282c34, #1a1b26
        Assert.That(ColorUtility.DeriveReadableForeground(0x1E1E2E), Is.EqualTo(ColorUtility.SoftWhite));
        Assert.That(ColorUtility.DeriveReadableForeground(0x282C34), Is.EqualTo(ColorUtility.SoftWhite));
        Assert.That(ColorUtility.DeriveReadableForeground(0x1A1B26), Is.EqualTo(ColorUtility.SoftWhite));
    }

    /// <summary>
    /// Light terminal background (e.g. typical light theme) should return soft black.
    /// </summary>
    [Test]
    public void DeriveReadableForeground_LightTerminalTheme_ReturnsSoftBlack()
    {
        // Typical light themes: #FAFAFA, #F5F5F5, #EEEEEE
        Assert.That(ColorUtility.DeriveReadableForeground(0xFAFAFA), Is.EqualTo(ColorUtility.SoftBlack));
        Assert.That(ColorUtility.DeriveReadableForeground(0xF5F5F5), Is.EqualTo(ColorUtility.SoftBlack));
        Assert.That(ColorUtility.DeriveReadableForeground(0xEEEEEE), Is.EqualTo(ColorUtility.SoftBlack));
    }

    /// <summary>
    /// Relative luminance of pure black should be 0.
    /// </summary>
    [Test]
    public void RelativeLuminance_Black_IsZero()
    {
        Assert.That(ColorUtility.RelativeLuminance(0x000000), Is.EqualTo(0.0).Within(0.0001));
    }

    /// <summary>
    /// Relative luminance of pure white should be 1.
    /// </summary>
    [Test]
    public void RelativeLuminance_White_IsOne()
    {
        Assert.That(ColorUtility.RelativeLuminance(0xFFFFFF), Is.EqualTo(1.0).Within(0.0001));
    }

    /// <summary>
    /// Relative luminance of mid-gray should be around 0.2140 (sRGB mid-gray, not 0.5).
    /// </summary>
    [Test]
    public void RelativeLuminance_MidGray_IsApproximatelyCorrect()
    {
        // sRGB #808080 has relative luminance ~0.2159
        double luminance = ColorUtility.RelativeLuminance(0x808080);
        Assert.That(luminance, Is.EqualTo(0.2159).Within(0.01));
    }

    /// <summary>
    /// Verify that the derived foreground always has good WCAG contrast against
    /// a range of typical background colors.
    /// </summary>
    /// <param name="bg">Background color as an RGB integer.</param>
    [TestCase(0x000000, Description = "Black")]
    [TestCase(0xFFFFFF, Description = "White")]
    [TestCase(0x1E1E2E, Description = "Catppuccin dark")]
    [TestCase(0x282C34, Description = "One Dark")]
    [TestCase(0xFAFAFA, Description = "Light theme")]
    [TestCase(0x2E3440, Description = "Nord dark")]
    [TestCase(0xECEFF4, Description = "Nord light")]
    [TestCase(0x1A1B26, Description = "Tokyo Night")]
    [TestCase(0x24283B, Description = "Tokyo Night Storm")]
    public void DeriveReadableForeground_AlwaysHasAdequateContrast(int bg)
    {
        int fg = ColorUtility.DeriveReadableForeground(bg);
        double bgLum = ColorUtility.RelativeLuminance(bg);
        double fgLum = ColorUtility.RelativeLuminance(fg);

        double lighter = Math.Max(bgLum, fgLum);
        double darker = Math.Min(bgLum, fgLum);
        double contrastRatio = (lighter + 0.05) / (darker + 0.05);

        // WCAG AA requires >= 4.5:1 for normal text
        Assert.That(contrastRatio, Is.GreaterThanOrEqualTo(4.5), $"Contrast ratio {contrastRatio:F2} for bg=0x{bg:X6} fg=0x{fg:X6}");
    }
}
