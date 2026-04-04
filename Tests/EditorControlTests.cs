// <copyright file="EditorControlTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests;

using AeroVim.Controls;
using AeroVim.Editor;
using AeroVim.Editor.Utilities;
using AeroVim.Tests.Helpers;
using NUnit.Framework;
using SkiaSharp;

/// <summary>
/// Tests editor rendering behavior with off-screen surfaces.
/// </summary>
public class EditorControlTests
{
    private const float LigatureTestFontSize = 20f;
    private static readonly string[] LigatureFontCandidates =
    [
        "Cascadia Code",
        "Cascadia Mono",
        "Fira Code",
        "JetBrains Mono",
        "Iosevka",
        "Hasklig",
        "Times New Roman",
        "Times",
        "Georgia",
        "Cambria",
        "DejaVu Serif",
        "Liberation Serif",
        "Noto Serif",
        "Palatino Linotype",
        "Constantia",
        "Candara",
        "Corbel",
    ];

    private static readonly string[] LigatureSamples =
    [
        "ffi",
        "ff",
        "fi",
        "fl",
        "->",
        "=>",
        "!=",
        "===",
    ];

    /// <summary>
    /// Rendering should reflect updated screen contents.
    /// </summary>
    [Test]
    public void RenderForTesting_ReflectsScreenContentChanges()
    {
        var editorClient = new TestEditorClient();
        var screenA = TestScreenBuilder.CreateScreen(2, 4);
        TestScreenBuilder.SetCell(screenA, 0, 0, "A", 0x000000, 0xFFFFFF);
        screenA.CursorPosition = (1, 1);
        editorClient.CurrentScreen = screenA;

        var control = new EditorControl(editorClient);
        byte[] firstRender = RenderToPng(control, 240, 120);

        var screenB = TestScreenBuilder.CreateScreen(2, 4);
        TestScreenBuilder.SetCell(screenB, 0, 0, "B", 0x000000, 0xFFFFFF);
        screenB.CursorPosition = (1, 1);
        editorClient.CurrentScreen = screenB;
        byte[] secondRender = RenderToPng(control, 240, 120);

        Assert.That(secondRender, Is.Not.EqualTo(firstRender));
    }

    /// <summary>
    /// Rendering with wide cells and IME preedit text should remain stable and change the output.
    /// </summary>
    [Test]
    public void RenderForTesting_WithWideCharacterAndPreedit_ChangesOutput()
    {
        var editorClient = new TestEditorClient();
        var screen = TestScreenBuilder.CreateScreen(1, 4);
        TestScreenBuilder.SetCell(screen, 0, 0, "中", 0x202020, 0xFFFFFF);
        TestScreenBuilder.SetCell(screen, 0, 1, null, 0x202020, 0xFFFFFF);
        screen.CursorPosition = (0, 2);
        editorClient.CurrentScreen = screen;

        var control = new EditorControl(editorClient);
        byte[] withoutPreedit = RenderToPng(control, 240, 120);

        control.SetPreeditTextForTesting("x", 0);
        byte[] withPreedit = RenderToPng(control, 240, 120);

        Assert.That(withPreedit, Is.Not.EqualTo(withoutPreedit));
    }

    /// <summary>
    /// HarfBuzz shaping should change the glyph sequence for a ligature-capable sample on an installed font.
    /// </summary>
    [Test]
    public void LigatureTextShaper_WithLigatureCapableFont_ChangesGlyphSequence()
    {
        Assert.That(TryFindLigatureFixture(out var fixture), Is.True, "No ligature-capable font fixture was found.");

        using var typeface = CreateValidatedTypeface(fixture.FontName);
        Assert.That(typeface, Is.Not.Null);

        using var shaper = new LigatureTextShaper();
        var shapedRun = shaper.ShapeText(typeface!, LigatureTestFontSize, fixture.Text);
        ushort[] unshapedGlyphs = GetUnshapedGlyphs(typeface!, fixture.Text);

        Assert.That(shapedRun, Is.Not.Null);
        Assert.That(shapedRun!.GlyphIds.SequenceEqual(unshapedGlyphs), Is.False);
    }

    /// <summary>
    /// Enabling ligatures should change rendered output for a ligature-capable font sample.
    /// </summary>
    [Test]
    public void RenderForTesting_WithLigaturesEnabled_ChangesOutput()
    {
        Assert.That(TryFindLigatureFixture(out var fixture), Is.True, "No ligature-capable font fixture was found.");

        var screen = CreateAsciiScreen(fixture.Text);
        byte[] withoutLigatures = RenderScreen(fixture.FontName, screen, false);
        byte[] withLigatures = RenderScreen(fixture.FontName, screen, true);

        Assert.That(withLigatures, Is.Not.EqualTo(withoutLigatures));
    }

