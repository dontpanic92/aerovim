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

    /// <summary>
    /// Wide characters should occupy their leading cell and reserve a continuation cell.
    /// </summary>
    [Test]
    public void PutChar_WideCharacter_UsesContinuationCell()
    {
        var buffer = new TerminalBuffer(3, 1);

        buffer.PutChar('中');

        var screen = buffer.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("中"));
        Assert.That(screen.Cells[0, 1].Character, Is.Null);
    }

    /// <summary>
    /// Switching between main and alternate buffers should preserve the main buffer state.
    /// </summary>
    [Test]
    public void AlternateBuffer_RestoresMainBufferContent()
    {
        var buffer = new TerminalBuffer(2, 1);
        buffer.PutChar('A');
        buffer.SwitchToAlternateBuffer();
        buffer.SetCursorPosition(0, 0);
        buffer.PutChar('B');

        var alternate = buffer.GetScreen();
        string? alternateCharacter = alternate!.Cells[0, 0].Character;

        buffer.SwitchToMainBuffer();
        var main = buffer.GetScreen();

        Assert.That(alternateCharacter, Is.EqualTo("B"));
        Assert.That(main, Is.Not.Null);
        Assert.That(main!.Cells[0, 0].Character, Is.EqualTo("A"));
    }

    /// <summary>
    /// Predominant colors should surface through the screen snapshot once they dominate the grid.
    /// </summary>
    [Test]
    public void GetScreen_DetectsPredominantColors()
    {
        var buffer = new TerminalBuffer(3, 2);
        buffer.SetForegroundColor(0x112233);
        buffer.SetBackgroundColor(0x445566);

        buffer.PutChar('A');
        buffer.PutChar('B');
        buffer.PutChar('C');
        buffer.PutChar('D');

        var screen = buffer.GetScreen();

        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.ForegroundColor, Is.EqualTo(0x112233));
        Assert.That(screen.BackgroundColor, Is.EqualTo(0x445566));
    }

    /// <summary>
    /// Scroll regions should only affect the configured rows.
    /// </summary>
    [Test]
    public void ScrollUp_WithScrollRegion_KeepsRowsOutsideRegionUntouched()
    {
        var buffer = new TerminalBuffer(3, 3);
        FillRow(buffer, 0, 'A');
        FillRow(buffer, 1, 'B');
        FillRow(buffer, 2, 'C');
        buffer.SetScrollRegion(1, 2);

        buffer.ScrollUp(1);

        var screen = buffer.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[1, 0].Character, Is.EqualTo("C"));
        Assert.That(screen.Cells[2, 0].Character, Is.EqualTo(" "));
    }

    private static void FillRow(TerminalBuffer buffer, int row, char value)
    {
        buffer.SetCursorPosition(row, 0);
        buffer.PutChar(value);
        buffer.PutChar(value);
        buffer.PutChar(value);
    }
}
