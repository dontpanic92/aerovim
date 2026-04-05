// <copyright file="EditorControlTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests;

using AeroVim.Controls;
using AeroVim.Editor;
using AeroVim.Editor.Utilities;
using AeroVim.Tests.Helpers;
using Avalonia;
using Avalonia.Input;
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
    /// Mouse presses should not be forwarded when the backend disables mouse support.
    /// </summary>
    [Test]
    public void HandlePointerPressedForTesting_WhenMouseDisabled_DoesNotForwardInput()
    {
        var editorClient = new TestEditorClient
        {
            MouseEnabled = false,
        };

        using var control = new EditorControl(editorClient);
        bool handled = control.HandlePointerPressedForTesting("left", 1, 2);

        Assert.That(handled, Is.False);
        Assert.That(editorClient.MouseCalls, Is.Empty);
    }

    /// <summary>
    /// Disabling mouse support mid-drag should clear the pending pressed button state.
    /// </summary>
    [Test]
    public void RefreshEditorUiStateForTesting_WhenMouseIsDisabledMidDrag_ClearsPendingRelease()
    {
        var editorClient = new TestEditorClient
        {
            MouseEnabled = true,
        };

        using var control = new EditorControl(editorClient);
        Assert.That(control.HandlePointerPressedForTesting("left", 1, 2), Is.True);

        editorClient.MouseEnabled = false;
        control.RefreshEditorUiStateForTesting();

        Assert.That(control.HandlePointerMovedForTesting(1, 3), Is.False);

        editorClient.MouseEnabled = true;
        control.RefreshEditorUiStateForTesting();

        Assert.That(control.HandlePointerReleasedForTesting(1, 3), Is.False);
        Assert.That(editorClient.MouseCalls, Has.Count.EqualTo(1));
        Assert.That(editorClient.MouseCalls[0], Is.EqualTo(("left", "press", string.Empty, 0, 1, 2)));
    }

    /// <summary>
    /// Wheel input should not be forwarded when mouse support is disabled.
    /// </summary>
    [Test]
    public void HandlePointerWheelForTesting_WhenMouseDisabled_DoesNotForwardInput()
    {
        var editorClient = new TestEditorClient
        {
            MouseEnabled = false,
        };

        using var control = new EditorControl(editorClient);
        bool handled = control.HandlePointerWheelForTesting(0, 0, new Vector(0, 1));

        Assert.That(handled, Is.False);
        Assert.That(editorClient.MouseCalls, Is.Empty);
    }

    /// <summary>
    /// Pointer-shape hints should resolve to the matching Avalonia cursor.
    /// </summary>
    [Test]
    public void RefreshEditorUiStateForTesting_WithBeamPointerShape_ResolvesIbeamCursor()
    {
        var editorClient = new TestEditorClient
        {
            ModeInfo = new ModeInfo(CursorShape.Block, 100, CursorBlinking.BlinkOff, pointerShape: "beam"),
        };

        using var control = new EditorControl(editorClient);
        control.RefreshEditorUiStateForTesting();

        Assert.That(control.ResolvedPointerCursorType, Is.EqualTo(StandardCursorType.Ibeam));
    }

    /// <summary>
    /// Pointer auto-hide should resolve to a hidden cursor when requested.
    /// </summary>
    [Test]
    public void RefreshEditorUiStateForTesting_WhenPointerModeHidesPointer_ResolvesNoneCursor()
    {
        var editorClient = new TestEditorClient
        {
            MouseEnabled = false,
            ModeInfo = new ModeInfo(CursorShape.Block, 100, CursorBlinking.BlinkOff, pointerShape: "beam", pointerMode: 1),
        };

        using var control = new EditorControl(editorClient);
        control.RefreshEditorUiStateForTesting();

        Assert.That(control.ResolvedPointerCursorType, Is.EqualTo(StandardCursorType.None));
    }

    /// <summary>
    /// Cursor visibility hints should change the rendered output.
    /// </summary>
    [Test]
    public void RenderForTesting_WithCursorHidden_SuppressesCursorOutput()
    {
        var editorClient = new TestEditorClient
        {
            CurrentScreen = CreateCursorScreen(),
            ModeInfo = new ModeInfo(CursorShape.Block, 100, CursorBlinking.BlinkOff),
        };

        using var control = new EditorControl(editorClient);
        byte[] visible = RenderToPng(control, 240, 120);

        editorClient.ModeInfo = new ModeInfo(CursorShape.Block, 100, CursorBlinking.BlinkOff, cursorVisible: false);
        byte[] hidden = RenderToPng(control, 240, 120);

        Assert.That(hidden, Is.Not.EqualTo(visible));
    }

    /// <summary>
    /// Disabling cursor styling should fall back to the default block cursor.
    /// </summary>
    [Test]
    public void RenderForTesting_WithCursorStyleDisabled_UsesDefaultBlockCursor()
    {
        var screen = CreateCursorScreen();
        byte[] styleDisabled = RenderCursorScreen(
            screen,
            new ModeInfo(CursorShape.Vertical, 25, CursorBlinking.BlinkOff, cursorStyleEnabled: false));
        byte[] defaultBlock = RenderCursorScreen(
            screen,
            new ModeInfo(CursorShape.Block, 100, CursorBlinking.BlinkOff));

        Assert.That(styleDisabled, Is.EqualTo(defaultBlock));
    }

    /// <summary>
    /// Cursor blink state should affect whether the cursor is drawn.
    /// </summary>
    [Test]
    public void RenderForTesting_WithBlinkingCursorHiddenState_SuppressesCursorOutput()
    {
        var editorClient = new TestEditorClient
        {
            CurrentScreen = CreateCursorScreen(),
            ModeInfo = new ModeInfo(CursorShape.Block, 100, CursorBlinking.BlinkOn),
        };

        using var control = new EditorControl(editorClient);
        byte[] visible = RenderToPng(control, 240, 120);

        control.SetCursorBlinkVisibleForTesting(false);
        byte[] hidden = RenderToPng(control, 240, 120);

        Assert.That(hidden, Is.Not.EqualTo(visible));
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
    /// Bold output should stay visually closer to plain bold output than to ligature normal output.
    /// </summary>
    [Test]
    public void RenderForTesting_WithLigaturesEnabled_BoldTextRetainsPlainBoldWeight()
    {
        Assert.That(TryFindLigatureFixture(out var fixture), Is.True, "No ligature-capable font fixture was found.");

        var normalScreen = CreateAsciiScreen(fixture.Text);
        var boldScreen = CreateAsciiScreen(fixture.Text, bold: true);

        long plainBoldInk = MeasureInk(RenderScreen(fixture.FontName, boldScreen, false));
        long ligatureNormalInk = MeasureInk(RenderScreen(fixture.FontName, normalScreen, true));
        long ligatureBoldInk = MeasureInk(RenderScreen(fixture.FontName, boldScreen, true));

        Assert.That(
            Math.Abs(ligatureBoldInk - plainBoldInk),
            Is.LessThan(Math.Abs(ligatureNormalInk - plainBoldInk)));
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

    private static Screen CreateCursorScreen()
    {
        var screen = TestScreenBuilder.CreateScreen(1, 2);
        TestScreenBuilder.SetCell(screen, 0, 0, "A", 0x000000, 0xFFFFFF);
        screen.CursorPosition = (0, 0);
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

    private static byte[] RenderCursorScreen(Screen screen, ModeInfo modeInfo)
    {
        var editorClient = new TestEditorClient
        {
            CurrentScreen = screen,
            ModeInfo = modeInfo,
        };

        using var control = new EditorControl(editorClient);
        return RenderToPng(control, 240, 120);
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

    private static long MeasureInk(byte[] pngData)
    {
        using var bitmap = SKBitmap.Decode(pngData);
        long ink = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                ink += byte.MaxValue - pixel.Red;
            }
        }

        return ink;
    }

    private readonly record struct LigatureFixture(string FontName, string Text);
}
