// <copyright file="VimClientTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests;

using System.Text;
using AeroVim.Tests.Helpers;
using AeroVim.VimClient;
using NUnit.Framework;

/// <summary>
/// Tests Vim PTY client behavior with a fake PTY.
/// </summary>
public class VimClientTests
{
    /// <summary>
    /// Resizing with an attached PTY should update both the buffer and the PTY connection.
    /// </summary>
    [Test]
    public void TryResize_WithAttachedPty_ResizesBufferAndPty()
    {
        var ptyConnection = new FakePtyConnection();
        using var client = new VimClient("vim", ptyConnection);

        client.TryResize(100, 40);
        client.ProcessOutputForTesting(Encoding.UTF8.GetBytes("A"));

        var screen = client.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells.GetLength(0), Is.EqualTo(40));
        Assert.That(screen.Cells.GetLength(1), Is.EqualTo(100));
        Assert.That(ptyConnection.LastResize, Is.EqualTo((100, 40)));
    }

    /// <summary>
    /// PTY output should update screen state, title, colors, and redraw notifications.
    /// </summary>
    [Test]
    public void ProcessOutputForTesting_UpdatesScreenStateAndRaisesEvents()
    {
        var ptyConnection = new FakePtyConnection();
        using var client = new VimClient("vim", ptyConnection);
        var titles = new List<string>();
        var foregrounds = new List<int>();
        var backgrounds = new List<int>();
        int redrawCount = 0;

        client.TitleChanged += titles.Add;
        client.ForegroundColorChanged += foregrounds.Add;
        client.BackgroundColorChanged += backgrounds.Add;
        client.Redraw += () => redrawCount++;

        client.ProcessOutputForTesting(Encoding.UTF8.GetBytes("\x1B]2;Editor\x1B\\"));
        client.ProcessOutputForTesting(Encoding.UTF8.GetBytes("\x1B[31;42m" + new string('A', 1200)));

        var screen = client.GetScreen();

        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(titles, Is.EqualTo(new[] { "Editor" }));
        Assert.That(foregrounds, Is.EqualTo(new[] { 0xCC0000 }));
        Assert.That(backgrounds, Is.EqualTo(new[] { 0x00CC00 }));
        Assert.That(redrawCount, Is.EqualTo(2));
    }

    /// <summary>
    /// Mouse input should be encoded using SGR mouse sequences.
    /// </summary>
    [Test]
    public void InputMouse_LeftPressWithModifiers_WritesExpectedSgrSequence()
    {
        var ptyConnection = new FakePtyConnection();
        using var client = new VimClient("vim", ptyConnection);

        client.InputMouse("left", "press", "C-S", 0, 4, 2);

        Assert.That(ptyConnection.GetWrittenText(), Is.EqualTo("\x1B[<20;3;5M"));
    }

    /// <summary>
    /// Commands should be sent through command-line mode once the PTY is attached.
    /// </summary>
    [Test]
    public void Command_WithAttachedPty_WritesEscapedCommandSequence()
    {
        var ptyConnection = new FakePtyConnection();
        using var client = new VimClient("vim", ptyConnection);

        client.Command("set number");

        Assert.That(ptyConnection.GetWrittenText(), Is.EqualTo("\x1B:set number\r"));
    }
}