    /// <summary>
    /// Bold text should still render differently when ligatures are enabled.
    /// </summary>
    [Test]
    public void RenderForTesting_WithLigaturesEnabled_BoldTextChangesOutput()
    {
        Assert.That(TryFindLigatureFixture(out var fixture), Is.True, "No ligature-capable font fixture was found.");

        byte[] normal = RenderScreen(fixture.FontName, CreateAsciiScreen(fixture.Text), true);
        byte[] bold = RenderScreen(fixture.FontName, CreateAsciiScreen(fixture.Text, bold: true), true);

        Assert.That(bold, Is.Not.EqualTo(normal));
    }

    /// <summary>
    /// Invisible style-run splits should not change glyph positions in ligature mode.
    /// </summary>
    [Test]
    public void RenderForTesting_WithInvisibleStyleSplit_DoesNotShiftLigatureText()
    {
        Assert.That(TryFindLigatureFixture(out var fixture), Is.True, "No ligature-capable font fixture was found.");

        string text = fixture.Text + "a(";
        var baselineScreen = CreateAsciiScreen(text);
        var splitScreen = CreateAsciiScreen(text);
        int splitIndex = fixture.Text.Length;
        TestScreenBuilder.SetCell(splitScreen, 0, splitIndex, text[splitIndex].ToString(), 0x000000, 0xFFFFFF, special: 0x123456);

        byte[] baseline = RenderScreen(fixture.FontName, baselineScreen, true);
        byte[] withInvisibleSplit = RenderScreen(fixture.FontName, splitScreen, true);

        Assert.That(withInvisibleSplit, Is.EqualTo(baseline));
    }

    private static Screen CreateAsciiScreen(string text, bool bold = false)
    {
        var screen = TestScreenBuilder.CreateScreen(1, text.Length + 2);
        for (int i = 0; i < text.Length; i++)
        {
            TestScreenBuilder.SetCell(screen, 0, i, text[i].ToString(), 0x000000, 0xFFFFFF, bold: bold);
        }

        screen.CursorPosition = (0, text.Length + 1);
        return screen;
    }

    private static SKTypeface? CreateValidatedTypeface(string fontName)
    {
        var typeface = SKTypeface.FromFamilyName(fontName);
        if (typeface is not null
            && string.Equals(typeface.FamilyName, fontName, StringComparison.OrdinalIgnoreCase))
        {
            return typeface;
        }

        typeface?.Dispose();
        return null;
    }

    private static bool TryFindLigatureFixture(out LigatureFixture fixture)
    {
        using var shaper = new LigatureTextShaper();
        foreach (var fontName in LigatureFontCandidates)
        {
            using var typeface = CreateValidatedTypeface(fontName);
            if (typeface is null)
            {
                continue;
            }

            foreach (var sample in LigatureSamples)
            {
                var shapedRun = shaper.ShapeText(typeface, LigatureTestFontSize, sample);
                if (shapedRun is not null && !shapedRun.GlyphIds.SequenceEqual(GetUnshapedGlyphs(typeface, sample)))
                {
                    fixture = new LigatureFixture(fontName, sample);
                    return true;
                }
            }
        }

        fixture = default;
        return false;
    }

    private static ushort[] GetUnshapedGlyphs(SKTypeface typeface, string text)
    {
        using var font = new SKFont(typeface, LigatureTestFontSize, 1f, 0f);
        ushort[] glyphs = new ushort[font.CountGlyphs(text)];
        font.GetGlyphs(text, glyphs);
        return glyphs;
    }

    private static byte[] RenderScreen(string fontName, Screen screen, bool enableLigature)
    {
        var editorClient = new TestEditorClient
        {
            CurrentScreen = screen,
        };

        using var control = new EditorControl(editorClient);
        editorClient.RaiseFontChanged(new FontSettings
        {
            FontNames = new List<string> { fontName },
            FontPointSize = LigatureTestFontSize,
        });

        control.EnableLigature = enableLigature;
        return RenderToPng(control, 480, 120);
    }

    private static byte[] RenderToPng(EditorControl control, int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        surface.Canvas.Clear(SKColors.Transparent);
        control.RenderForTesting(surface.Canvas);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private readonly record struct LigatureFixture(string FontName, string Text);
}
