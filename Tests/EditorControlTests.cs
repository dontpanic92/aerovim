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
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using NUnit.Framework;
using SkiaSharp;
using MouseButton = AeroVim.Editor.MouseButton;

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
    [AvaloniaTest]
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
    [AvaloniaTest]
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
    [AvaloniaTest]
    public void HandlePointerPressedForTesting_WhenMouseDisabled_DoesNotForwardInput()
    {
        var editorClient = new TestEditorClient
        {
            MouseEnabled = false,
        };

        using var control = new EditorControl(editorClient);
        bool handled = control.HandlePointerPressedForTesting(MouseButton.Left, 1, 2);

        Assert.That(handled, Is.False);
        Assert.That(editorClient.MouseCalls, Is.Empty);
    }

    /// <summary>
    /// Disabling mouse support mid-drag should clear the pending pressed button state.
    /// </summary>
    [AvaloniaTest]
    public void RefreshEditorUiStateForTesting_WhenMouseIsDisabledMidDrag_ClearsPendingRelease()
    {
        var editorClient = new TestEditorClient
        {
            MouseEnabled = true,
        };

        using var control = new EditorControl(editorClient);
        Assert.That(control.HandlePointerPressedForTesting(MouseButton.Left, 1, 2), Is.True);

        editorClient.MouseEnabled = false;
        control.RefreshEditorUiStateForTesting();

        Assert.That(control.HandlePointerMovedForTesting(1, 3), Is.False);

        editorClient.MouseEnabled = true;
        control.RefreshEditorUiStateForTesting();

        Assert.That(control.HandlePointerReleasedForTesting(1, 3), Is.False);
        Assert.That(editorClient.MouseCalls, Has.Count.EqualTo(1));
        Assert.That(editorClient.MouseCalls[0], Is.EqualTo((MouseButton.Left, MouseAction.Press, string.Empty, 0, 1, 2)));
    }

    /// <summary>
    /// Wheel input should not be forwarded when mouse support is disabled.
    /// </summary>
    [AvaloniaTest]
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
    [AvaloniaTest]
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
    [AvaloniaTest]
    public void RefreshEditorUiStateForTesting_WhenPointerModeHidesPointer_ResolvesNoneCursor()
    {
        var editorClient = new TestEditorClient
        {
            MouseEnabled = false,
            ModeInfo = new ModeInfo(CursorShape.Block, 100, CursorBlinking.BlinkOff, pointerShape: "beam", pointerMode: PointerMode.HideWhenTrackingDisabled),
        };

        using var control = new EditorControl(editorClient);
        control.RefreshEditorUiStateForTesting();

        Assert.That(control.ResolvedPointerCursorType, Is.EqualTo(StandardCursorType.None));
    }

    /// <summary>
    /// Cursor visibility hints should change the rendered output.
    /// </summary>
    [AvaloniaTest]
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
    [AvaloniaTest]
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
    [AvaloniaTest]
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
    [AvaloniaTest]
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
    [AvaloniaTest]
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
    [AvaloniaTest]
    public void RenderForTesting_WithLigaturesEnabled_BoldTextChangesOutput()
    {
        Assert.That(TryFindLigatureFixture(out var fixture), Is.True, "No ligature-capable font fixture was found.");

        byte[] normal = RenderScreen(fixture.FontName, CreateAsciiScreen(fixture.Text), true);
        byte[] bold = RenderScreen(fixture.FontName, CreateAsciiScreen(fixture.Text, bold: true), true);

        Assert.That(bold, Is.Not.EqualTo(normal));
    }

    /// <summary>
    /// Ligature rendering should produce measurably different ink coverage than
    /// plain rendering for the same text, confirming the shaping path is active.
    /// </summary>
    [AvaloniaTest]
    public void RenderForTesting_WithLigaturesEnabled_ChangesInkCoverage()
    {
        Assert.That(TryFindLigatureFixture(out var fixture), Is.True, "No ligature-capable font fixture was found.");

        var screen = CreateAsciiScreen(fixture.Text);

        long plainInk = MeasureInk(RenderScreen(fixture.FontName, screen, false));
        long ligatureInk = MeasureInk(RenderScreen(fixture.FontName, screen, true));

        Assert.That(ligatureInk, Is.Not.EqualTo(plainInk));
    }

    /// <summary>
    /// Bold text should produce more ink than normal text when ligatures are
    /// enabled, confirming the embolden flag is applied in the shaping path.
    /// </summary>
    [AvaloniaTest]
    public void RenderForTesting_WithLigaturesEnabled_BoldProducesMoreInk()
    {
        Assert.That(TryFindLigatureFixture(out var fixture), Is.True, "No ligature-capable font fixture was found.");

        var normalScreen = CreateAsciiScreen(fixture.Text);
        var boldScreen = CreateAsciiScreen(fixture.Text, bold: true);

        long ligatureNormalInk = MeasureInk(RenderScreen(fixture.FontName, normalScreen, true));
        long ligatureBoldInk = MeasureInk(RenderScreen(fixture.FontName, boldScreen, true));

        Assert.That(ligatureBoldInk, Is.GreaterThan(ligatureNormalInk));
    }

    /// <summary>
    /// Invisible style-run splits should not change glyph positions in ligature mode.
    /// </summary>
    [AvaloniaTest]
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

    /// <summary>
    /// Incremental rendering of a single dirty row should produce the same
    /// output as a full-grid render of the same screen state.
    /// </summary>
    [AvaloniaTest]
    public void RenderForTesting_IncrementalUpdateMatchesFullRender()
    {
        var screen = TestScreenBuilder.CreateScreen(3, 6);
        TestScreenBuilder.SetCell(screen, 0, 0, "A", 0x000000, 0xFFFFFF);
        TestScreenBuilder.SetCell(screen, 1, 0, "B", 0x000000, 0xFFFFFF);
        TestScreenBuilder.SetCell(screen, 2, 0, "C", 0x000000, 0xFFFFFF);
        screen.CursorPosition = (0, 1);

        // First render: AllDirty paints everything onto the backbuffer.
        screen.AllDirty = true;
        var editorClient = new TestEditorClient { CurrentScreen = screen };
        using var control = new EditorControl(editorClient);
        RenderToPng(control, 240, 120);

        // Now update only row 1 and mark it dirty.
        TestScreenBuilder.SetCell(screen, 1, 0, "Z", 0x000000, 0xFFFFFF);
        screen.AllDirty = false;
        screen.DirtyRows = new[] { false, true, false };
        byte[] incremental = RenderToPng(control, 240, 120);

        // Full render of the same screen state for comparison.
        screen.AllDirty = true;
        screen.DirtyRows = null;
        var freshClient = new TestEditorClient { CurrentScreen = screen };
        using var freshControl = new EditorControl(freshClient);
        byte[] fullRender = RenderToPng(freshControl, 240, 120);

        Assert.That(incremental, Is.EqualTo(fullRender));
    }

    /// <summary>
    /// When no rows are dirty the rendered output should still be valid
    /// (backbuffer blit without grid changes, e.g. cursor blink).
    /// </summary>
    [AvaloniaTest]
    public void RenderForTesting_NoDirtyRows_StillProducesOutput()
    {
        var screen = TestScreenBuilder.CreateScreen(2, 4);
        TestScreenBuilder.SetCell(screen, 0, 0, "X", 0x000000, 0xFFFFFF);
        screen.CursorPosition = (0, 1);
        screen.AllDirty = true;

        var editorClient = new TestEditorClient { CurrentScreen = screen };
        using var control = new EditorControl(editorClient);
        byte[] first = RenderToPng(control, 240, 120);

        // No changes — dirty flags clear.
        screen.AllDirty = false;
        screen.DirtyRows = new[] { false, false };
        byte[] second = RenderToPng(control, 240, 120);

        Assert.That(second, Is.EqualTo(first));
    }

    /// <summary>
    /// After SetFallbackFonts is called and a FontChanged event arrives with
    /// an empty guifont, the font chain primary should be the fallback font.
    /// </summary>
    [AvaloniaTest]
    public void SetFallbackFonts_ThenEmptyGuifont_UsesFallbackAsPrimary()
    {
        var platformDefaults = AeroVim.Utilities.Helpers.GetDefaultFallbackFontNames();
        string? fallbackFont = FindNonDefaultFont(platformDefaults);
        Assert.That(fallbackFont, Is.Not.Null, "No non-default font found for test.");

        var screen = CreateAsciiScreen("Hello");
        var editorClient = new TestEditorClient { CurrentScreen = screen };
        using var control = new EditorControl(editorClient);

        // Set user fallback fonts (simulates EditorSessionCoordinator.InitializeAsync)
        control.SetFallbackFonts(new List<string> { fallbackFont! });

        // Simulate Neovim sending option_set guifont "" (empty guifont)
        editorClient.RaiseFontChanged(new FontSettings { FontPointSize = 11 });

        // Trigger a render to process pending actions
        RenderToPng(control, 480, 120);

        // The font chain primary should be the user fallback font
        Assert.That(
            control.GetPrimaryFontNameForTesting(),
            Is.EqualTo(fallbackFont).IgnoreCase,
            "Font chain primary should be the user fallback font, not the platform default.");
    }

    /// <summary>
    /// After SetFallbackFonts and an empty guifont, the text layout
    /// parameters should use the fallback font for grid metric calculations.
    /// </summary>
    [AvaloniaTest]
    public void SetFallbackFonts_ThenEmptyGuifont_TextParamUsesFallbackFont()
    {
        var platformDefaults = AeroVim.Utilities.Helpers.GetDefaultFallbackFontNames();
        string? fallbackFont = FindNonDefaultFont(platformDefaults);
        Assert.That(fallbackFont, Is.Not.Null, "No non-default font found for test.");

        var screen = CreateAsciiScreen("Hello");
        var editorClient = new TestEditorClient { CurrentScreen = screen };
        using var control = new EditorControl(editorClient);

        control.SetFallbackFonts(new List<string> { fallbackFont! });
        editorClient.RaiseFontChanged(new FontSettings { FontPointSize = 11 });

        // Trigger a render to process pending actions
        RenderToPng(control, 480, 120);

        Assert.That(
            control.GetTextParamFontNameForTesting(),
            Is.EqualTo(fallbackFont).IgnoreCase,
            "TextLayoutParameters should use the fallback font name.");
    }

    /// <summary>
    /// Fallback fonts should produce visibly different output compared to
    /// the platform default when guifont is not set.
    /// </summary>
    [AvaloniaTest]
    public void SetFallbackFonts_ThenEmptyGuifont_RendersDifferentlyFromDefault()
    {
        var platformDefaults = AeroVim.Utilities.Helpers.GetDefaultFallbackFontNames();
        string? fallbackFont = FindNonDefaultFont(platformDefaults);
        Assert.That(fallbackFont, Is.Not.Null, "No non-default font found for test.");

        var screen = CreateAsciiScreen("Hello World");

        // Control A: no fallback fonts, empty guifont → platform default
        var clientA = new TestEditorClient { CurrentScreen = screen };
        using var controlA = new EditorControl(clientA);
        clientA.RaiseFontChanged(new FontSettings { FontPointSize = 14 });
        byte[] renderDefault = RenderToPng(controlA, 480, 120);

        // Control B: fallback font set, empty guifont → should use fallback
        var clientB = new TestEditorClient { CurrentScreen = screen };
        using var controlB = new EditorControl(clientB);
        controlB.SetFallbackFonts(new List<string> { fallbackFont! });
        clientB.RaiseFontChanged(new FontSettings { FontPointSize = 14 });
        byte[] renderFallback = RenderToPng(controlB, 480, 120);

        Assert.That(
            renderFallback,
            Is.Not.EqualTo(renderDefault),
            "Fallback font should produce different output than platform default.");
    }

    /// <summary>
    /// When FontChanged fires with empty guifont before SetFallbackFonts is
    /// called, and the pending action processes first (simulating a render
    /// between the two calls), the fallback font should still take effect
    /// after SetFallbackFonts triggers a second rebuild.
    /// </summary>
    [AvaloniaTest]
    public void EmptyGuifont_ThenSetFallbackFonts_EventuallyUsesFallback()
    {
        var platformDefaults = AeroVim.Utilities.Helpers.GetDefaultFallbackFontNames();
        string? fallbackFont = FindNonDefaultFont(platformDefaults);
        Assert.That(fallbackFont, Is.Not.Null, "No non-default font found for test.");

        var screen = CreateAsciiScreen("Hello");
        var editorClient = new TestEditorClient { CurrentScreen = screen };
        using var control = new EditorControl(editorClient);

        // Fire empty guifont BEFORE setting fallback fonts
        editorClient.RaiseFontChanged(new FontSettings { FontPointSize = 11 });

        // Process the pending action (renders with platform default)
        RenderToPng(control, 480, 120);

        // Now set fallback fonts (like EditorSessionCoordinator would)
        control.SetFallbackFonts(new List<string> { fallbackFont! });

        // Render again to process the SetFallbackFonts action
        RenderToPng(control, 480, 120);

        Assert.That(
            control.GetPrimaryFontNameForTesting(),
            Is.EqualTo(fallbackFont).IgnoreCase,
            "After SetFallbackFonts, primary should be the fallback font.");
    }

    /// <summary>
    /// When FontChanged fires with empty guifont AFTER SetFallbackFonts
    /// and both actions are already processed, a subsequent render should
    /// still use the fallback font.
    /// </summary>
    [AvaloniaTest]
    public void SetFallbackFonts_ThenMultipleEmptyGuifontEvents_KeepsFallback()
    {
        var platformDefaults = AeroVim.Utilities.Helpers.GetDefaultFallbackFontNames();
        string? fallbackFont = FindNonDefaultFont(platformDefaults);
        Assert.That(fallbackFont, Is.Not.Null, "No non-default font found for test.");

        var screen = CreateAsciiScreen("Hello");
        var editorClient = new TestEditorClient { CurrentScreen = screen };
        using var control = new EditorControl(editorClient);

        control.SetFallbackFonts(new List<string> { fallbackFont! });
        editorClient.RaiseFontChanged(new FontSettings { FontPointSize = 11 });
        RenderToPng(control, 480, 120);

        // Fire another empty guifont event (e.g., after a resize)
        editorClient.RaiseFontChanged(new FontSettings { FontPointSize = 11 });
        RenderToPng(control, 480, 120);

        Assert.That(
            control.GetPrimaryFontNameForTesting(),
            Is.EqualTo(fallbackFont).IgnoreCase,
            "Subsequent empty guifont events should not reset the fallback font.");
    }

    /// <summary>
    /// User-configured fallback fonts should take priority over guifont
    /// names (including Neovim 0.12+ defaults like SF Mono, Menlo, etc.)
    /// when placed before the $GUIFONT sentinel in the priority list.
    /// </summary>
    [AvaloniaTest]
    public void FontPriorityList_UserFontBeforeGuifont_TakesPriority()
    {
        var platformDefaults = AeroVim.Utilities.Helpers.GetDefaultFallbackFontNames();
        string? fallbackFont = FindNonDefaultFont(platformDefaults);
        Assert.That(fallbackFont, Is.Not.Null, "No non-default font found for test.");

        // Pick a guifont that is a platform default (simulates Neovim 0.12+ defaults)
        string guiFont = platformDefaults[0];

        var screen = CreateAsciiScreen("Hello");
        var editorClient = new TestEditorClient { CurrentScreen = screen };
        using var control = new EditorControl(editorClient);

        // User font placed BEFORE the $GUIFONT sentinel
        control.SetFontPriorityList(new List<string>
        {
            fallbackFont!,
            AeroVim.Editor.Utilities.FontPriorityList.GuiFontSentinel,
            AeroVim.Editor.Utilities.FontPriorityList.SystemMonoSentinel,
        });

        // Simulate Neovim sending a non-empty guifont (e.g. default in 0.12+)
        editorClient.RaiseFontChanged(new FontSettings
        {
            FontNames = new List<string> { guiFont },
            FontPointSize = 11,
        });

        RenderToPng(control, 480, 120);

        Assert.That(
            control.GetPrimaryFontNameForTesting(),
            Is.EqualTo(fallbackFont).IgnoreCase,
            "User font before $GUIFONT sentinel should take priority over guifont.");
    }

    /// <summary>
    /// When the user places $GUIFONT before their fonts in the priority
    /// list, guifont should take priority over user fonts.
    /// </summary>
    [AvaloniaTest]
    public void FontPriorityList_GuifontBeforeUserFont_GuifontTakesPriority()
    {
        var platformDefaults = AeroVim.Utilities.Helpers.GetDefaultFallbackFontNames();
        string? fallbackFont = FindNonDefaultFont(platformDefaults);
        Assert.That(fallbackFont, Is.Not.Null, "No non-default font found for test.");

        string guiFont = platformDefaults[0];

        var screen = CreateAsciiScreen("Hello");
        var editorClient = new TestEditorClient { CurrentScreen = screen };
        using var control = new EditorControl(editorClient);

        // $GUIFONT placed BEFORE the user font
        control.SetFontPriorityList(new List<string>
        {
            AeroVim.Editor.Utilities.FontPriorityList.GuiFontSentinel,
            fallbackFont!,
            AeroVim.Editor.Utilities.FontPriorityList.SystemMonoSentinel,
        });

        editorClient.RaiseFontChanged(new FontSettings
        {
            FontNames = new List<string> { guiFont },
            FontPointSize = 11,
        });

        RenderToPng(control, 480, 120);

        Assert.That(
            control.GetPrimaryFontNameForTesting(),
            Is.EqualTo(guiFont).IgnoreCase,
            "Guifont should take priority when $GUIFONT sentinel is before user font.");
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

    private static string? FindNonDefaultFont(IReadOnlyList<string> platformDefaults)
    {
        // Candidates that are visually distinct from monospace platform defaults.
        var candidates = new[]
        {
            "Georgia", "Times New Roman", "Times", "Palatino",
            "Arial", "Helvetica", "Verdana", "Trebuchet MS",
        };

        foreach (var name in candidates)
        {
            if (platformDefaults.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var typeface = SKTypeface.FromFamilyName(name);
            if (typeface is not null
                && string.Equals(typeface.FamilyName, name, StringComparison.OrdinalIgnoreCase))
            {
                typeface.Dispose();
                return name;
            }

            typeface?.Dispose();
        }

        return null;
    }

    private readonly record struct LigatureFixture(string FontName, string Text);
}
