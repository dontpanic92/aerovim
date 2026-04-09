// <copyright file="TerminalInputEncoderTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests;

using AeroVim.VimClient;
using NUnit.Framework;

/// <summary>
/// Tests for <see cref="TerminalInputEncoder"/> which converts Vim-notation
/// key sequences to terminal escape sequences for PTY input.
/// </summary>
public class TerminalInputEncoderTests
{
    // ── Basic special keys ──────────────────────────────────────────────

    /// <summary>
    /// Unmodified ESC should produce the ESC byte.
    /// </summary>
    [Test]
    public void Encode_Esc_ProducesEscByte()
    {
        Assert.That(TerminalInputEncoder.Encode("<Esc>"), Is.EqualTo("\x1B"));
    }

    /// <summary>
    /// CR, Return, and Enter should all produce carriage return.
    /// </summary>
    [Test]
    public void Encode_CR_ProducesCarriageReturn()
    {
        Assert.That(TerminalInputEncoder.Encode("<CR>"), Is.EqualTo("\r"));
        Assert.That(TerminalInputEncoder.Encode("<Return>"), Is.EqualTo("\r"));
        Assert.That(TerminalInputEncoder.Encode("<Enter>"), Is.EqualTo("\r"));
    }

    /// <summary>
    /// NL should produce a newline byte.
    /// </summary>
    [Test]
    public void Encode_NL_ProducesNewline()
    {
        Assert.That(TerminalInputEncoder.Encode("<NL>"), Is.EqualTo("\n"));
    }

    /// <summary>
    /// BS should produce DEL (0x7F).
    /// </summary>
    [Test]
    public void Encode_BS_ProducesDel()
    {
        Assert.That(TerminalInputEncoder.Encode("<BS>"), Is.EqualTo("\x7F"));
    }

    /// <summary>
    /// Tab should produce a horizontal tab character.
    /// </summary>
    [Test]
    public void Encode_Tab_ProducesTab()
    {
        Assert.That(TerminalInputEncoder.Encode("<Tab>"), Is.EqualTo("\t"));
    }

    /// <summary>
    /// Space should produce a space character.
    /// </summary>
    [Test]
    public void Encode_Space_ProducesSpace()
    {
        Assert.That(TerminalInputEncoder.Encode("<Space>"), Is.EqualTo(" "));
    }

    /// <summary>
    /// &lt;lt&gt; should produce a literal less-than character.
    /// </summary>
    [Test]
    public void Encode_Lt_ProducesLessThan()
    {
        Assert.That(TerminalInputEncoder.Encode("<lt>"), Is.EqualTo("<"));
    }

    // ── Arrow and cursor keys ───────────────────────────────────────────

    /// <summary>
    /// Unmodified arrow keys produce CSI sequences in normal mode.
    /// </summary>
    [Test]
    public void Encode_ArrowKeys_ProduceCsiSequences()
    {
        Assert.That(TerminalInputEncoder.Encode("<Up>"), Is.EqualTo("\x1B[A"));
        Assert.That(TerminalInputEncoder.Encode("<Down>"), Is.EqualTo("\x1B[B"));
        Assert.That(TerminalInputEncoder.Encode("<Right>"), Is.EqualTo("\x1B[C"));
        Assert.That(TerminalInputEncoder.Encode("<Left>"), Is.EqualTo("\x1B[D"));
        Assert.That(TerminalInputEncoder.Encode("<Home>"), Is.EqualTo("\x1B[H"));
        Assert.That(TerminalInputEncoder.Encode("<End>"), Is.EqualTo("\x1B[F"));
    }

    /// <summary>
    /// Application cursor mode (DECCKM) emits SS3 for unmodified arrow keys.
    /// </summary>
    [Test]
    public void Encode_ApplicationCursorKeys_EmitsSS3()
    {
        Assert.That(TerminalInputEncoder.Encode("<Up>", true), Is.EqualTo("\x1BOA"));
        Assert.That(TerminalInputEncoder.Encode("<Down>", true), Is.EqualTo("\x1BOB"));
        Assert.That(TerminalInputEncoder.Encode("<Home>", true), Is.EqualTo("\x1BOH"));
    }

    /// <summary>
    /// Modified arrow keys use CSI 1;{mod}{final} even in DECCKM mode.
    /// </summary>
    [Test]
    public void Encode_ModifiedArrowKeys_IgnoresDecckm()
    {
        Assert.That(TerminalInputEncoder.Encode("<C-Up>", true), Is.EqualTo("\x1B[1;5A"));
        Assert.That(TerminalInputEncoder.Encode("<S-Left>"), Is.EqualTo("\x1B[1;2D"));
        Assert.That(TerminalInputEncoder.Encode("<A-Right>"), Is.EqualTo("\x1B[1;3C"));
        Assert.That(TerminalInputEncoder.Encode("<C-S-Home>"), Is.EqualTo("\x1B[1;6H"));
    }

