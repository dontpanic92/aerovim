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
}
