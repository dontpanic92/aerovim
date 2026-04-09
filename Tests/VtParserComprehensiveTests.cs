// <copyright file="VtParserComprehensiveTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests;

using System.Text;
using AeroVim.Editor.Utilities;
using AeroVim.VimClient;
using NUnit.Framework;

/// <summary>
/// Comprehensive tests for VT escape-sequence parsing and terminal buffer operations.
/// </summary>
public class VtParserComprehensiveTests
{
    // ── UTF-8 Decoding ──────────────────────────────────────────────────

    /// <summary>
    /// Two-byte UTF-8 character (é = U+00E9) should decode and render.
    /// </summary>
    [Test]
    public void Process_Utf8TwoByte_DecodesCorrectly()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        // é = 0xC3 0xA9
        parser.Process(new byte[] { 0xC3, 0xA9 });

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("é"));
    }

    /// <summary>
    /// Three-byte UTF-8 CJK character (中 = U+4E2D) should decode and render as wide.
    /// </summary>
    [Test]
    public void Process_Utf8ThreeByteCjk_DecodesAsWideChar()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        // 中 = 0xE4 0xB8 0xAD
        parser.Process(new byte[] { 0xE4, 0xB8, 0xAD });

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("中"));

        // Continuation cell should be null (wide char occupies two columns)
        Assert.That(screen.Cells[0, 1].Character, Is.Null);
    }

    /// <summary>
    /// Four-byte UTF-8 emoji (😀 = U+1F600) should decode and render.
    /// </summary>
    [Test]
    public void Process_Utf8FourByteEmoji_DecodesCorrectly()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        // 😀 = 0xF0 0x9F 0x98 0x80
        parser.Process(new byte[] { 0xF0, 0x9F, 0x98, 0x80 });

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("😀"));
    }

    /// <summary>
    /// Invalid UTF-8 continuation byte should recover and process subsequent ASCII.
    /// </summary>
    [Test]
    public void Process_Utf8InvalidContinuation_Recovers()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        // Start 2-byte sequence (0xC3) then send ASCII 'A' instead of continuation
        parser.Process(new byte[] { 0xC3, (byte)'A' });

        var screen = buffer.GetScreen();

        // Should recover and print 'A'
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
    }

    // ── Control Characters in Ground State ──────────────────────────────

    /// <summary>
    /// BS (0x08) should move the cursor back one column.
    /// </summary>
    [Test]
    public void Process_Backspace_MovesCursorBack()
    {
        var buffer = new TerminalBuffer(5, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("AB\bX"));

        var screen = buffer.GetScreen();

        // 'X' overwrites 'B' at column 1
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[0, 1].Character, Is.EqualTo("X"));
    }

    /// <summary>
    /// TAB (0x09) should advance the cursor to the next tab stop.
    /// </summary>
    [Test]
    public void Process_Tab_AdvancesToNextTabStop()
    {
        var buffer = new TerminalBuffer(20, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("A\tB"));

        // Default tab stops at every 8 columns: A at 0, tab to 8, B at 8
        Assert.That(buffer.CursorCol, Is.EqualTo(9));
        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[0, 8].Character, Is.EqualTo("B"));
    }

    /// <summary>
    /// LF (0x0A) should perform a line feed.
    /// </summary>
    [Test]
    public void Process_LineFeed_MovesToNextRow()
    {
        var buffer = new TerminalBuffer(5, 3);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("A\nB"));

        Assert.That(buffer.CursorRow, Is.EqualTo(1));
        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[1, 1].Character, Is.EqualTo("B"));
    }

    /// <summary>
    /// VT (0x0B) should perform a line feed just like LF.
    /// </summary>
    [Test]
    public void Process_VerticalTab_PerformsLineFeed()
    {
        var buffer = new TerminalBuffer(5, 3);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(new byte[] { (byte)'A', 0x0B, (byte)'B' });

        Assert.That(buffer.CursorRow, Is.EqualTo(1));
    }

    /// <summary>
    /// FF (0x0C) should perform a line feed just like LF.
    /// </summary>
    [Test]
    public void Process_FormFeed_PerformsLineFeed()
    {
        var buffer = new TerminalBuffer(5, 3);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(new byte[] { (byte)'A', 0x0C, (byte)'B' });

        Assert.That(buffer.CursorRow, Is.EqualTo(1));
    }

    /// <summary>
    /// CR (0x0D) should move the cursor to column 0.
    /// </summary>
    [Test]
    public void Process_CarriageReturn_ReturnsToColumn0()
    {
        var buffer = new TerminalBuffer(5, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("ABC\rX"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("X"));
        Assert.That(screen.Cells[0, 1].Character, Is.EqualTo("B"));
    }

    /// <summary>
    /// BEL (0x07) should be silently ignored without affecting output.
    /// </summary>
    [Test]
    public void Process_Bell_IsIgnored()
    {
        var buffer = new TerminalBuffer(3, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(new byte[] { (byte)'A', 0x07, (byte)'B' });

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[0, 1].Character, Is.EqualTo("B"));
    }

    // ── CSI Cursor Movement ─────────────────────────────────────────────

    /// <summary>
    /// CUU (CSI A) should move cursor up with default and explicit parameters.
    /// </summary>
    [Test]
    public void Process_CuuCursorUp_MovesUp()
    {
        var buffer = new TerminalBuffer(5, 5);
        var parser = new VtParser(buffer, _ => { });

        buffer.SetCursorPosition(3, 0);

        // Default parameter (1)
        parser.Process(Encoding.UTF8.GetBytes("\x1B[A"));
        Assert.That(buffer.CursorRow, Is.EqualTo(2));

        // Explicit parameter (2)
        parser.Process(Encoding.UTF8.GetBytes("\x1B[2A"));
        Assert.That(buffer.CursorRow, Is.EqualTo(0));
    }

    /// <summary>
    /// CUU should clamp at the top of the screen.
    /// </summary>
    [Test]
    public void Process_CuuCursorUp_ClampsAtTop()
    {
        var buffer = new TerminalBuffer(5, 5);
        var parser = new VtParser(buffer, _ => { });

        buffer.SetCursorPosition(1, 0);
        parser.Process(Encoding.UTF8.GetBytes("\x1B[99A"));

        Assert.That(buffer.CursorRow, Is.EqualTo(0));
    }

    /// <summary>
    /// CUD (CSI B) should move cursor down with default and explicit parameters.
    /// </summary>
    [Test]
    public void Process_CudCursorDown_MovesDown()
    {
        var buffer = new TerminalBuffer(5, 5);
        var parser = new VtParser(buffer, _ => { });

        // Default parameter (1)
        parser.Process(Encoding.UTF8.GetBytes("\x1B[B"));
        Assert.That(buffer.CursorRow, Is.EqualTo(1));

        // Explicit parameter (2)
        parser.Process(Encoding.UTF8.GetBytes("\x1B[2B"));
        Assert.That(buffer.CursorRow, Is.EqualTo(3));
    }

    /// <summary>
    /// CUD should clamp at the bottom of the screen.
    /// </summary>
    [Test]
    public void Process_CudCursorDown_ClampsAtBottom()
    {
        var buffer = new TerminalBuffer(5, 5);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[99B"));

        Assert.That(buffer.CursorRow, Is.EqualTo(4));
    }

    /// <summary>
    /// CUF (CSI C) should move cursor forward with default and explicit parameters.
    /// </summary>
    [Test]
    public void Process_CufCursorForward_MovesRight()
    {
        var buffer = new TerminalBuffer(10, 1);
        var parser = new VtParser(buffer, _ => { });

        // Default parameter (1)
        parser.Process(Encoding.UTF8.GetBytes("\x1B[C"));
        Assert.That(buffer.CursorCol, Is.EqualTo(1));

        // Explicit parameter (3)
        parser.Process(Encoding.UTF8.GetBytes("\x1B[3C"));
        Assert.That(buffer.CursorCol, Is.EqualTo(4));
    }

    /// <summary>
    /// CUF should clamp at the right edge of the screen.
    /// </summary>
    [Test]
    public void Process_CufCursorForward_ClampsAtRight()
    {
        var buffer = new TerminalBuffer(5, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[99C"));

        Assert.That(buffer.CursorCol, Is.EqualTo(4));
    }

    /// <summary>
    /// CUB (CSI D) should move cursor back with default and explicit parameters.
    /// </summary>
    [Test]
    public void Process_CubCursorBack_MovesLeft()
    {
        var buffer = new TerminalBuffer(10, 1);
        var parser = new VtParser(buffer, _ => { });

        buffer.SetCursorPosition(0, 5);

        // Default parameter (1)
        parser.Process(Encoding.UTF8.GetBytes("\x1B[D"));
        Assert.That(buffer.CursorCol, Is.EqualTo(4));

        // Explicit parameter (2)
        parser.Process(Encoding.UTF8.GetBytes("\x1B[2D"));
        Assert.That(buffer.CursorCol, Is.EqualTo(2));
    }

    /// <summary>
    /// CUB should clamp at the left edge of the screen.
    /// </summary>
    [Test]
    public void Process_CubCursorBack_ClampsAtLeft()
    {
        var buffer = new TerminalBuffer(5, 1);
        var parser = new VtParser(buffer, _ => { });

        buffer.SetCursorPosition(0, 2);
        parser.Process(Encoding.UTF8.GetBytes("\x1B[99D"));

        Assert.That(buffer.CursorCol, Is.EqualTo(0));
    }

    /// <summary>
    /// CUP (CSI H) should set cursor position with 1-based parameters.
    /// </summary>
    [Test]
    public void Process_CupCursorPosition_SetsPosition()
    {
        var buffer = new TerminalBuffer(10, 10);
        var parser = new VtParser(buffer, _ => { });

        // Row 3, Col 5 (1-based)
        parser.Process(Encoding.UTF8.GetBytes("\x1B[3;5H"));

        Assert.That(buffer.CursorRow, Is.EqualTo(2));
        Assert.That(buffer.CursorCol, Is.EqualTo(4));
    }

    /// <summary>
    /// CUP with default parameters should move to home position (0,0).
    /// </summary>
    [Test]
    public void Process_CupDefaultParams_MovesToHome()
    {
        var buffer = new TerminalBuffer(10, 10);
        var parser = new VtParser(buffer, _ => { });

        buffer.SetCursorPosition(5, 5);
        parser.Process(Encoding.UTF8.GetBytes("\x1B[H"));

        Assert.That(buffer.CursorRow, Is.EqualTo(0));
        Assert.That(buffer.CursorCol, Is.EqualTo(0));
    }

    /// <summary>
    /// VPA (CSI d) should set the cursor row while keeping the column.
    /// </summary>
    [Test]
    public void Process_VpaCursorRow_SetsRow()
    {
        var buffer = new TerminalBuffer(10, 10);
        var parser = new VtParser(buffer, _ => { });

        buffer.SetCursorPosition(0, 5);

        // VPA row 4 (1-based)
        parser.Process(Encoding.UTF8.GetBytes("\x1B[4d"));

        Assert.That(buffer.CursorRow, Is.EqualTo(3));
        Assert.That(buffer.CursorCol, Is.EqualTo(5));
    }

    // ── CSI Erase Operations ────────────────────────────────────────────

    /// <summary>
    /// ED mode 0 (erase from cursor to end of display) should clear cells after cursor.
    /// </summary>
    [Test]
    public void Process_EdMode0_ErasesFromCursorToEnd()
    {
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("ABCDEFGH"));
        buffer.SetCursorPosition(0, 2);

        parser.Process(Encoding.UTF8.GetBytes("\x1B[J"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[0, 1].Character, Is.EqualTo("B"));
        Assert.That(screen.Cells[0, 2].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[0, 3].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[1, 0].Character, Is.EqualTo(" "));
    }

    /// <summary>
    /// ED mode 1 (erase from start of display to cursor) should clear cells before and at cursor.
    /// </summary>
    [Test]
    public void Process_EdMode1_ErasesFromStartToCursor()
    {
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("ABCDEFGH"));
        buffer.SetCursorPosition(1, 1);

        parser.Process(Encoding.UTF8.GetBytes("\x1B[1J"));

        var screen = buffer.GetScreen();

        // Rows 0 and row 1 up to col 1 should be cleared
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[0, 3].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[1, 0].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[1, 1].Character, Is.EqualTo(" "));

        // Cells after cursor should remain
        Assert.That(screen.Cells[1, 2].Character, Is.EqualTo("G"));
        Assert.That(screen.Cells[1, 3].Character, Is.EqualTo("H"));
    }

    /// <summary>
    /// ED mode 2 (erase entire display) should clear all cells.
    /// </summary>
    [Test]
    public void Process_EdMode2_ErasesEntireDisplay()
    {
        var buffer = new TerminalBuffer(3, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("ABCDEF"));
        parser.Process(Encoding.UTF8.GetBytes("\x1B[2J"));

        var screen = buffer.GetScreen();
        for (int r = 0; r < 2; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                Assert.That(screen!.Cells[r, c].Character, Is.EqualTo(" "), $"Cell [{r},{c}]");
            }
        }
    }

    /// <summary>
    /// EL mode 0 (erase from cursor to end of line) should clear cells to the right.
    /// </summary>
    [Test]
    public void Process_ElMode0_ErasesFromCursorToEndOfLine()
    {
        var buffer = new TerminalBuffer(5, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("ABCDE"));
        buffer.SetCursorPosition(0, 2);

        parser.Process(Encoding.UTF8.GetBytes("\x1B[K"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[0, 1].Character, Is.EqualTo("B"));
        Assert.That(screen.Cells[0, 2].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[0, 3].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[0, 4].Character, Is.EqualTo(" "));
    }

    /// <summary>
    /// EL mode 1 (erase from start of line to cursor) should clear cells to the left.
    /// </summary>
    [Test]
    public void Process_ElMode1_ErasesFromStartToEndOfLine()
    {
        var buffer = new TerminalBuffer(5, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("ABCDE"));
        buffer.SetCursorPosition(0, 2);

        parser.Process(Encoding.UTF8.GetBytes("\x1B[1K"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[0, 1].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[0, 2].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[0, 3].Character, Is.EqualTo("D"));
        Assert.That(screen.Cells[0, 4].Character, Is.EqualTo("E"));
    }

    /// <summary>
    /// EL mode 2 (erase entire line) should clear the whole line.
    /// </summary>
    [Test]
    public void Process_ElMode2_ErasesEntireLine()
    {
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("ABCDEFGH"));
        buffer.SetCursorPosition(0, 1);

        parser.Process(Encoding.UTF8.GetBytes("\x1B[2K"));

        var screen = buffer.GetScreen();
        for (int c = 0; c < 4; c++)
        {
            Assert.That(screen!.Cells[0, c].Character, Is.EqualTo(" "), $"Col {c}");
        }

        // Row 1 should be unaffected
        Assert.That(screen!.Cells[1, 0].Character, Is.EqualTo("E"));
    }

    /// <summary>
    /// ECH (CSI X) should erase N characters at cursor position.
    /// </summary>
    [Test]
    public void Process_Ech_ErasesCharactersAtCursor()
    {
        var buffer = new TerminalBuffer(6, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("ABCDEF"));
        buffer.SetCursorPosition(0, 1);

        parser.Process(Encoding.UTF8.GetBytes("\x1B[3X"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[0, 1].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[0, 2].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[0, 3].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[0, 4].Character, Is.EqualTo("E"));
        Assert.That(screen.Cells[0, 5].Character, Is.EqualTo("F"));
    }

    // ── CSI Insert/Delete ───────────────────────────────────────────────

    /// <summary>
    /// ICH (CSI @) should insert blank characters, shifting content right.
    /// </summary>
    [Test]
    public void Process_Ich_InsertsBlankCharacters()
    {
        var buffer = new TerminalBuffer(6, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("ABCDEF"));
        buffer.SetCursorPosition(0, 1);

        parser.Process(Encoding.UTF8.GetBytes("\x1B[2@"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[0, 1].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[0, 2].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[0, 3].Character, Is.EqualTo("B"));
        Assert.That(screen.Cells[0, 4].Character, Is.EqualTo("C"));
        Assert.That(screen.Cells[0, 5].Character, Is.EqualTo("D"));
    }

    /// <summary>
    /// DCH (CSI P) should delete characters, shifting content left.
    /// </summary>
    [Test]
    public void Process_Dch_DeletesCharacters()
    {
        var buffer = new TerminalBuffer(6, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("ABCDEF"));
        buffer.SetCursorPosition(0, 1);

        parser.Process(Encoding.UTF8.GetBytes("\x1B[2P"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[0, 1].Character, Is.EqualTo("D"));
        Assert.That(screen.Cells[0, 2].Character, Is.EqualTo("E"));
        Assert.That(screen.Cells[0, 3].Character, Is.EqualTo("F"));
        Assert.That(screen.Cells[0, 4].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[0, 5].Character, Is.EqualTo(" "));
    }

    /// <summary>
    /// IL (CSI L) should insert blank lines, shifting content down.
    /// </summary>
    [Test]
    public void Process_Il_InsertsLines()
    {
        var buffer = new TerminalBuffer(3, 4);
        var parser = new VtParser(buffer, _ => { });

        FillRow(buffer, 0, 'A');
        FillRow(buffer, 1, 'B');
        FillRow(buffer, 2, 'C');
        FillRow(buffer, 3, 'D');

        buffer.SetCursorPosition(1, 0);
        parser.Process(Encoding.UTF8.GetBytes("\x1B[1L"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[1, 0].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[2, 0].Character, Is.EqualTo("B"));
        Assert.That(screen.Cells[3, 0].Character, Is.EqualTo("C"));
    }

    /// <summary>
    /// DL (CSI M) should delete lines, shifting content up.
    /// </summary>
    [Test]
    public void Process_Dl_DeletesLines()
    {
        var buffer = new TerminalBuffer(3, 4);
        var parser = new VtParser(buffer, _ => { });

        FillRow(buffer, 0, 'A');
        FillRow(buffer, 1, 'B');
        FillRow(buffer, 2, 'C');
        FillRow(buffer, 3, 'D');

        buffer.SetCursorPosition(1, 0);
        parser.Process(Encoding.UTF8.GetBytes("\x1B[1M"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[1, 0].Character, Is.EqualTo("C"));
        Assert.That(screen.Cells[2, 0].Character, Is.EqualTo("D"));
        Assert.That(screen.Cells[3, 0].Character, Is.EqualTo(" "));
    }

    // ── CSI Scroll ──────────────────────────────────────────────────────

    /// <summary>
    /// SU (CSI S) should scroll content up within the scroll region.
    /// </summary>
    [Test]
    public void Process_Su_ScrollsUp()
    {
        var buffer = new TerminalBuffer(3, 3);
        var parser = new VtParser(buffer, _ => { });

        FillRow(buffer, 0, 'A');
        FillRow(buffer, 1, 'B');
        FillRow(buffer, 2, 'C');

        parser.Process(Encoding.UTF8.GetBytes("\x1B[1S"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("B"));
        Assert.That(screen.Cells[1, 0].Character, Is.EqualTo("C"));
        Assert.That(screen.Cells[2, 0].Character, Is.EqualTo(" "));
    }

    /// <summary>
    /// SD (CSI T) should scroll content down within the scroll region.
    /// </summary>
    [Test]
    public void Process_Sd_ScrollsDown()
    {
        var buffer = new TerminalBuffer(3, 3);
        var parser = new VtParser(buffer, _ => { });

        FillRow(buffer, 0, 'A');
        FillRow(buffer, 1, 'B');
        FillRow(buffer, 2, 'C');

        parser.Process(Encoding.UTF8.GetBytes("\x1B[1T"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[1, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[2, 0].Character, Is.EqualTo("B"));
    }

    /// <summary>
    /// SU within a scroll region should only affect the region.
    /// </summary>
    [Test]
    public void Process_Su_WithScrollRegion_OnlyAffectsRegion()
    {
        var buffer = new TerminalBuffer(3, 4);
        var parser = new VtParser(buffer, _ => { });

        FillRow(buffer, 0, 'A');
        FillRow(buffer, 1, 'B');
        FillRow(buffer, 2, 'C');
        FillRow(buffer, 3, 'D');

        // Set scroll region to rows 1-2 (1-based: 2-3)
        parser.Process(Encoding.UTF8.GetBytes("\x1B[2;3r"));

        // Position cursor inside region for scroll
        buffer.SetCursorPosition(1, 0);
        parser.Process(Encoding.UTF8.GetBytes("\x1B[1S"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[1, 0].Character, Is.EqualTo("C"));
        Assert.That(screen.Cells[2, 0].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[3, 0].Character, Is.EqualTo("D"));
    }

    // ── SGR Comprehensive ───────────────────────────────────────────────

    /// <summary>
    /// SGR bold (1) and reset-bold (22) should set and clear the Bold attribute.
    /// </summary>
    [Test]
    public void Process_SgrBold_SetsAndResetsAttribute()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[1mA\x1B[22mB"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Bold, Is.True);
        Assert.That(screen.Cells[0, 1].Bold, Is.False);
    }

    /// <summary>
    /// SGR dim (2) and reset-dim (22) should set and clear the Dim attribute.
    /// </summary>
    [Test]
    public void Process_SgrDim_SetsAndResetsAttribute()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[2mA\x1B[22mB"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Dim, Is.True);
        Assert.That(screen.Cells[0, 1].Dim, Is.False);
    }

    /// <summary>
    /// SGR italic (3) and reset-italic (23) should set and clear the Italic attribute.
    /// </summary>
    [Test]
    public void Process_SgrItalic_SetsAndResetsAttribute()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[3mA\x1B[23mB"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Italic, Is.True);
        Assert.That(screen.Cells[0, 1].Italic, Is.False);
    }

    /// <summary>
    /// SGR underline (4) and reset-underline (24) should set and clear the Underline attribute.
    /// </summary>
    [Test]
    public void Process_SgrUnderline_SetsAndResetsAttribute()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[4mA\x1B[24mB"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Underline, Is.True);
        Assert.That(screen.Cells[0, 1].Underline, Is.False);
    }

    /// <summary>
    /// SGR blink (5) and reset-blink (25) should set and clear the Blink attribute.
    /// </summary>
    [Test]
    public void Process_SgrBlink_SetsAndResetsAttribute()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[5mA\x1B[25mB"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Blink, Is.True);
        Assert.That(screen.Cells[0, 1].Blink, Is.False);
    }

    /// <summary>
    /// SGR reverse (7) and reset-reverse (27) should set and clear the Reverse attribute.
    /// </summary>
    [Test]
    public void Process_SgrReverse_SetsAndResetsAttribute()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[7mA\x1B[27mB"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Reverse, Is.True);
        Assert.That(screen.Cells[0, 1].Reverse, Is.False);
    }

    /// <summary>
    /// SGR hidden (8) and reset-hidden (28) should set and clear the Hidden attribute.
    /// </summary>
    [Test]
    public void Process_SgrHidden_SetsAndResetsAttribute()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[8mA\x1B[28mB"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Hidden, Is.True);
        Assert.That(screen.Cells[0, 1].Hidden, Is.False);
    }

    /// <summary>
    /// SGR strikethrough (9) and reset-strikethrough (29) should set and clear the attribute.
    /// </summary>
    [Test]
    public void Process_SgrStrikethrough_SetsAndResetsAttribute()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[9mA\x1B[29mB"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Strikethrough, Is.True);
        Assert.That(screen.Cells[0, 1].Strikethrough, Is.False);
    }

    /// <summary>
    /// SGR overline (53) and reset-overline (55) should set and clear the attribute.
    /// </summary>
    [Test]
    public void Process_SgrOverline_SetsAndResetsAttribute()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[53mA\x1B[55mB"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Overline, Is.True);
        Assert.That(screen.Cells[0, 1].Overline, Is.False);
    }

    /// <summary>
    /// SGR 21 (double underline) should set the Underline attribute.
    /// </summary>
    [Test]
    public void Process_SgrDoubleUnderline_SetsUnderline()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[21mA"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Underline, Is.True);
    }

    /// <summary>
    /// SGR 0 should reset all attributes to default.
    /// </summary>
    [Test]
    public void Process_SgrReset_ClearsAllAttributes()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[1;3;4;7;9;53mX\x1B[0mA"));

        var screen = buffer.GetScreen();

        // Cell 'A' should have no attributes
        Assert.That(screen!.Cells[0, 1].Bold, Is.False);
        Assert.That(screen.Cells[0, 1].Italic, Is.False);
        Assert.That(screen.Cells[0, 1].Underline, Is.False);
        Assert.That(screen.Cells[0, 1].Reverse, Is.False);
        Assert.That(screen.Cells[0, 1].Strikethrough, Is.False);
        Assert.That(screen.Cells[0, 1].Overline, Is.False);
    }

    // ── SGR Colors Comprehensive ────────────────────────────────────────

    /// <summary>
    /// Standard foreground colors (30-37) should set the correct RGB value.
    /// </summary>
    [Test]
    public void Process_SgrStandardForeground_SetsCorrectColor()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        // SGR 31 = red foreground (0xCC0000)
        parser.Process(Encoding.UTF8.GetBytes("\x1B[31mA"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].ForegroundColor, Is.EqualTo(0xCC0000));
    }

    /// <summary>
    /// Standard background colors (40-47) should set the correct RGB value.
    /// </summary>
    [Test]
    public void Process_SgrStandardBackground_SetsCorrectColor()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        // SGR 42 = green background (0x00CC00)
        parser.Process(Encoding.UTF8.GetBytes("\x1B[42mA"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].BackgroundColor, Is.EqualTo(0x00CC00));
    }

    /// <summary>
    /// Bright foreground colors (90-97) should set the correct bright RGB value.
    /// </summary>
    [Test]
    public void Process_SgrBrightForeground_SetsCorrectColor()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        // SGR 91 = bright red foreground (0xFF5555)
        parser.Process(Encoding.UTF8.GetBytes("\x1B[91mA"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].ForegroundColor, Is.EqualTo(0xFF5555));
    }

    /// <summary>
    /// Bright background colors (100-107) should set the correct bright RGB value.
    /// </summary>
    [Test]
    public void Process_SgrBrightBackground_SetsCorrectColor()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        // SGR 102 = bright green background (0x55FF55)
        parser.Process(Encoding.UTF8.GetBytes("\x1B[102mA"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].BackgroundColor, Is.EqualTo(0x55FF55));
    }

    /// <summary>
    /// SGR 39 should reset the foreground to the default.
    /// </summary>
    [Test]
    public void Process_SgrDefaultForeground_ResetsColor()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[31m\x1B[39mA"));

        var screen = buffer.GetScreen();

        // After reset to default, foreground should be 0 (default)
        Assert.That(screen!.Cells[0, 0].ForegroundColor, Is.EqualTo(0));
    }

    /// <summary>
    /// SGR 49 should reset the background to the default (white = 0xFFFFFF).
    /// </summary>
    [Test]
    public void Process_SgrDefaultBackground_ResetsColor()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        // Set a custom bg, then reset it back to default
        parser.Process(Encoding.UTF8.GetBytes("\x1B[41mX\x1B[49mA"));

        var screen = buffer.GetScreen();

        // Cell 'X' should have the custom bg
        Assert.That(screen!.Cells[0, 0].BackgroundColor, Is.Not.EqualTo(buffer.DefaultBackground));

        // Cell 'A' should match the terminal's default background
        Assert.That(screen.Cells[0, 1].BackgroundColor, Is.EqualTo(buffer.DefaultBackground));
    }

    /// <summary>
    /// 256-color foreground (38;5;N) should resolve to the palette color.
    /// </summary>
    [Test]
    public void Process_Sgr256Foreground_SetsCorrectColor()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        // Index 196 is in the 6x6x6 cube: (196-16) = 180, r=5,g=0,b=0 → rgb(255,0,0)
        parser.Process(Encoding.UTF8.GetBytes("\x1B[38;5;196mA"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].ForegroundColor, Is.EqualTo(0xFF0000));
    }

    /// <summary>
    /// 256-color background (48;5;N) should resolve to the palette color.
    /// </summary>
    [Test]
    public void Process_Sgr256Background_SetsCorrectColor()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        // Index 46 is in the 6x6x6 cube: (46-16)=30, r=0,g=5,b=0 → rgb(0,255,0)
        parser.Process(Encoding.UTF8.GetBytes("\x1B[48;5;46mA"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].BackgroundColor, Is.EqualTo(0x00FF00));
    }

    /// <summary>
    /// Truecolor foreground (38;2;R;G;B) should set exact RGB value.
    /// </summary>
    [Test]
    public void Process_SgrTruecolorForeground_SetsExactColor()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[38;2;100;150;200mA"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].ForegroundColor, Is.EqualTo((100 << 16) | (150 << 8) | 200));
    }

    /// <summary>
    /// Truecolor background (48;2;R;G;B) should set exact RGB value.
    /// </summary>
    [Test]
    public void Process_SgrTruecolorBackground_SetsExactColor()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[48;2;10;20;30mA"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].BackgroundColor, Is.EqualTo((10 << 16) | (20 << 8) | 30));
    }

    // ── Scroll Region (DECSTBM) ─────────────────────────────────────────

    /// <summary>
    /// DECSTBM should reset cursor to position (0,0).
    /// </summary>
    [Test]
    public void Process_Decstbm_ResetsCursorToOrigin()
    {
        var buffer = new TerminalBuffer(5, 10);
        var parser = new VtParser(buffer, _ => { });

        buffer.SetCursorPosition(5, 3);

        // Set scroll region rows 2-8 (1-based: 3;9)
        parser.Process(Encoding.UTF8.GetBytes("\x1B[3;9r"));

        Assert.That(buffer.CursorRow, Is.EqualTo(0));
        Assert.That(buffer.CursorCol, Is.EqualTo(0));
    }

    /// <summary>
    /// Scrolling within a scroll region should only affect the region's rows.
    /// </summary>
    [Test]
    public void Process_Decstbm_ScrollUpWithinRegion()
    {
        var buffer = new TerminalBuffer(3, 5);
        var parser = new VtParser(buffer, _ => { });

        FillRow(buffer, 0, 'A');
        FillRow(buffer, 1, 'B');
        FillRow(buffer, 2, 'C');
        FillRow(buffer, 3, 'D');
        FillRow(buffer, 4, 'E');

        // Set scroll region rows 1-3 (1-based: 2;4)
        parser.Process(Encoding.UTF8.GetBytes("\x1B[2;4r"));

        buffer.SetCursorPosition(1, 0);
        buffer.ScrollUp(1);

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[1, 0].Character, Is.EqualTo("C"));
        Assert.That(screen.Cells[2, 0].Character, Is.EqualTo("D"));
        Assert.That(screen.Cells[3, 0].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[4, 0].Character, Is.EqualTo("E"));
    }

    // ── DECSCUSR Cursor Styles ──────────────────────────────────────────

    /// <summary>
    /// DECSCUSR 0 should reset to default cursor (shape=null, blink=off).
    /// </summary>
    [Test]
    public void Process_Decscusr0_ResetsToDefault()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[0 q"));

        Assert.That(buffer.RequestedCursorShape, Is.Null);
        Assert.That(buffer.RequestedCursorBlinking, Is.EqualTo(CursorBlinking.BlinkOff));
    }

    /// <summary>
    /// DECSCUSR 1 should set blinking block cursor.
    /// </summary>
    [Test]
    public void Process_Decscusr1_SetsBlinkingBlock()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[1 q"));

        Assert.That(buffer.RequestedCursorShape, Is.EqualTo(CursorShape.Block));
        Assert.That(buffer.RequestedCursorBlinking, Is.EqualTo(CursorBlinking.BlinkOn));
    }

    /// <summary>
    /// DECSCUSR 2 should set steady block cursor.
    /// </summary>
    [Test]
    public void Process_Decscusr2_SetsSteadyBlock()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[2 q"));

        Assert.That(buffer.RequestedCursorShape, Is.EqualTo(CursorShape.Block));
        Assert.That(buffer.RequestedCursorBlinking, Is.EqualTo(CursorBlinking.BlinkOff));
    }

    /// <summary>
    /// DECSCUSR 3 should set blinking underline cursor.
    /// </summary>
    [Test]
    public void Process_Decscusr3_SetsBlinkingUnderline()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[3 q"));

        Assert.That(buffer.RequestedCursorShape, Is.EqualTo(CursorShape.Horizontal));
        Assert.That(buffer.RequestedCursorBlinking, Is.EqualTo(CursorBlinking.BlinkOn));
    }

    /// <summary>
    /// DECSCUSR 4 should set steady underline cursor.
    /// </summary>
    [Test]
    public void Process_Decscusr4_SetsSteadyUnderline()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[4 q"));

        Assert.That(buffer.RequestedCursorShape, Is.EqualTo(CursorShape.Horizontal));
        Assert.That(buffer.RequestedCursorBlinking, Is.EqualTo(CursorBlinking.BlinkOff));
    }

    /// <summary>
    /// DECSCUSR 5 should set blinking bar cursor.
    /// </summary>
    [Test]
    public void Process_Decscusr5_SetsBlinkingBar()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[5 q"));

        Assert.That(buffer.RequestedCursorShape, Is.EqualTo(CursorShape.Vertical));
        Assert.That(buffer.RequestedCursorBlinking, Is.EqualTo(CursorBlinking.BlinkOn));
    }

    /// <summary>
    /// DECSCUSR 6 should set steady bar cursor.
    /// </summary>
    [Test]
    public void Process_Decscusr6_SetsSteadyBar()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[6 q"));

        Assert.That(buffer.RequestedCursorShape, Is.EqualTo(CursorShape.Vertical));
        Assert.That(buffer.RequestedCursorBlinking, Is.EqualTo(CursorBlinking.BlinkOff));
    }

    // ── OSC Title ───────────────────────────────────────────────────────

    /// <summary>
    /// OSC 0 should set the title via BEL terminator.
    /// </summary>
    [Test]
    public void Process_Osc0WithBel_SetsTitle()
    {
        string? title = null;
        var parser = new VtParser(new TerminalBuffer(2, 2), value => title = value);

        parser.Process(Encoding.UTF8.GetBytes("\x1B]0;MyTitle\x07"));

        Assert.That(title, Is.EqualTo("MyTitle"));
    }

    /// <summary>
    /// OSC 0 should set the title via ST (ESC \) terminator.
    /// </summary>
    [Test]
    public void Process_Osc0WithSt_SetsTitle()
    {
        string? title = null;
        var parser = new VtParser(new TerminalBuffer(2, 2), value => title = value);

        parser.Process(Encoding.UTF8.GetBytes("\x1B]0;MyTitle\x1B\\"));

        Assert.That(title, Is.EqualTo("MyTitle"));
    }

    /// <summary>
    /// OSC 2 should set the title the same as OSC 0.
    /// </summary>
    [Test]
    public void Process_Osc2_SetsTitle()
    {
        string? title = null;
        var parser = new VtParser(new TerminalBuffer(2, 2), value => title = value);

        parser.Process(Encoding.UTF8.GetBytes("\x1B]2;Window Title\x1B\\"));

        Assert.That(title, Is.EqualTo("Window Title"));
    }

    // ── OSC Color Set ───────────────────────────────────────────────────

    /// <summary>
    /// OSC 10 with rgb:RR/GG/BB format should set the default foreground.
    /// </summary>
    [Test]
    public void Process_Osc10RgbFormat_SetsForeground()
    {
        var buffer = new TerminalBuffer(2, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B]10;rgb:aa/bb/cc\x1B\\"));

        Assert.That(buffer.DefaultForeground, Is.EqualTo(0xAABBCC));
    }

    /// <summary>
    /// OSC 10 with #RRGGBB format should set the default foreground.
    /// </summary>
    [Test]
    public void Process_Osc10HashFormat_SetsForeground()
    {
        var buffer = new TerminalBuffer(2, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B]10;#112233\x1B\\"));

        Assert.That(buffer.DefaultForeground, Is.EqualTo(0x112233));
    }

    /// <summary>
    /// OSC 10 with #RGB short format should expand and set the default foreground.
    /// </summary>
    [Test]
    public void Process_Osc10ShortHashFormat_SetsForeground()
    {
        var buffer = new TerminalBuffer(2, 2);
        var parser = new VtParser(buffer, _ => { });

        // #F80 → FF8800
        parser.Process(Encoding.UTF8.GetBytes("\x1B]10;#F80\x1B\\"));

        Assert.That(buffer.DefaultForeground, Is.EqualTo(0xFF8800));
    }

    /// <summary>
    /// OSC 11 with rgb:RR/GG/BB format should set the default background.
    /// </summary>
    [Test]
    public void Process_Osc11RgbFormat_SetsBackground()
    {
        var buffer = new TerminalBuffer(2, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B]11;rgb:11/22/33\x1B\\"));

        Assert.That(buffer.DefaultBackground, Is.EqualTo(0x112233));
    }

    /// <summary>
    /// OSC 11 with #RRGGBB format should set the default background.
    /// </summary>
    [Test]
    public void Process_Osc11HashFormat_SetsBackground()
    {
        var buffer = new TerminalBuffer(2, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B]11;#445566\x1B\\"));

        Assert.That(buffer.DefaultBackground, Is.EqualTo(0x445566));
    }

    // ── OSC Color Query ─────────────────────────────────────────────────

    /// <summary>
    /// OSC 10 with "?" should write back the current default foreground color.
    /// </summary>
    [Test]
    public void Process_Osc10Query_WritesBackForegroundColor()
    {
        byte[]? response = null;
        var buffer = new TerminalBuffer(2, 2);
        buffer.SetTerminalDefaultForeground(0xAABBCC);
        var parser = new VtParser(buffer, _ => { }, bytes => response = bytes);

        parser.Process(Encoding.UTF8.GetBytes("\x1B]10;?\x1B\\"));

        Assert.That(response, Is.Not.Null);
        string resp = Encoding.ASCII.GetString(response!);
        Assert.That(resp, Does.Contain("10;rgb:"));
    }

    /// <summary>
    /// OSC 11 with "?" should write back the current default background color.
    /// </summary>
    [Test]
    public void Process_Osc11Query_WritesBackBackgroundColor()
    {
        byte[]? response = null;
        var buffer = new TerminalBuffer(2, 2);
        buffer.SetTerminalDefaultBackground(0x445566);
        var parser = new VtParser(buffer, _ => { }, bytes => response = bytes);

        parser.Process(Encoding.UTF8.GetBytes("\x1B]11;?\x1B\\"));

        Assert.That(response, Is.Not.Null);
        string resp = Encoding.ASCII.GetString(response!);
        Assert.That(resp, Does.Contain("11;rgb:"));
    }

    // ── ESC Sequences ───────────────────────────────────────────────────

    /// <summary>
    /// ESC D (IND) should perform index (line feed) moving cursor down.
    /// </summary>
    [Test]
    public void Process_EscD_Ind_MovesDown()
    {
        var buffer = new TerminalBuffer(5, 3);
        var parser = new VtParser(buffer, _ => { });

        buffer.SetCursorPosition(0, 2);
        parser.Process(Encoding.UTF8.GetBytes("\u001BDA"));

        Assert.That(buffer.CursorRow, Is.EqualTo(1));
        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[1, 2].Character, Is.EqualTo("A"));
    }

    /// <summary>
    /// ESC D (IND) at the bottom of scroll region should scroll up.
    /// </summary>
    [Test]
    public void Process_EscD_IndAtBottom_ScrollsUp()
    {
        var buffer = new TerminalBuffer(3, 3);
        var parser = new VtParser(buffer, _ => { });

        FillRow(buffer, 0, 'A');
        FillRow(buffer, 1, 'B');
        FillRow(buffer, 2, 'C');
        buffer.SetCursorPosition(2, 0);

        parser.Process(Encoding.UTF8.GetBytes("\u001BD"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("B"));
        Assert.That(screen.Cells[1, 0].Character, Is.EqualTo("C"));
        Assert.That(screen.Cells[2, 0].Character, Is.EqualTo(" "));
    }

    /// <summary>
    /// ESC M (RI) should move cursor up, or scroll down at top of scroll region.
    /// </summary>
    [Test]
    public void Process_EscM_ReverseIndex_MovesUp()
    {
        var buffer = new TerminalBuffer(5, 3);
        var parser = new VtParser(buffer, _ => { });

        buffer.SetCursorPosition(1, 0);
        parser.Process(Encoding.UTF8.GetBytes("\x1BM"));

        Assert.That(buffer.CursorRow, Is.EqualTo(0));
    }

    /// <summary>
    /// ESC M at top of scroll region should scroll down.
    /// </summary>
    [Test]
    public void Process_EscM_ReverseIndexAtTop_ScrollsDown()
    {
        var buffer = new TerminalBuffer(3, 3);
        var parser = new VtParser(buffer, _ => { });

        FillRow(buffer, 0, 'A');
        FillRow(buffer, 1, 'B');
        FillRow(buffer, 2, 'C');
        buffer.SetCursorPosition(0, 0);

        parser.Process(Encoding.UTF8.GetBytes("\x1BM"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[1, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[2, 0].Character, Is.EqualTo("B"));
    }

    /// <summary>
    /// ESC c (RIS) should perform a full terminal reset.
    /// </summary>
    [Test]
    public void Process_EscC_Ris_ResetsTerminal()
    {
        var buffer = new TerminalBuffer(4, 3);
        var parser = new VtParser(buffer, _ => { });

        // Set some state
        buffer.SetCursorPosition(2, 3);
        parser.Process(Encoding.UTF8.GetBytes("\x1B[1m"));
        parser.Process(Encoding.UTF8.GetBytes("\x1B[?7l"));
        parser.Process(Encoding.UTF8.GetBytes("\x1B[?2004h"));

        // Full reset
        parser.Process(Encoding.UTF8.GetBytes("\u001Bc"));

        Assert.That(buffer.CursorRow, Is.EqualTo(0));
        Assert.That(buffer.CursorCol, Is.EqualTo(0));
        Assert.That(buffer.AutoWrap, Is.True);
        Assert.That(buffer.BracketedPasteEnabled, Is.False);
    }

    /// <summary>
    /// ESC H (HTS) should set a tab stop at the current cursor position.
    /// </summary>
    [Test]
    public void Process_EscH_Hts_SetsTabStop()
    {
        var buffer = new TerminalBuffer(20, 1);
        var parser = new VtParser(buffer, _ => { });

        // Move to col 5, set tab stop
        buffer.SetCursorPosition(0, 5);
        parser.Process(Encoding.UTF8.GetBytes("\x1BH"));

        // Move to col 0, then tab — should land at 5
        buffer.SetCursorPosition(0, 0);
        buffer.AdvanceToNextTabStop();

        Assert.That(buffer.CursorCol, Is.EqualTo(5));
    }

    /// <summary>
    /// ESC 7 (DECSC) and ESC 8 (DECRC) should save and restore cursor position.
    /// </summary>
    [Test]
    public void Process_Esc7Esc8_SaveRestoreCursor()
    {
        var buffer = new TerminalBuffer(10, 10);
        var parser = new VtParser(buffer, _ => { });

        buffer.SetCursorPosition(3, 7);
        parser.Process(Encoding.UTF8.GetBytes("\u001B7"));

        buffer.SetCursorPosition(0, 0);
        parser.Process(Encoding.UTF8.GetBytes("\u001B8"));

        Assert.That(buffer.CursorRow, Is.EqualTo(3));
        Assert.That(buffer.CursorCol, Is.EqualTo(7));
    }

    /// <summary>
    /// ESC s and ESC u should save and restore cursor as alternatives.
    /// </summary>
    [Test]
    public void Process_EscSEscU_SaveRestoreCursor()
    {
        var buffer = new TerminalBuffer(10, 10);
        var parser = new VtParser(buffer, _ => { });

        buffer.SetCursorPosition(4, 6);
        parser.Process(Encoding.UTF8.GetBytes("\x1Bs"));

        buffer.SetCursorPosition(0, 0);
        parser.Process(Encoding.UTF8.GetBytes("\x1Bu"));

        Assert.That(buffer.CursorRow, Is.EqualTo(4));
        Assert.That(buffer.CursorCol, Is.EqualTo(6));
    }

    // ── Alternate Screen Buffer ─────────────────────────────────────────

    /// <summary>
    /// Mode 1049 should switch to alternate buffer and save cursor.
    /// </summary>
    [Test]
    public void Process_Mode1049_SwitchesAndSavesCursor()
    {
        var buffer = new TerminalBuffer(3, 2);
        var parser = new VtParser(buffer, _ => { });

        buffer.PutChar('A');
        buffer.SetCursorPosition(0, 1);

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1049h"));
        buffer.SetCursorPosition(0, 0);
        buffer.PutChar('B');

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1049l"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
    }

    /// <summary>
    /// Mode 1047 should switch to alternate buffer without saving cursor.
    /// </summary>
    [Test]
    public void Process_Mode1047_SwitchesAlternateBuffer()
    {
        var buffer = new TerminalBuffer(3, 1);
        var parser = new VtParser(buffer, _ => { });

        buffer.PutChar('X');

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1047h"));
        buffer.SetCursorPosition(0, 0);
        buffer.PutChar('Y');

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1047l"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("X"));
    }

    /// <summary>
    /// Alternate screen should be cleared on switch from main.
    /// </summary>
    [Test]
    public void Process_AlternateScreen_ClearedOnSwitch()
    {
        var buffer = new TerminalBuffer(3, 1);
        var parser = new VtParser(buffer, _ => { });

        // Enter alternate, write something, exit
        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1049h"));
        buffer.PutChar('Z');
        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1049l"));

        // Re-enter alternate — it should be clear
        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1049h"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo(" "));
    }

    /// <summary>
    /// Main buffer should be preserved across resize while in alternate buffer.
    /// </summary>
    [Test]
    public void Process_AlternateScreen_ResizePreservesMainBuffer()
    {
        var buffer = new TerminalBuffer(3, 2);
        var parser = new VtParser(buffer, _ => { });

        buffer.PutChar('M');

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1049h"));

        // Resize while in alternate buffer
        buffer.Resize(4, 3);

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1049l"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("M"));
    }

    // ── Full Terminal Reset (ESC c) ─────────────────────────────────────

    /// <summary>
    /// ESC c (RIS) should reset all terminal state comprehensively.
    /// </summary>
    [Test]
    public void Process_Ris_ResetsAllState()
    {
        var buffer = new TerminalBuffer(5, 5);
        var parser = new VtParser(buffer, _ => { });

        // Set up complex state
        buffer.SetCursorPosition(3, 3);
        parser.Process(Encoding.UTF8.GetBytes("\x1B[1;3;4m"));
        parser.Process(Encoding.UTF8.GetBytes("\x1B[38;2;255;0;0m"));
        parser.Process(Encoding.UTF8.GetBytes("\x1B[2;4r"));
        parser.Process(Encoding.UTF8.GetBytes("\x1B[?7l"));
        parser.Process(Encoding.UTF8.GetBytes("\x1B[?2004h"));
        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1004h"));
        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1h"));
        parser.Process(Encoding.UTF8.GetBytes("\x1B(0"));
        parser.Process(Encoding.UTF8.GetBytes("ABCDE"));

        // Full reset
        parser.Process(Encoding.UTF8.GetBytes("\u001Bc"));

        Assert.That(buffer.CursorRow, Is.EqualTo(0));
        Assert.That(buffer.CursorCol, Is.EqualTo(0));
        Assert.That(buffer.AutoWrap, Is.True);
        Assert.That(buffer.BracketedPasteEnabled, Is.False);
        Assert.That(buffer.FocusEventsEnabled, Is.False);
        Assert.That(buffer.ApplicationCursorKeys, Is.False);
        Assert.That(buffer.ReverseVideo, Is.False);
        Assert.That(buffer.OriginMode, Is.False);
        Assert.That(buffer.SgrMouseEnabled, Is.False);
        Assert.That(buffer.MouseTrackingMode, Is.EqualTo(MouseTrackingMode.None));
        Assert.That(buffer.SynchronizedOutput, Is.False);

        // Screen should be cleared
        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo(" "));

        // Writing after reset should use ASCII charset (not line drawing)
        parser.Process(Encoding.UTF8.GetBytes("q"));
        screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("q"));
    }

    // ── TerminalBuffer Operations ───────────────────────────────────────

    /// <summary>
    /// EraseInDisplay with cursor at origin and mode 0 should clear the entire display.
    /// </summary>
    [Test]
    public void Buffer_EraseInDisplayMode0_AtOrigin_ClearsAll()
    {
        var buffer = new TerminalBuffer(3, 2);
        FillRow(buffer, 0, 'A');
        FillRow(buffer, 1, 'B');

        buffer.SetCursorPosition(0, 0);
        buffer.EraseInDisplay(0);

        var screen = buffer.GetScreen();
        for (int r = 0; r < 2; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                Assert.That(screen!.Cells[r, c].Character, Is.EqualTo(" "), $"Cell [{r},{c}]");
            }
        }
    }

    /// <summary>
    /// InsertLines outside scroll region should be a no-op.
    /// </summary>
    [Test]
    public void Buffer_InsertLines_OutsideScrollRegion_IsNoOp()
    {
        var buffer = new TerminalBuffer(3, 5);
        FillRow(buffer, 0, 'A');
        FillRow(buffer, 1, 'B');
        FillRow(buffer, 2, 'C');
        FillRow(buffer, 3, 'D');
        FillRow(buffer, 4, 'E');

        // Set scroll region to rows 1-3
        buffer.SetScrollRegion(1, 3);

        // Position cursor outside the scroll region (row 0)
        buffer.SetCursorPosition(0, 0);
        buffer.InsertLines(1);

        // Content should not have changed
        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[1, 0].Character, Is.EqualTo("B"));
        Assert.That(screen.Cells[2, 0].Character, Is.EqualTo("C"));
    }

    /// <summary>
    /// DeleteLines outside scroll region should be a no-op.
    /// </summary>
    [Test]
    public void Buffer_DeleteLines_OutsideScrollRegion_IsNoOp()
    {
        var buffer = new TerminalBuffer(3, 5);
        FillRow(buffer, 0, 'A');
        FillRow(buffer, 1, 'B');
        FillRow(buffer, 2, 'C');
        FillRow(buffer, 3, 'D');
        FillRow(buffer, 4, 'E');

        buffer.SetScrollRegion(1, 3);

        // Position cursor outside the scroll region (row 4)
        buffer.SetCursorPosition(4, 0);
        buffer.DeleteLines(1);

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[4, 0].Character, Is.EqualTo("E"));
    }

    /// <summary>
    /// ScrollDown should move content down and clear top rows within scroll region.
    /// </summary>
    [Test]
    public void Buffer_ScrollDown_MovesContentDown()
    {
        var buffer = new TerminalBuffer(3, 3);
        FillRow(buffer, 0, 'A');
        FillRow(buffer, 1, 'B');
        FillRow(buffer, 2, 'C');

        buffer.ScrollDown(1);

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[1, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[2, 0].Character, Is.EqualTo("B"));
    }

    /// <summary>
    /// SetScrollRegion should clamp out-of-bounds values.
    /// </summary>
    [Test]
    public void Buffer_SetScrollRegion_ClampsValues()
    {
        var buffer = new TerminalBuffer(3, 5);

        // Set extreme values — should clamp and not throw
        buffer.SetScrollRegion(-1, 100);

        // Cursor should be reset to origin
        Assert.That(buffer.CursorRow, Is.EqualTo(0));
        Assert.That(buffer.CursorCol, Is.EqualTo(0));

        // Content operations should still work
        buffer.PutChar('A');
        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
    }

    // ── DSR 5 ───────────────────────────────────────────────────────────

    /// <summary>
    /// DSR 5 should write back the "terminal OK" status report.
    /// </summary>
    [Test]
    public void Process_Dsr5_WritesStatusReport()
    {
        byte[]? response = null;
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { }, bytes => response = bytes);

        parser.Process(Encoding.UTF8.GetBytes("\x1B[5n"));

        Assert.That(response, Is.Not.Null);
        Assert.That(Encoding.ASCII.GetString(response!), Is.EqualTo("\x1B[0n"));
    }

    // ── CSI CHT (I) Forward Tab ─────────────────────────────────────────

    /// <summary>
    /// CHT (CSI I) with count parameter should advance by multiple tab stops.
    /// </summary>
    [Test]
    public void Process_Cht_AdvancesMultipleTabStops()
    {
        var buffer = new TerminalBuffer(30, 1);
        var parser = new VtParser(buffer, _ => { });

        // Start at col 0, advance 2 tab stops: 0→8→16
        parser.Process(Encoding.UTF8.GetBytes("\x1B[2I"));

        Assert.That(buffer.CursorCol, Is.EqualTo(16));
    }

    /// <summary>
    /// CHT with default parameter should advance one tab stop.
    /// </summary>
    [Test]
    public void Process_Cht_DefaultParam_AdvancesOneTabStop()
    {
        var buffer = new TerminalBuffer(20, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[I"));

        Assert.That(buffer.CursorCol, Is.EqualTo(8));
    }

    // ── Subparameter Handling ───────────────────────────────────────────

    /// <summary>
    /// SGR 4:3 should set undercurl and clear underline.
    /// </summary>
    [Test]
    public void Process_Sgr4Colon3_SetsUndercurl()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[4:3mA"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Undercurl, Is.True);
        Assert.That(screen.Cells[0, 0].Underline, Is.False);
    }

    /// <summary>
    /// SGR 4:0 should turn off both underline and undercurl.
    /// </summary>
    [Test]
    public void Process_Sgr4Colon0_ClearsUnderlineAndUndercurl()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        // Set undercurl first, then 4:0 to clear
        parser.Process(Encoding.UTF8.GetBytes("\x1B[4:3m\x1B[4:0mA"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Undercurl, Is.False);
        Assert.That(screen.Cells[0, 0].Underline, Is.False);
    }

    /// <summary>
    /// SGR 4:1 should set single underline and clear undercurl.
    /// </summary>
    [Test]
    public void Process_Sgr4Colon1_SetsSingleUnderline()
    {
        var buffer = new TerminalBuffer(2, 1);
        var parser = new VtParser(buffer, _ => { });

        // Set undercurl first, then 4:1 for single underline
        parser.Process(Encoding.UTF8.GetBytes("\x1B[4:3m\x1B[4:1mA"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Underline, Is.True);
        Assert.That(screen.Cells[0, 0].Undercurl, Is.False);
    }

    // ── Wide Character Edge Cases ───────────────────────────────────────

    /// <summary>
    /// Wide character at the last column should wrap to the next line.
    /// </summary>
    [Test]
    public void Process_WideCharAtLastCol_Wraps()
    {
        var buffer = new TerminalBuffer(3, 2);
        var parser = new VtParser(buffer, _ => { });

        // Write 'A', 'B' — cursor at col 2 (last col in 3-wide terminal)
        // Then wide char '中' can't fit, should wrap
        parser.Process(Encoding.UTF8.GetBytes("AB"));

        // Now at col 2 (last column). Wide char needs 2 cols — should wrap.
        parser.Process(new byte[] { 0xE4, 0xB8, 0xAD });

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[0, 1].Character, Is.EqualTo("B"));

        // Wide char should be on the next row
        Assert.That(screen.Cells[1, 0].Character, Is.EqualTo("中"));
        Assert.That(screen.Cells[1, 1].Character, Is.Null);
    }

    /// <summary>
    /// Overwriting the first half of a wide character should clear the second half.
    /// </summary>
    [Test]
    public void Process_OverwriteWideCharFirstHalf_ClearsSecondHalf()
    {
        var buffer = new TerminalBuffer(4, 1);

        // Place a wide char at col 0-1
        buffer.PutChar('中');

        // Move back and overwrite col 0 with a narrow char
        buffer.SetCursorPosition(0, 0);
        buffer.PutChar('X');

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("X"));

        // The continuation cell should be cleared to space
        Assert.That(screen.Cells[0, 1].Character, Is.EqualTo(" "));
    }

    /// <summary>
    /// Overwriting the second half of a wide character should clear the first half.
    /// </summary>
    [Test]
    public void Process_OverwriteWideCharSecondHalf_ClearsFirstHalf()
    {
        var buffer = new TerminalBuffer(4, 1);

        // Place a wide char at col 0-1
        buffer.PutChar('中');

        // Move to col 1 (continuation cell) and overwrite
        buffer.SetCursorPosition(0, 1);
        buffer.PutChar('Y');

        var screen = buffer.GetScreen();

        // The first half should be cleared
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[0, 1].Character, Is.EqualTo("Y"));
    }

    // ── Pending Wrap and Auto-Wrap Interactions ─────────────────────────

    /// <summary>
    /// Cursor movement should clear pending wrap state.
    /// </summary>
    [Test]
    public void Process_PendingWrap_ClearedByCursorMovement()
    {
        var buffer = new TerminalBuffer(3, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("ABC"));
        Assert.That(buffer.PendingWrap, Is.True);

        // CUF should clear pending wrap
        parser.Process(Encoding.UTF8.GetBytes("\x1B[C"));
        Assert.That(buffer.PendingWrap, Is.False);
    }

    /// <summary>
    /// CarriageReturn should clear pending wrap state.
    /// </summary>
    [Test]
    public void Process_PendingWrap_ClearedByCarriageReturn()
    {
        var buffer = new TerminalBuffer(3, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("ABC"));
        Assert.That(buffer.PendingWrap, Is.True);

        parser.Process(Encoding.UTF8.GetBytes("\r"));
        Assert.That(buffer.PendingWrap, Is.False);
        Assert.That(buffer.CursorCol, Is.EqualTo(0));
    }

    /// <summary>
    /// Line feed while pending wrap should move to next line.
    /// </summary>
    [Test]
    public void Process_PendingWrap_LineFeedMovesToNextLine()
    {
        var buffer = new TerminalBuffer(3, 3);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("ABC"));
        Assert.That(buffer.PendingWrap, Is.True);

        parser.Process(Encoding.UTF8.GetBytes("\n"));

        // Should be on row 1 after line feed
        Assert.That(buffer.CursorRow, Is.EqualTo(1));
    }

    // ── Multiple DECSET/DECRST in One Sequence ──────────────────────────

    /// <summary>
    /// Multiple modes in a single DECSET sequence should all be enabled.
    /// </summary>
    [Test]
    public void Process_MultipleDecset_EnablesAllModes()
    {
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1;25;2004h"));

        Assert.That(buffer.ApplicationCursorKeys, Is.True);
        Assert.That(buffer.CursorVisible, Is.True);
        Assert.That(buffer.BracketedPasteEnabled, Is.True);
    }

    /// <summary>
    /// Multiple modes in a single DECRST sequence should all be disabled.
    /// </summary>
    [Test]
    public void Process_MultipleDecrst_DisablesAllModes()
    {
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { });

        // Enable first
        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1;2004;1004h"));
        Assert.That(buffer.ApplicationCursorKeys, Is.True);
        Assert.That(buffer.BracketedPasteEnabled, Is.True);
        Assert.That(buffer.FocusEventsEnabled, Is.True);

        // Disable all at once
        parser.Process(Encoding.UTF8.GetBytes("\x1B[?1;2004;1004l"));
        Assert.That(buffer.ApplicationCursorKeys, Is.False);
        Assert.That(buffer.BracketedPasteEnabled, Is.False);
        Assert.That(buffer.FocusEventsEnabled, Is.False);
    }

    // ── ESC Interrupts CSI ──────────────────────────────────────────────

    /// <summary>
    /// ESC received mid-CSI should abandon CSI and start a new escape sequence.
    /// </summary>
    [Test]
    public void Process_EscInterruptsCsi_AbandonsCsiAndStartsEscape()
    {
        var buffer = new TerminalBuffer(5, 3);
        var parser = new VtParser(buffer, _ => { });

        // Start CSI sequence \x1B[2 — but before it finishes, send ESC M (reverse index)
        // \x1B[2\x1BM — the CSI should be abandoned, ESC M should execute
        buffer.SetCursorPosition(1, 0);
        parser.Process(Encoding.UTF8.GetBytes("\x1B[2\x1BM"));

        // RI (ESC M) should have moved cursor up from row 1 to row 0
        Assert.That(buffer.CursorRow, Is.EqualTo(0));
    }

    /// <summary>
    /// ESC during CSI with parameters should abandon and start new sequence.
    /// </summary>
    [Test]
    public void Process_EscDuringCsiWithParams_AbandonsCsi()
    {
        var buffer = new TerminalBuffer(5, 1);
        var parser = new VtParser(buffer, _ => { });

        // Start bold SGR (\x1B[1m) but interrupt with ESC, then write char
        // \x1B[1\x1B[0mA — first CSI is abandoned, second CSI resets, then 'A'
        parser.Process(Encoding.UTF8.GetBytes("\x1B[1m\x1B[1\x1B[0mA"));

        var screen = buffer.GetScreen();

        // The first \x1B[1m sets bold. The interrupted \x1B[1 is abandoned.
        // \x1B[0m resets attributes. 'A' should not be bold.
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[0, 0].Bold, Is.False);
    }

    // ── Private Marker on Non-Mode CSI ──────────────────────────────────

    /// <summary>
    /// Private ED (CSI ? J) should not crash or corrupt state.
    /// </summary>
    [Test]
    public void Process_PrivateEd_DoesNotCorruptState()
    {
        var buffer = new TerminalBuffer(4, 2);
        var parser = new VtParser(buffer, _ => { });

        buffer.PutChar('A');

        // Private ED — should be silently ignored
        parser.Process(Encoding.UTF8.GetBytes("\x1B[?J"));

        // Parser should recover and print normally
        parser.Process(Encoding.UTF8.GetBytes("B"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[0, 1].Character, Is.EqualTo("B"));
    }

    /// <summary>
    /// Greater-than SGR (CSI > m) should not apply SGR attributes.
    /// </summary>
    [Test]
    public void Process_GreaterThanSgr_DoesNotApplyAttributes()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        parser.Process(Encoding.UTF8.GetBytes("\x1B[>1mA"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[0, 0].Bold, Is.False);
    }

    /// <summary>
    /// Private marker on non-mode CSI should not corrupt subsequent parsing.
    /// </summary>
    [Test]
    public void Process_PrivateMarkerOnArbitraryCsi_RecoversParsing()
    {
        var buffer = new TerminalBuffer(4, 1);
        var parser = new VtParser(buffer, _ => { });

        // Unknown private CSI followed by valid bold SGR
        parser.Process(Encoding.UTF8.GetBytes("\x1B[?99z\x1B[1mA"));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[0, 0].Bold, Is.True);
    }

    // ── Helpers ─────────────────────────────────────────────────────────
    private static void FillRow(TerminalBuffer buffer, int row, char value)
    {
        buffer.SetCursorPosition(row, 0);
        for (int c = 0; c < buffer.Cols; c++)
        {
            buffer.PutChar(value);
        }
    }
}