    // ── Modified function keys (F1-F12) ─────────────────────────────────

    /// <summary>
    /// Unmodified F1-F4 produce SS3 sequences and F5-F12 produce tilde sequences.
    /// </summary>
    [Test]
    public void Encode_FunctionKeys_Unmodified()
    {
        Assert.That(TerminalInputEncoder.Encode("<F1>"), Is.EqualTo("\x1BOP"));
        Assert.That(TerminalInputEncoder.Encode("<F4>"), Is.EqualTo("\x1BOS"));
        Assert.That(TerminalInputEncoder.Encode("<F5>"), Is.EqualTo("\x1B[15~"));
        Assert.That(TerminalInputEncoder.Encode("<F12>"), Is.EqualTo("\x1B[24~"));
    }

    /// <summary>
    /// Modified F1-F4 switch from SS3 to CSI 1;{mod}{final} format.
    /// </summary>
    [Test]
    public void Encode_ModifiedF1ToF4_UseCsiFormat()
    {
        Assert.That(TerminalInputEncoder.Encode("<S-F1>"), Is.EqualTo("\x1B[1;2P"));
        Assert.That(TerminalInputEncoder.Encode("<C-F3>"), Is.EqualTo("\x1B[1;5R"));
        Assert.That(TerminalInputEncoder.Encode("<A-F4>"), Is.EqualTo("\x1B[1;3S"));
        Assert.That(TerminalInputEncoder.Encode("<C-S-F2>"), Is.EqualTo("\x1B[1;6Q"));
    }

    /// <summary>
    /// Modified F5-F12 use CSI {num};{mod}~ format.
    /// </summary>
    [Test]
    public void Encode_ModifiedF5ToF12_UseTildeFormat()
    {
        Assert.That(TerminalInputEncoder.Encode("<S-F5>"), Is.EqualTo("\x1B[15;2~"));
        Assert.That(TerminalInputEncoder.Encode("<C-F6>"), Is.EqualTo("\x1B[17;5~"));
        Assert.That(TerminalInputEncoder.Encode("<A-F12>"), Is.EqualTo("\x1B[24;3~"));
        Assert.That(TerminalInputEncoder.Encode("<C-S-A-F9>"), Is.EqualTo("\x1B[20;8~"));
    }

    // ── Modified editing keys ───────────────────────────────────────────

    /// <summary>
    /// Modified Insert/Delete/PageUp/PageDown use CSI {num};{mod}~ format.
    /// </summary>
    [Test]
    public void Encode_ModifiedEditingKeys_UseTildeFormat()
    {
        Assert.That(TerminalInputEncoder.Encode("<S-Insert>"), Is.EqualTo("\x1B[2;2~"));
        Assert.That(TerminalInputEncoder.Encode("<C-Del>"), Is.EqualTo("\x1B[3;5~"));
        Assert.That(TerminalInputEncoder.Encode("<S-PageUp>"), Is.EqualTo("\x1B[5;2~"));
        Assert.That(TerminalInputEncoder.Encode("<C-S-PageDown>"), Is.EqualTo("\x1B[6;6~"));
    }

    // ── Shift+Tab (back-tab) ────────────────────────────────────────────

    /// <summary>
    /// Shift+Tab should produce back-tab (CSI Z).
    /// </summary>
    [Test]
    public void Encode_ShiftTab_ProducesBackTab()
    {
        Assert.That(TerminalInputEncoder.Encode("<S-Tab>"), Is.EqualTo("\x1B[Z"));
    }

    /// <summary>
    /// Ctrl+Shift+Tab uses xterm modifier format.
    /// </summary>
    [Test]
    public void Encode_CtrlShiftTab_UsesXtermModifier()
    {
        // modifier 6 = Ctrl+Shift
        Assert.That(TerminalInputEncoder.Encode("<C-S-Tab>"), Is.EqualTo("\x1B[1;6Z"));
    }

    // ── Ctrl + single character ─────────────────────────────────────────

    /// <summary>
    /// Ctrl + letter produces the corresponding control byte (C-a = 0x01, C-z = 0x1A).
    /// </summary>
    [Test]
    public void Encode_CtrlLetter_ProducesControlByte()
    {
        Assert.That(TerminalInputEncoder.Encode("<C-a>"), Is.EqualTo("\x01"));
        Assert.That(TerminalInputEncoder.Encode("<C-c>"), Is.EqualTo("\x03"));
        Assert.That(TerminalInputEncoder.Encode("<C-z>"), Is.EqualTo("\x1A"));
    }

