// <copyright file="NeovimClientTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests;

using AeroVim.Editor.Utilities;
using AeroVim.NeovimClient;
using AeroVim.NeovimClient.Events;
using NUnit.Framework;

/// <summary>
/// Tests Neovim client screen-state handling.
/// </summary>
public class NeovimClientTests
{
    /// <summary>
    /// Redraw events should update the screen and raise the corresponding callbacks.
    /// </summary>
    [Test]
    public void ProcessRedrawForTesting_AppliesScreenUpdatesAndRaisesCallbacks()
    {
        var client = new NeovimClient();
        var titles = new List<string>();
        var foregrounds = new List<int>();
        var backgrounds = new List<int>();
        var fonts = new List<FontSettings>();
        int redrawCount = 0;

        client.TitleChanged += titles.Add;
        client.ForegroundColorChanged += foregrounds.Add;
        client.BackgroundColorChanged += backgrounds.Add;
        client.FontChanged += fonts.Add;
        client.Redraw += () => redrawCount++;

        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new ResizeEvent(2, 3),
            new UpdateFgEvent(0x112233),
            new UpdateBgEvent(0x445566),
            new HighlightSetEvent(0xABCDEF, 0x123456, 0x654321, reverse: false, italic: true, bold: true, underline: true, undercurl: false),
            new CursorGotoEvent(0, 0),
            new PutEvent(new string?[] { "A" }),
            new SetTitleEvent("AeroVim"),
            new GuiFontEvent("Cascadia_Code,Fira_Code:h13:b"),
        });

        var screen = client.GetScreen();

        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(screen.Cells[0, 0].ForegroundColor, Is.EqualTo(0xABCDEF));
        Assert.That(screen.Cells[0, 0].BackgroundColor, Is.EqualTo(0x123456));
        Assert.That(screen.Cells[0, 0].Bold, Is.True);
        Assert.That(screen.Cells[0, 0].Italic, Is.True);
        Assert.That(screen.Cells[0, 0].Underline, Is.True);
        Assert.That(screen.ForegroundColor, Is.EqualTo(0x112233));
        Assert.That(screen.BackgroundColor, Is.EqualTo(0x445566));
        Assert.That(titles, Is.EqualTo(new[] { "AeroVim" }));
        Assert.That(foregrounds, Is.EqualTo(new[] { 0x112233 }));
        Assert.That(backgrounds, Is.EqualTo(new[] { 0x445566 }));
        Assert.That(fonts, Has.Count.EqualTo(1));
        Assert.That(fonts[0].FontNames, Is.EqualTo(new[] { "Cascadia Code", "Fira Code" }));
        Assert.That(fonts[0].FontPointSize, Is.EqualTo(13).Within(0.001));
        Assert.That(fonts[0].Bold, Is.True);
        Assert.That(redrawCount, Is.EqualTo(1));
    }

    /// <summary>
    /// Dirty-row copies should preserve untouched rows in the shared screen snapshot.
    /// </summary>
    [Test]
    public void GetScreen_WhenOnlyOneRowChanges_KeepsUntouchedRows()
    {
        var client = new NeovimClient();
        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new ResizeEvent(2, 3),
            new CursorGotoEvent(0, 0),
            new PutEvent(new string?[] { "A" }),
            new CursorGotoEvent(1, 0),
            new PutEvent(new string?[] { "B" }),
        });

        var first = client.GetScreen();
        Assert.That(first, Is.Not.Null);

        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new CursorGotoEvent(0, 1),
            new PutEvent(new string?[] { "C" }),
        });

        var second = client.GetScreen();
        Assert.That(second, Is.Not.Null);
        Assert.That(second!.Cells[0, 0].Character, Is.EqualTo("A"));
        Assert.That(second.Cells[0, 1].Character, Is.EqualTo("C"));
        Assert.That(second.Cells[1, 0].Character, Is.EqualTo("B"));
    }

    /// <summary>
    /// Mode and mouse capability events should update the exposed client state.
    /// </summary>
    [Test]
    public void ProcessRedrawForTesting_UpdatesModeInfoAndMouseCapability()
    {
        var client = new NeovimClient();
        var modeInfo = new Dictionary<string, string>
        {
            ["cursor_shape"] = "vertical",
            ["cell_percentage"] = "25",
            ["blinkon"] = "1",
            ["mouse_shape"] = "beam",
        };

        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new ModeInfoSetEvent(false, new List<IDictionary<string, string>> { modeInfo }),
            new ModeChangeEvent("insert", 0),
            new MouseOffEvent(),
        });

        Assert.That(client.ModeInfo, Is.Not.Null);
        Assert.That(client.ModeInfo!.CursorShape, Is.EqualTo(CursorShape.Vertical));
        Assert.That(client.ModeInfo.CellPercentage, Is.EqualTo(25));
        Assert.That(client.ModeInfo.CursorBlinking, Is.EqualTo(CursorBlinking.BlinkOn));
        Assert.That(client.ModeInfo.PointerShape, Is.EqualTo("beam"));
        Assert.That(client.ModeInfo.CursorStyleEnabled, Is.False);
        Assert.That(client.ModeInfo.CursorVisible, Is.True);
        Assert.That(client.ModeInfo.PointerMode, Is.EqualTo(0));
        Assert.That(client.MouseEnabled, Is.False);

        client.ProcessRedrawForTesting(new IRedrawEvent[] { new MouseOnEvent() });

        Assert.That(client.MouseEnabled, Is.True);
    }

    /// <summary>
    /// Initial GetScreen after resize should report AllDirty.
    /// </summary>
    [Test]
    public void GetScreen_AfterResize_ReportsAllDirty()
    {
        var client = new NeovimClient();
        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new ResizeEvent(2, 3),
            new CursorGotoEvent(0, 0),
            new PutEvent(new string?[] { "A" }),
        });

        var screen = client.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.AllDirty, Is.True);
    }

    /// <summary>
    /// A partial update should mark only the affected rows dirty.
    /// </summary>
    [Test]
    public void GetScreen_AfterPartialUpdate_ReportsOnlyDirtyRows()
    {
        var client = new NeovimClient();
        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new ResizeEvent(4, 3),
            new CursorGotoEvent(0, 0),
            new PutEvent(new string?[] { "A" }),
            new CursorGotoEvent(1, 0),
            new PutEvent(new string?[] { "B" }),
            new CursorGotoEvent(2, 0),
            new PutEvent(new string?[] { "C" }),
        });

        // First snapshot consumes all dirty flags.
        client.GetScreen();

        // Only modify row 1.
        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new CursorGotoEvent(1, 1),
            new PutEvent(new string?[] { "Z" }),
        });

        var screen = client.GetScreen();
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
        var client = new NeovimClient();
        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new ResizeEvent(2, 3),
            new CursorGotoEvent(0, 0),
            new PutEvent(new string?[] { "A" }),
        });

        client.GetScreen();

        var screen = client.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.AllDirty, Is.False);
        Assert.That(screen.DirtyRows, Is.Not.Null);
        Assert.That(screen.DirtyRows!.Any(d => d), Is.False);
    }
}
