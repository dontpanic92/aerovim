// <copyright file="TerminalBufferTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests;

using AeroVim.VimClient;
using NUnit.Framework;

/// <summary>
/// Tests terminal buffer state handling.
/// </summary>
public class TerminalBufferTests
{
    /// <summary>
    /// Resize should preserve overlapping cells.
    /// </summary>
    [Test]
    public void Resize_PreservesExistingCells()
    {
        var buffer = new TerminalBuffer(2, 2);
        buffer.PutChar('A');
        buffer.SetCursorPosition(1, 1);
        buffer.PutChar('Z');

        buffer.Resize(4, 3);

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[1, 1].Character, Is.EqualTo("Z"));
        Assert.That(screen.Cells[2, 3].Character, Is.EqualTo(" "));
    }

    /// <summary>
    /// Scroll should move lines up and clear the newly exposed row.
    /// </summary>
    [Test]
    public void ScrollUp_MovesContentAndClearsExposedRow()
    {
        var buffer = new TerminalBuffer(2, 2);
        buffer.PutChar('A');
        buffer.SetCursorPosition(1, 0);
        buffer.PutChar('B');

        buffer.ScrollUp(1);

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("B"));
        Assert.That(screen.Cells[1, 0].Character, Is.EqualTo(" "));
    }
}
