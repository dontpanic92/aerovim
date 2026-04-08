// <copyright file="VtParserTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests;

using System.Text;
using AeroVim.Editor.Utilities;
using AeroVim.VimClient;
using NUnit.Framework;

/// <summary>
/// Tests VT escape-sequence parsing.
/// </summary>
public class VtParserTests
{
    /// <summary>
    /// Private and greater-than CSI markers should prevent SGR dispatch.
    /// </summary>
    [Test]
    public void Process_GreaterThanMarkedM_DoesNotApplySgrState()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[>4;2mA"));

        var screen = buffer.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[0, 0].Underline, Is.False);
        Assert.That(screen.Cells[0, 0].Undercurl, Is.False);
    }

    /// <summary>
    /// OSC title updates should call the title callback.
    /// </summary>
    [Test]
    public void Process_TitleOsc_InvokesTitleCallback()
    {
        string? title = null;
        var parser = new VtParser(new TerminalBuffer(2, 2), value => title = value);

        parser.Process(Encoding.UTF8.GetBytes("\x1B]2;AeroVim\x1B\\"));

        Assert.That(title, Is.EqualTo("AeroVim"));
    }

    /// <summary>
    /// Foreground-color queries should be answered with the current default color.
    /// </summary>
    [Test]
    public void Process_OscForegroundQuery_WritesCurrentDefaultColor()
    {
        byte[]? response = null;
        var buffer = new TerminalBuffer(2, 2);
        buffer.SetTerminalDefaultForeground(0x112233);
        var parser = new VtParser(buffer, _ => { }, bytes => response = bytes);

        parser.Process(Encoding.UTF8.GetBytes("\x1B]10;?\x1B\\"));

        Assert.That(Encoding.ASCII.GetString(response!), Is.EqualTo("\x1B]10;rgb:1111/2222/3333\x1B\\"));
    }

    /// <summary>
    /// Mouse mode, cursor-shape, and pointer-shape controls should update buffer state.
    /// </summary>
    [Test]
    public void Process_MouseAndCursorControls_UpdateBufferState()
    {
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1006h\x1B[>2p\x1B[5 q\x1B]22;beam\x1B\\\x1B[?25l"));

        Assert.That(buffer.SgrMouseEnabled, Is.True);
        Assert.That(buffer.RequestedCursorShape, Is.EqualTo(CursorShape.Vertical));
        Assert.That(buffer.RequestedCursorBlinking, Is.EqualTo(CursorBlinking.BlinkOn));
        Assert.That(buffer.PointerShape, Is.EqualTo("beam"));
        Assert.That(buffer.PointerMode, Is.EqualTo(2));
        Assert.That(buffer.CursorVisible, Is.False);
    }

    /// <summary>
    /// Extended SGR colors should apply to subsequent character cells.
    /// </summary>
    [Test]
    public void Process_ExtendedSgrColors_AppliesToWrittenCells()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[38;2;17;34;51;48;5;46mA"));

        var screen = buffer.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[0, 0].ForegroundColor, Is.EqualTo(0x112233));
        Assert.That(screen.Cells[0, 0].BackgroundColor, Is.EqualTo(0x00FF00));
    }

    /// <summary>
    /// DECCKM (mode 1) should toggle application cursor keys state.
    /// </summary>
    [Test]
    public void Process_Decckm_TogglesApplicationCursorKeys()
    {
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { });

        Assert.That(buffer.ApplicationCursorKeys, Is.False);

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1h"));
        Assert.That(buffer.ApplicationCursorKeys, Is.True);

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1l"));
        Assert.That(buffer.ApplicationCursorKeys, Is.False);
    }

    /// <summary>
    /// DECAWM (mode 7) should toggle auto-wrap mode.
    /// </summary>
    [Test]
    public void Process_Decawm_TogglesAutoWrap()
    {
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { });

        Assert.That(buffer.AutoWrap, Is.True);

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?7l"));
        Assert.That(buffer.AutoWrap, Is.False);

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?7h"));
        Assert.That(buffer.AutoWrap, Is.True);
    }

    /// <summary>
    /// Bracketed paste mode (2004) should toggle correctly.
    /// </summary>
    [Test]
    public void Process_BracketedPaste_TogglesMode()
    {
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?2004h"));
        Assert.That(buffer.BracketedPasteEnabled, Is.True);

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?2004l"));
        Assert.That(buffer.BracketedPasteEnabled, Is.False);
    }

    /// <summary>
    /// DSR 6 should write back the cursor position report.
    /// </summary>
    [Test]
    public void Process_Dsr6_WritesCursorPositionReport()
    {
        byte[]? response = null;
        var buffer = new TerminalBuffer(10, 10);
        var parser = new VtParser(buffer, _ => { }, bytes => response = bytes);

        buffer.SetCursorPosition(4, 7);
        parser.Process(Encoding.UTF8.GetBytes("\x1B[6n"));

        Assert.That(response, Is.Not.Null);
        Assert.That(Encoding.ASCII.GetString(response!), Is.EqualTo("\x1B[5;8R"));
    }

    /// <summary>
    /// DA1 (CSI c) should write back a device attributes response.
    /// </summary>
    [Test]
    public void Process_Da1_WritesDeviceAttributesResponse()
    {
        byte[]? response = null;
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { }, bytes => response = bytes);

        parser.Process(Encoding.UTF8.GetBytes("\x1B[c"));

        Assert.That(response, Is.Not.Null);
        Assert.That(Encoding.ASCII.GetString(response!), Does.StartWith("\x1B[?"));
    }

    /// <summary>
    /// DEC Special Graphics charset (ESC(0) should translate box-drawing characters.
    /// </summary>
    [Test]
    public void Process_DecLineDrawing_TranslatesBoxChars()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        // ESC(0 activates line drawing for G0, then 'l' and 'q' are box chars
        parser.Process(Encoding.UTF8.GetBytes("\x1B(0lq"));

        var screen = buffer.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("┌"));
        Assert.That(screen.Cells[0, 1].Character, Is.EqualTo("─"));
    }

    /// <summary>
    /// ESC(B should reset charset to ASCII.
    /// </summary>
    [Test]
    public void Process_AsciiCharset_DisablesLineDrawing()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B(0l\x1B(Bl"));

        var screen = buffer.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("┌"));
        Assert.That(screen.Cells[0, 1].Character, Is.EqualTo("l"));
    }

    /// <summary>
    /// SO/SI should switch between G0 and G1.
    /// </summary>
    [Test]
    public void Process_ShiftOutShiftIn_SwitchesCharsets()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        // Designate G1 as line drawing, then SO activates it, SI deactivates
        parser.Process(Encoding.UTF8.GetBytes("\x1B)0\x0Eq\x0Fq"));

        var screen = buffer.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("─"));
        Assert.That(screen.Cells[0, 1].Character, Is.EqualTo("q"));
    }

    /// <summary>
    /// SGR 58 should set the special (underline) color.
    /// </summary>
    [Test]
    public void Process_Sgr58_SetsSpecialColor()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[58;2;255;0;0mA"));

        var screen = buffer.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[0, 0].SpecialColor, Is.EqualTo(0xFF0000));
    }

    /// <summary>
    /// SGR 59 should reset the special color to default.
    /// </summary>
    [Test]
    public void Process_Sgr59_ResetsSpecialColor()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[58;2;255;0;0m\x1B[59mA"));

        var screen = buffer.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[0, 0].SpecialColor, Is.EqualTo(0));
    }

    /// <summary>
    /// Synchronized output mode (2026) should toggle correctly.
    /// </summary>
    [Test]
    public void Process_SynchronizedOutput_TogglesMode()
    {
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?2026h"));
        Assert.That(buffer.SynchronizedOutput, Is.True);

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?2026l"));
        Assert.That(buffer.SynchronizedOutput, Is.False);
    }

    /// <summary>
    /// Focus events mode (1004) should toggle correctly.
    /// </summary>
    [Test]
    public void Process_FocusEvents_TogglesMode()
    {
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1004h"));
        Assert.That(buffer.FocusEventsEnabled, Is.True);

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1004l"));
        Assert.That(buffer.FocusEventsEnabled, Is.False);
    }

    /// <summary>
    /// SGR dim, strikethrough, hidden, blink, overline should set cell attributes.
    /// </summary>
    [Test]
    public void Process_ExtendedSgrAttributes_SetOnCells()
    {
        var buffer = new TerminalBuffer(5, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[2;9;5;53mA"));

        var screen = buffer.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[0, 0].Dim, Is.True);
        Assert.That(screen.Cells[0, 0].Strikethrough, Is.True);
        Assert.That(screen.Cells[0, 0].Blink, Is.True);
        Assert.That(screen.Cells[0, 0].Overline, Is.True);
    }

    /// <summary>
    /// SGR reset codes should clear extended attributes.
    /// </summary>
    [Test]
    public void Process_ExtendedSgrReset_ClearsAttributes()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[2;9;5;53m\x1B[22;29;25;55mA"));

        var screen = buffer.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[0, 0].Dim, Is.False);
        Assert.That(screen.Cells[0, 0].Strikethrough, Is.False);
        Assert.That(screen.Cells[0, 0].Blink, Is.False);
        Assert.That(screen.Cells[0, 0].Overline, Is.False);
    }

    /// <summary>
    /// CSI b (REP) should repeat the last printed character.
    /// </summary>
    [Test]
    public void Process_Rep_RepeatsLastCharacter()
    {
        var buffer = new TerminalBuffer(5, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("X\x1B[3b"));

        var screen = buffer.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("X"));
        Assert.That(screen.Cells[0, 1].Character, Is.EqualTo("X"));
        Assert.That(screen.Cells[0, 2].Character, Is.EqualTo("X"));
        Assert.That(screen.Cells[0, 3].Character, Is.EqualTo("X"));
    }

    /// <summary>
    /// CSI E (CNL) should move cursor to next line at column 0.
    /// </summary>
    [Test]
    public void Process_Cnl_MovesCursorToNextLine()
    {
        var buffer = new TerminalBuffer(4, 4);
        var parser = new VtParser(buffer, _ => { });

        buffer.SetCursorPosition(0, 2);
        parser.Process(Encoding.UTF8.GetBytes("\x1B[2EA"));

        var screen = buffer.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[2, 0].Character, Is.EqualTo("A"));
    }

    /// <summary>
    /// CSI F (CPL) should move cursor to preceding line at column 0.
    /// </summary>
    [Test]
    public void Process_Cpl_MovesCursorToPrecedingLine()
    {
        var buffer = new TerminalBuffer(4, 4);
        var parser = new VtParser(buffer, _ => { });

        buffer.SetCursorPosition(3, 2);
        parser.Process(Encoding.UTF8.GetBytes("\x1B[2FA"));

        var screen = buffer.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[1, 0].Character, Is.EqualTo("A"));
    }

    /// <summary>
    /// ESC E (NEL) should move to the beginning of the next line.
    /// </summary>
    [Test]
    public void Process_Nel_MovesToNextLine()
    {
        var buffer = new TerminalBuffer(4, 4);
        var parser = new VtParser(buffer, _ => { });

        buffer.SetCursorPosition(0, 2);
        parser.Process(Encoding.UTF8.GetBytes("\u001BEA"));

        var screen = buffer.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[1, 0].Character, Is.EqualTo("A"));
    }

    /// <summary>
    /// Application cursor keys: TerminalInputEncoder should emit SS3 sequences.
    /// </summary>
    [Test]
    public void Encode_ApplicationCursorKeys_EmitsSS3()
    {
        string normal = TerminalInputEncoder.Encode("<Up>", false);
        string appMode = TerminalInputEncoder.Encode("<Up>", true);

        Assert.That(normal, Is.EqualTo("\x1B[A"));
        Assert.That(appMode, Is.EqualTo("\x1BOA"));
    }

    /// <summary>
    /// Modified arrow keys should still use CSI even in application cursor mode.
    /// </summary>
    [Test]
    public void Encode_ModifiedArrowKeys_IgnoresDecckm()
    {
        string result = TerminalInputEncoder.Encode("<C-Up>", true);
        Assert.That(result, Is.EqualTo("\x1B[1;5A"));
    }

    /// <summary>
    /// OSC 4 should set a palette color.
    /// </summary>
    [Test]
    public void Process_Osc4_SetsPaletteColor()
    {
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B]4;1;rgb:ff/00/00\x1B\\"));

        Assert.That(buffer.GetPaletteColor(1), Is.EqualTo(0xFF0000));
    }

    /// <summary>
    /// OSC 4 query should write back the palette color.
    /// </summary>
    [Test]
    public void Process_Osc4Query_WritesBackPaletteColor()
    {
        byte[]? response = null;
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { }, bytes => response = bytes);

        parser.Process(Encoding.UTF8.GetBytes("\x1B]4;1;?\x1B\\"));

        Assert.That(response, Is.Not.Null);
        Assert.That(Encoding.ASCII.GetString(response!), Does.Contain(";1;rgb:"));
    }

    /// <summary>
    /// OSC 12 should set cursor color.
    /// </summary>
    [Test]
    public void Process_Osc12_SetsCursorColor()
    {
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B]12;#00ff00\x1B\\"));

        Assert.That(buffer.CursorColor, Is.EqualTo(0x00FF00));
    }

    /// <summary>
    /// OSC 104 should reset palette colors.
    /// </summary>
    [Test]
    public void Process_Osc104_ResetsPaletteColor()
    {
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { });

        int original = buffer.GetPaletteColor(1);
        parser.Process(Encoding.UTF8.GetBytes("\x1B]4;1;rgb:ff/ff/ff\x1B\\"));
        Assert.That(buffer.GetPaletteColor(1), Is.Not.EqualTo(original));

        parser.Process(Encoding.UTF8.GetBytes("\x1B]104;1\x1B\\"));
        Assert.That(buffer.GetPaletteColor(1), Is.EqualTo(original));
    }

    /// <summary>
    /// OSC 110/111/112 should reset default colors.
    /// </summary>
    [Test]
    public void Process_Osc110_111_112_ResetsDefaultColors()
    {
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B]10;#112233\x1B\\"));
        parser.Process(Encoding.UTF8.GetBytes("\x1B]11;#445566\x1B\\"));
        parser.Process(Encoding.UTF8.GetBytes("\x1B]12;#778899\x1B\\"));

        parser.Process(Encoding.UTF8.GetBytes("\x1B]110\x07"));
        Assert.That(buffer.DefaultForeground, Is.EqualTo(0x000000));

        parser.Process(Encoding.UTF8.GetBytes("\x1B]111\x07"));
        Assert.That(buffer.DefaultBackground, Is.EqualTo(0xFFFFFF));

        parser.Process(Encoding.UTF8.GetBytes("\x1B]112\x07"));
        Assert.That(buffer.CursorColor, Is.EqualTo(0x000000));
    }

    /// <summary>
    /// ESC H should set a tab stop, CSI 0g should clear it, CSI 3g should clear all.
    /// </summary>
    [Test]
    public void Process_TabStops_SetAndClear()
    {
        var buffer = new TerminalBuffer(20, 1);
        var parser = new VtParser(buffer, _ => { });

        // Move to col 5 and set tab stop
        parser.Process(Encoding.UTF8.GetBytes("\x1B[6G\x1BH"));

        // Move to col 0, then advance to next tab stop (should hit col 5)
        parser.Process(Encoding.UTF8.GetBytes("\x1B[1G\x1B[I"));
        Assert.That(buffer.CursorCol, Is.EqualTo(5));

        // Clear tab stop at col 5
        parser.Process(Encoding.UTF8.GetBytes("\x1B[0g"));

        // Move to col 0, advance — should skip past 5 to next default stop (8)
        parser.Process(Encoding.UTF8.GetBytes("\x1B[1G\x1B[I"));
        Assert.That(buffer.CursorCol, Is.EqualTo(8));
    }

    /// <summary>
    /// CSI Z (CBT) should move cursor backward to previous tab stop.
    /// </summary>
    [Test]
    public void Process_Cbt_MovesBackToTabStop()
    {
        var buffer = new TerminalBuffer(20, 1);
        var parser = new VtParser(buffer, _ => { });

        // Move to col 10, then back-tab — should hit col 8
        parser.Process(Encoding.UTF8.GetBytes("\x1B[11G\x1B[Z"));
        Assert.That(buffer.CursorCol, Is.EqualTo(8));
    }

    /// <summary>
    /// Mouse tracking modes 1000/1002/1003 should toggle MouseTrackingMode.
    /// </summary>
    [Test]
    public void Process_MouseTrackingModes_UpdateBufferState()
    {
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1000h"));
        Assert.That(buffer.MouseTrackingMode, Is.EqualTo(MouseTrackingMode.Normal));

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1002h"));
        Assert.That(buffer.MouseTrackingMode, Is.EqualTo(MouseTrackingMode.ButtonEvent));

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1003h"));
        Assert.That(buffer.MouseTrackingMode, Is.EqualTo(MouseTrackingMode.AnyEvent));

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1003l"));
        Assert.That(buffer.MouseTrackingMode, Is.EqualTo(MouseTrackingMode.None));
    }

    /// <summary>
    /// Mode 47 should switch alternate screen without cursor save.
    /// </summary>
    [Test]
    public void Process_Mode47_SwitchesAlternateScreen()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        buffer.PutChar('A');

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?47h"));
        buffer.SetCursorPosition(0, 0);
        buffer.PutChar('B');

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?47l"));
        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
    }

    /// <summary>
    /// Mode 1048 should save/restore cursor.
    /// </summary>
    [Test]
    public void Process_Mode1048_SavesRestoresCursor()
    {
        var buffer = new TerminalBuffer(4, 4);
        var parser = new VtParser(buffer, _ => { });

        buffer.SetCursorPosition(2, 3);
        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1048h"));

        buffer.SetCursorPosition(0, 0);
        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1048l"));

        Assert.That(buffer.CursorRow, Is.EqualTo(2));
        Assert.That(buffer.CursorCol, Is.EqualTo(3));
    }

    /// <summary>
    /// CSI ` (HPA) should set cursor column.
    /// </summary>
    [Test]
    public void Process_Hpa_SetsCursorColumn()
    {
        var buffer = new TerminalBuffer(10, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[5`A"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 4].Character, Is.EqualTo("A"));
    }

    /// <summary>
    /// CSI a (HPR) should move cursor forward.
    /// </summary>
    [Test]
    public void Process_Hpr_MovesCursorForward()
    {
        var buffer = new TerminalBuffer(10, 2);
        var parser = new VtParser(buffer, _ => { });

        buffer.SetCursorPosition(0, 2);
        parser.Process(Encoding.UTF8.GetBytes("\x1B[3aA"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 5].Character, Is.EqualTo("A"));
    }

    /// <summary>
    /// CSI e (VPR) should move cursor down.
    /// </summary>
    [Test]
    public void Process_Vpr_MovesCursorDown()
    {
        var buffer = new TerminalBuffer(4, 10);
        var parser = new VtParser(buffer, _ => { });

        buffer.SetCursorPosition(1, 0);
        parser.Process(Encoding.UTF8.GetBytes("\x1B[3eA"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[4, 0].Character, Is.EqualTo("A"));
    }

    /// <summary>
    /// CSI > c (DA2) should write back secondary device attributes.
    /// </summary>
    [Test]
    public void Process_Da2_WritesSecondaryDeviceAttributes()
    {
        byte[]? response = null;
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { }, bytes => response = bytes);

        parser.Process(Encoding.UTF8.GetBytes("\x1B[>c"));

        Assert.That(response, Is.Not.Null);
        Assert.That(Encoding.ASCII.GetString(response!), Does.StartWith("\x1B[>"));
    }

    /// <summary>
    /// ESC # 8 (DECALN) should fill the screen with 'E'.
    /// </summary>
    [Test]
    public void Process_Decaln_FillsScreenWithE()
    {
        var buffer = new TerminalBuffer(3, 2);
        var parser = new VtParser(buffer, _ => { });

        buffer.PutChar('X');
        parser.Process(Encoding.UTF8.GetBytes("\x1B#8"));

        var screen = buffer.GetScreen();
        Assert.That(screen, Is.Not.Null);
        for (int r = 0; r < 2; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                Assert.That(screen!.Cells[r, c].Character, Is.EqualTo("E"), $"Cell [{r},{c}]");
            }
        }
    }

    /// <summary>
    /// DCS strings (ESC P ... ST) should be consumed without corrupting parser state.
    /// </summary>
    [Test]
    public void Process_DcsString_ConsumedWithoutCorruption()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        // DCS with some content, terminated by ST, then print 'A'
        parser.Process(Encoding.UTF8.GetBytes("\x1BP+q544e\x1B\\A"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
    }

    /// <summary>
    /// PM/APC strings should be consumed without corrupting parser state.
    /// </summary>
    [Test]
    public void Process_PmApc_ConsumedWithoutCorruption()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        // PM (ESC ^) with content, terminated by ST, then print 'A'
        parser.Process(Encoding.UTF8.GetBytes("\x1B^some pm content\x1B\\A"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));

        // APC (ESC _) with content, terminated by BEL, then print 'B'
        buffer.SetCursorPosition(0, 1);
        parser.Process(Encoding.UTF8.GetBytes("\x1B_apc stuff\u0007B"));

        screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 1].Character, Is.EqualTo("B"));
    }

    /// <summary>
    /// ESC N (SS2) and ESC O (SS3) should apply charset for one character only.
    /// </summary>
    [Test]
    public void Process_SingleShift_AppliesForOneChar()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        // Designate G2 as line drawing, then SS2 should translate one char
        parser.Process(Encoding.UTF8.GetBytes("\x1B*0\x1BNq"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("─"));

        // Next char without SS2 should be plain
        parser.Process(Encoding.UTF8.GetBytes("q"));
        screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 1].Character, Is.EqualTo("q"));
    }

    /// <summary>
    /// ESC * and ESC + should designate G2 and G3 charsets.
    /// </summary>
    [Test]
    public void Process_G2G3Charset_DesignatesCorrectly()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        // Designate G3 as line drawing, SS3 should translate
        parser.Process(Encoding.UTF8.GetBytes("\x1B+0\x1BOl"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("┌"));
    }

    /// <summary>
    /// DECSC should save and DECRC should restore SGR attributes.
    /// </summary>
    [Test]
    public void Process_DecscDecrc_SavesRestoresAttributes()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        // Set bold + fg color, then save
        parser.Process(Encoding.UTF8.GetBytes("\x1B[1;38;2;255;0;0m\x1B7"));

        // Reset attributes
        parser.Process(Encoding.UTF8.GetBytes("\x1B[0m"));

        // Restore and write
        parser.Process(Encoding.UTF8.GetBytes("\x1B8A"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Bold, Is.True);
        Assert.That(screen.Cells[0, 0].ForegroundColor, Is.EqualTo(0xFF0000));
    }

    /// <summary>
    /// DECSCNM (mode 5) should toggle reverse video.
    /// </summary>
    [Test]
    public void Process_Decscnm_TogglesReverseVideo()
    {
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?5h"));
        Assert.That(buffer.ReverseVideo, Is.True);

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?5l"));
        Assert.That(buffer.ReverseVideo, Is.False);
    }

    /// <summary>
    /// DECOM (mode 6) should make cursor positioning relative to scroll region.
    /// </summary>
    [Test]
    public void Process_Decom_MakesPositioningRelative()
    {
        var buffer = new TerminalBuffer(4, 10);
        var parser = new VtParser(buffer, _ => { });

        // Set scroll region rows 3-7, enable origin mode
        parser.Process(Encoding.UTF8.GetBytes("\x1B[4;8r\x1B[?6h"));

        // CUP(1,1) should place cursor at absolute row 3, col 0
        parser.Process(Encoding.UTF8.GetBytes("\x1B[1;1HA"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[3, 0].Character, Is.EqualTo("A"));
    }

    /// <summary>
    /// DECRQM should respond with mode status.
    /// </summary>
    [Test]
    public void Process_Decrqm_RespondsWithModeStatus()
    {
        byte[]? response = null;
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { }, bytes => response = bytes);

        // Enable auto-wrap, then query mode 7
        parser.Process(Encoding.UTF8.GetBytes("\x1B[?7h"));
        parser.Process(Encoding.UTF8.GetBytes("\x1B[?7$p"));

        Assert.That(response, Is.Not.Null);
        string resp = Encoding.ASCII.GetString(response!);
        Assert.That(resp, Is.EqualTo("\x1B[?7;1$y"));
    }

    /// <summary>
    /// Cursor blink mode 12 should toggle blinking.
    /// </summary>
    [Test]
    public void Process_Mode12_TogglesCursorBlink()
    {
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?12h"));
        Assert.That(buffer.RequestedCursorBlinking, Is.EqualTo(CursorBlinking.BlinkOn));

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?12l"));
        Assert.That(buffer.RequestedCursorBlinking, Is.EqualTo(CursorBlinking.BlinkOff));
    }

    /// <summary>
    /// OSC 52 write should invoke the clipboard write callback with decoded text.
    /// </summary>
    [Test]
    public void Process_Osc52Write_InvokesClipboardCallback()
    {
        string? written = null;
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(
            buffer,
            _ => { },
            writeBack: null,
            clipboardRead: null,
            clipboardWrite: text => written = text);

        // "Hello" in base64 is "SGVsbG8="
        parser.Process(Encoding.UTF8.GetBytes("\x1B]52;c;SGVsbG8=\x1B\\"));

        Assert.That(written, Is.EqualTo("Hello"));
    }

    /// <summary>
    /// OSC 52 query should write back clipboard contents as base64.
    /// </summary>
    [Test]
    public void Process_Osc52Query_WritesBackBase64()
    {
        byte[]? response = null;
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(
            buffer,
            _ => { },
            writeBack: bytes => response = bytes,
            clipboardRead: () => "World",
            clipboardWrite: null);

        parser.Process(Encoding.UTF8.GetBytes("\x1B]52;c;?\x1B\\"));

        Assert.That(response, Is.Not.Null);
        string resp = Encoding.UTF8.GetString(response!);

        // "World" in base64 is "V29ybGQ="
        Assert.That(resp, Does.Contain("V29ybGQ="));
    }
}