    /// <summary>
    /// Ctrl + special ASCII characters produce the expected control bytes.
    /// </summary>
    [Test]
    public void Encode_CtrlSpecialChars_ProducesControlBytes()
    {
        Assert.That(TerminalInputEncoder.Encode("<C-@>"), Is.EqualTo("\x00"));
        Assert.That(TerminalInputEncoder.Encode("<C-[>"), Is.EqualTo("\x1B"));
        Assert.That(TerminalInputEncoder.Encode("<C-]>"), Is.EqualTo("\x1D"));
    }

    /// <summary>
    /// Ctrl+Shift+letter produces the same control byte as Ctrl+letter
    /// (terminals cannot distinguish these).
    /// </summary>
    [Test]
    public void Encode_CtrlShiftLetter_SameAsCtrlLetter()
    {
        Assert.That(TerminalInputEncoder.Encode("<C-S-a>"), Is.EqualTo("\x01"));
        Assert.That(TerminalInputEncoder.Encode("<C-S-z>"), Is.EqualTo("\x1A"));
    }

    // ── Ctrl+Alt + single character ─────────────────────────────────────

    /// <summary>
    /// Ctrl+Alt+letter produces ESC + the Ctrl byte.
    /// </summary>
    [Test]
    public void Encode_CtrlAltLetter_ProducesEscPlusCtrlByte()
    {
        Assert.That(TerminalInputEncoder.Encode("<C-A-a>"), Is.EqualTo("\u001B\x01"));
        Assert.That(TerminalInputEncoder.Encode("<C-A-c>"), Is.EqualTo("\u001B\x03"));
    }

    // ── Alt + single character ──────────────────────────────────────────

    /// <summary>
    /// Alt + letter produces ESC + the letter.
    /// </summary>
    [Test]
    public void Encode_AltLetter_ProducesEscPlusChar()
    {
        Assert.That(TerminalInputEncoder.Encode("<A-a>"), Is.EqualTo("\u001Ba"));
        Assert.That(TerminalInputEncoder.Encode("<A-x>"), Is.EqualTo("\x1Bx"));
    }

    /// <summary>
    /// Alt+Shift + letter produces ESC + the uppercase letter.
    /// </summary>
    [Test]
    public void Encode_AltShiftLetter_ProducesEscPlusUpperChar()
    {
        Assert.That(TerminalInputEncoder.Encode("<A-S-a>"), Is.EqualTo("\u001BA"));
    }

    // ── Alt + multi-char special keys ───────────────────────────────────

    /// <summary>
    /// Alt + CR produces ESC + carriage return.
    /// </summary>
    [Test]
    public void Encode_AltCR_ProducesEscPlusCR()
    {
        Assert.That(TerminalInputEncoder.Encode("<A-CR>"), Is.EqualTo("\x1B\r"));
    }

    /// <summary>
    /// Alt + BS produces ESC + DEL (0x7F).
    /// </summary>
    [Test]
    public void Encode_AltBS_ProducesEscPlusDel()
    {
        Assert.That(TerminalInputEncoder.Encode("<A-BS>"), Is.EqualTo("\x1B\x7F"));
    }

    /// <summary>
    /// Alt + Tab sends xterm-modified Tab format (CSI 1;3 Z).
    /// </summary>
    [Test]
    public void Encode_AltTab_ProducesModifiedTabSequence()
    {
        Assert.That(TerminalInputEncoder.Encode("<A-Tab>"), Is.EqualTo("\x1B[1;3Z"));
    }

    /// <summary>
    /// Alt + Esc produces ESC + ESC (double ESC).
    /// </summary>
    [Test]
    public void Encode_AltEsc_ProducesDoubleEsc()
    {
        Assert.That(TerminalInputEncoder.Encode("<A-Esc>"), Is.EqualTo("\u001B\x1B"));
    }

    /// <summary>
    /// Alt + Space produces ESC + space.
    /// </summary>
    [Test]
    public void Encode_AltSpace_ProducesEscPlusSpace()
    {
        Assert.That(TerminalInputEncoder.Encode("<A-Space>"), Is.EqualTo("\x1B "));
    }

    // ── Regular character passthrough ───────────────────────────────────

    /// <summary>
    /// ASCII characters pass through unchanged.
    /// </summary>
    [Test]
    public void Encode_AsciiChar_PassesThrough()
    {
        Assert.That(TerminalInputEncoder.Encode("a"), Is.EqualTo("a"));
        Assert.That(TerminalInputEncoder.Encode("Z"), Is.EqualTo("Z"));
        Assert.That(TerminalInputEncoder.Encode("1"), Is.EqualTo("1"));
    }

