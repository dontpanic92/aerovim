// <copyright file="TerminalBufferTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests;

using AeroVim.Editor.Utilities;
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
    /// Predominant background color should surface through the screen snapshot
    /// and foreground should be derived from it for readable chrome text.
    /// </summary>
    [Test]
    public void GetScreen_DetectsPredominantBackground_DerivesForeground()
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
        Assert.That(screen!.BackgroundColor, Is.EqualTo(0x445566));

        // Foreground is derived from bg, not detected from cell fg.
        Assert.That(screen.ForegroundColor, Is.EqualTo(ColorUtility.DeriveReadableForeground(0x445566)));
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

    /// <summary>
    /// Initial GetScreen should report all rows dirty.
    /// </summary>
    [Test]
    public void GetScreen_AfterConstruction_ReportsAllDirty()
    {
        var buffer = new TerminalBuffer(3, 2);
        buffer.PutChar('X');

        var screen = buffer.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.AllDirty, Is.True);
    }

    /// <summary>
    /// A partial update should report only the affected row as dirty.
    /// </summary>
    [Test]
    public void GetScreen_AfterPartialUpdate_ReportsOnlyDirtyRows()
    {
        var buffer = new TerminalBuffer(3, 3);
        FillRow(buffer, 0, 'A');
        FillRow(buffer, 1, 'B');
        FillRow(buffer, 2, 'C');

        // First snapshot consumes all dirty flags.
        buffer.GetScreen();

        // Update only row 1.
        buffer.SetCursorPosition(1, 0);
        buffer.PutChar('Z');

        var screen = buffer.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.AllDirty, Is.False);
        Assert.That(screen.DirtyRows, Is.Not.Null);
        Assert.That(screen.DirtyRows![0], Is.False);
        Assert.That(screen.DirtyRows[1], Is.True);
        Assert.That(screen.DirtyRows[2], Is.False);
    }

    /// <summary>
    /// A second GetScreen with no intervening changes should report nothing dirty.
    /// </summary>
    [Test]
    public void GetScreen_WithNoChanges_ReportsNothingDirty()
    {
        var buffer = new TerminalBuffer(2, 2);
        buffer.PutChar('A');

        buffer.GetScreen();

        var screen = buffer.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.AllDirty, Is.False);
        Assert.That(screen.DirtyRows, Is.Not.Null);
        Assert.That(screen.DirtyRows!.Any(d => d), Is.False);
    }

    /// <summary>
    /// Resize should report AllDirty in the subsequent snapshot.
    /// </summary>
    [Test]
    public void GetScreen_AfterResize_ReportsAllDirty()
    {
        var buffer = new TerminalBuffer(2, 2);
        buffer.PutChar('A');

        buffer.GetScreen();
        buffer.Resize(4, 3);

        var screen = buffer.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.AllDirty, Is.True);
    }

    /// <summary>
    /// With auto-wrap on, pending wrap should defer the actual wrap until the next character.
    /// </summary>
    [Test]
    public void PutChar_PendingWrap_DefersWrapUntilNextChar()
    {
        var buffer = new TerminalBuffer(3, 2);
        buffer.PutChar('A');
        buffer.PutChar('B');
        buffer.PutChar('C'); // fills last column — should set pending wrap

        Assert.That(buffer.PendingWrap, Is.True);
        Assert.That(buffer.CursorRow, Is.EqualTo(0));

        buffer.PutChar('D'); // this triggers the actual wrap

        Assert.That(buffer.PendingWrap, Is.False);
        Assert.That(buffer.CursorRow, Is.EqualTo(1));
        Assert.That(buffer.CursorCol, Is.EqualTo(1));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 2].Character, Is.EqualTo("C"));
        Assert.That(screen.Cells[1, 0].Character, Is.EqualTo("D"));
    }

    /// <summary>
    /// With auto-wrap off, writing at the last column should overwrite in place.
    /// </summary>
    [Test]
    public void PutChar_AutoWrapOff_OverwritesLastColumn()
    {
        var buffer = new TerminalBuffer(3, 2);
        buffer.AutoWrap = false;
        buffer.PutChar('A');
        buffer.PutChar('B');
        buffer.PutChar('C'); // at last column
        buffer.PutChar('D'); // should overwrite column 2

        Assert.That(buffer.CursorRow, Is.EqualTo(0));

        var screen = buffer.GetScreen();
        Assert.That(screen!.Cells[0, 2].Character, Is.EqualTo("D"));
    }

    /// <summary>
    /// Cursor movement should clear pending wrap state.
    /// </summary>
    [Test]
    public void SetCursorPosition_ClearsPendingWrap()
    {
        var buffer = new TerminalBuffer(3, 2);
        buffer.PutChar('A');
        buffer.PutChar('B');
        buffer.PutChar('C');
        Assert.That(buffer.PendingWrap, Is.True);

        buffer.SetCursorPosition(0, 0);
        Assert.That(buffer.PendingWrap, Is.False);
    }

    private static void FillRow(TerminalBuffer buffer, int row, char value)
    {
        buffer.SetCursorPosition(row, 0);
        buffer.PutChar(value);
        buffer.PutChar(value);
        buffer.PutChar(value);
    }
}