    /// <summary>
    /// CJK characters pass through unchanged.
    /// </summary>
    [Test]
    public void Encode_CjkChar_PassesThrough()
    {
        Assert.That(TerminalInputEncoder.Encode("你"), Is.EqualTo("你"));
        Assert.That(TerminalInputEncoder.Encode("好"), Is.EqualTo("好"));
        Assert.That(TerminalInputEncoder.Encode("世"), Is.EqualTo("世"));
    }

    /// <summary>
    /// Emoji (surrogate pairs) pass through unchanged.
    /// </summary>
    [Test]
    public void Encode_Emoji_PassesThrough()
    {
        Assert.That(TerminalInputEncoder.Encode("😀"), Is.EqualTo("😀"));
    }

    // ── Hardened <...> pattern check ────────────────────────────────────

    /// <summary>
    /// Raw text that starts with &lt; and ends with &gt; (e.g. from bracketed
    /// paste) should NOT be misidentified as a Vim key notation.
    /// </summary>
    [Test]
    public void Encode_HtmlLikeText_PassesThroughUnmodified()
    {
        string html = "<div>Hello</div>";
        Assert.That(TerminalInputEncoder.Encode(html), Is.EqualTo(html));
    }

    /// <summary>
    /// Text with non-ASCII content inside angle brackets should pass through.
    /// </summary>
    [Test]
    public void Encode_CjkInAngleBrackets_PassesThroughUnmodified()
    {
        string text = "<你好>";
        Assert.That(TerminalInputEncoder.Encode(text), Is.EqualTo(text));
    }

    /// <summary>
    /// Very long strings in angle brackets should pass through.
    /// </summary>
    [Test]
    public void Encode_LongAngleBracketText_PassesThroughUnmodified()
    {
        string text = "<this-is-a-very-long-fake-key-name-that-is-definitely-not-valid>";
        Assert.That(TerminalInputEncoder.Encode(text), Is.EqualTo(text));
    }

    /// <summary>
    /// Text with whitespace inside angle brackets should pass through.
    /// </summary>
    [Test]
    public void Encode_WhitespaceInAngleBrackets_PassesThroughUnmodified()
    {
        string text = "<hello world>";
        Assert.That(TerminalInputEncoder.Encode(text), Is.EqualTo(text));
    }

    /// <summary>
    /// Multi-line text should always pass through, even if it starts with
    /// &lt; and ends with &gt;.
    /// </summary>
    [Test]
    public void Encode_MultilineText_PassesThroughUnmodified()
    {
        string text = "<line1\nline2>";
        Assert.That(TerminalInputEncoder.Encode(text), Is.EqualTo(text));
    }

    /// <summary>
    /// Empty and null input should return empty string.
    /// </summary>
    [Test]
    public void Encode_EmptyInput_ReturnsEmpty()
    {
        Assert.That(TerminalInputEncoder.Encode(string.Empty), Is.EqualTo(string.Empty));
        Assert.That(TerminalInputEncoder.Encode(null!), Is.EqualTo(string.Empty));
    }

    // ── Xterm modifier values ───────────────────────────────────────────

    /// <summary>
    /// Verifies the xterm modifier encoding: 2=Shift, 3=Alt, 4=Shift+Alt,
    /// 5=Ctrl, 6=Ctrl+Shift, 7=Ctrl+Alt, 8=Ctrl+Shift+Alt.
    /// </summary>
    [Test]
    public void Encode_AllModifierCombinations_CorrectValues()
    {
        // Shift=2
        Assert.That(TerminalInputEncoder.Encode("<S-Up>"), Is.EqualTo("\x1B[1;2A"));

        // Alt=3
        Assert.That(TerminalInputEncoder.Encode("<A-Up>"), Is.EqualTo("\x1B[1;3A"));

        // Shift+Alt=4
        Assert.That(TerminalInputEncoder.Encode("<S-A-Up>"), Is.EqualTo("\x1B[1;4A"));

        // Ctrl=5
        Assert.That(TerminalInputEncoder.Encode("<C-Up>"), Is.EqualTo("\x1B[1;5A"));

        // Ctrl+Shift=6
        Assert.That(TerminalInputEncoder.Encode("<C-S-Up>"), Is.EqualTo("\x1B[1;6A"));

        // Ctrl+Alt=7
        Assert.That(TerminalInputEncoder.Encode("<C-A-Up>"), Is.EqualTo("\x1B[1;7A"));

        // Ctrl+Shift+Alt=8
        Assert.That(TerminalInputEncoder.Encode("<C-S-A-Up>"), Is.EqualTo("\x1B[1;8A"));
    }
}
