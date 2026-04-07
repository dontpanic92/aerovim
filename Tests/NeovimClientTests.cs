// <copyright file="NeovimClientTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests;

using AeroVim.Editor;
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
            new HighlightSetEvent(0xABCDEF, 0x123456, 0x654321, Reverse: false, Italic: true, Bold: true, Underline: true, Undercurl: false),
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

    /// <summary>
    /// ext_linegrid: grid_resize + grid_line should build cells with resolved highlight attributes.
    /// </summary>
    [Test]
    public void ExtLinegrid_GridLine_ResolvesHighlightAttributes()
    {
        var client = new NeovimClient();
        var attrs = new AeroVim.Editor.Utilities.HighlightAttributes
        {
            Foreground = 0xFF0000,
            Background = 0x00FF00,
            Bold = true,
            Italic = true,
        };

        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new DefaultColorsSetEvent(0xAAAAAA, 0xBBBBBB, 0xCCCCCC, 0, 0),
            new HlAttrDefineEvent(1, attrs),
            new GridResizeEvent(1, 4, 2),
            new GridLineEvent(
                1,
                0,
                0,
                new[]
                {
                    new GridLineCell("H", 1, 1),
                    new GridLineCell("i", null, 1),
                },
                false),
            new FlushEvent(),
        });

        var screen = client.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("H"));
        Assert.That(screen.Cells[0, 0].ForegroundColor, Is.EqualTo(0xFF0000));
        Assert.That(screen.Cells[0, 0].BackgroundColor, Is.EqualTo(0x00FF00));
        Assert.That(screen.Cells[0, 0].Bold, Is.True);
        Assert.That(screen.Cells[0, 0].Italic, Is.True);
        Assert.That(screen.Cells[0, 1].Character, Is.EqualTo("i"));
        Assert.That(screen.Cells[0, 1].ForegroundColor, Is.EqualTo(0xFF0000));
        Assert.That(screen.Cells[0, 1].Bold, Is.True);
    }

    /// <summary>
    /// ext_linegrid: grid_line cells with repeat > 1 should fill multiple columns.
    /// </summary>
    [Test]
    public void ExtLinegrid_GridLine_RepeatFillsMultipleColumns()
    {
        var client = new NeovimClient();

        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new DefaultColorsSetEvent(0x000000, 0xFFFFFF, 0x000000, 0, 0),
            new GridResizeEvent(1, 5, 1),
            new GridLineEvent(
                1,
                0,
                0,
                new[]
                {
                    new GridLineCell("X", 0, 3),
                    new GridLineCell("Y", 0, 2),
                },
                false),
            new FlushEvent(),
        });

        var screen = client.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("X"));
        Assert.That(screen.Cells[0, 1].Character, Is.EqualTo("X"));
        Assert.That(screen.Cells[0, 2].Character, Is.EqualTo("X"));
        Assert.That(screen.Cells[0, 3].Character, Is.EqualTo("Y"));
        Assert.That(screen.Cells[0, 4].Character, Is.EqualTo("Y"));
    }

    /// <summary>
    /// ext_linegrid: default_colors_set should update screen colors.
    /// </summary>
    [Test]
    public void ExtLinegrid_DefaultColorsSet_UpdatesScreenColors()
    {
        var client = new NeovimClient();
        var colors = new List<int>();
        client.ForegroundColorChanged += colors.Add;
        client.BackgroundColorChanged += colors.Add;

        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new DefaultColorsSetEvent(0x112233, 0x445566, 0x778899, 0, 0),
            new GridResizeEvent(1, 2, 1),
            new FlushEvent(),
        });

        var screen = client.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.ForegroundColor, Is.EqualTo(0x112233));
        Assert.That(screen.BackgroundColor, Is.EqualTo(0x445566));
        Assert.That(colors, Does.Contain(0x112233));
        Assert.That(colors, Does.Contain(0x445566));
    }

    /// <summary>
    /// ext_linegrid: hl_id 0 should use default colors.
    /// </summary>
    [Test]
    public void ExtLinegrid_HlIdZero_UsesDefaultColors()
    {
        var client = new NeovimClient();

        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new DefaultColorsSetEvent(0xAAAAAA, 0xBBBBBB, 0xCCCCCC, 0, 0),
            new GridResizeEvent(1, 2, 1),
            new GridLineEvent(
                1,
                0,
                0,
                new[]
                {
                    new GridLineCell("A", 0, 1),
                },
                false),
            new FlushEvent(),
        });

        var screen = client.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[0, 0].ForegroundColor, Is.EqualTo(0xAAAAAA));
        Assert.That(screen.Cells[0, 0].BackgroundColor, Is.EqualTo(0xBBBBBB));
        Assert.That(screen.Cells[0, 0].Bold, Is.False);
    }

    /// <summary>
    /// ext_linegrid: grid_cursor_goto should update cursor position.
    /// </summary>
    [Test]
    public void ExtLinegrid_GridCursorGoto_UpdatesCursorPosition()
    {
        var client = new NeovimClient();

        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new GridResizeEvent(1, 5, 5),
            new GridCursorGotoEvent(1, 3, 2),
            new FlushEvent(),
        });

        var screen = client.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.CursorPosition.Row, Is.EqualTo(3));
        Assert.That(screen.CursorPosition.Col, Is.EqualTo(2));
    }

    /// <summary>
    /// ext_linegrid: grid_scroll should move rows using end-exclusive ranges.
    /// </summary>
    [Test]
    public void ExtLinegrid_GridScroll_MovesRowsWithEndExclusiveRanges()
    {
        var client = new NeovimClient();

        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new DefaultColorsSetEvent(0x000000, 0xFFFFFF, 0x000000, 0, 0),
            new GridResizeEvent(1, 3, 4),
            new GridLineEvent(1, 0, 0, new[] { new GridLineCell("A", 0, 1), new GridLineCell(" ", 0, 2) }, false),
            new GridLineEvent(1, 1, 0, new[] { new GridLineCell("B", 0, 1), new GridLineCell(" ", 0, 2) }, false),
            new GridLineEvent(1, 2, 0, new[] { new GridLineCell("C", 0, 1), new GridLineCell(" ", 0, 2) }, false),
            new GridLineEvent(1, 3, 0, new[] { new GridLineCell("D", 0, 1), new GridLineCell(" ", 0, 2) }, false),
            new FlushEvent(),
        });

        client.GetScreen();

        // Scroll up by 1 in region [0, 4) — end-exclusive
        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new GridScrollEvent(1, 0, 4, 0, 3, 1, 0),
            new FlushEvent(),
        });

        var screen = client.GetScreen();
        Assert.That(screen, Is.Not.Null);

        // Row 0 should now contain "B" (moved up from row 1)
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("B"));
        Assert.That(screen.Cells[1, 0].Character, Is.EqualTo("C"));
        Assert.That(screen.Cells[2, 0].Character, Is.EqualTo("D"));

        // Row 3 should be cleared
        Assert.That(screen.Cells[3, 0].Character, Is.EqualTo(" "));
    }

    /// <summary>
    /// ext_linegrid: flush event should trigger Redraw callback.
    /// </summary>
    [Test]
    public void ExtLinegrid_FlushEvent_TriggersRedraw()
    {
        var client = new NeovimClient();
        int redrawCount = 0;
        client.Redraw += () => redrawCount++;

        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new DefaultColorsSetEvent(0x000000, 0xFFFFFF, 0x000000, 0, 0),
            new GridResizeEvent(1, 3, 2),
            new GridLineEvent(1, 0, 0, new[] { new GridLineCell("A", 0, 1) }, false),
            new FlushEvent(),
        });

        Assert.That(redrawCount, Is.EqualTo(1));
    }

    /// <summary>
    /// ext_linegrid: grid_clear should reset all cells to defaults.
    /// </summary>
    [Test]
    public void ExtLinegrid_GridClear_ResetsAllCells()
    {
        var client = new NeovimClient();

        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new DefaultColorsSetEvent(0x000000, 0xFFFFFF, 0x000000, 0, 0),
            new GridResizeEvent(1, 3, 2),
            new GridLineEvent(1, 0, 0, new[] { new GridLineCell("A", 0, 1), new GridLineCell("B", 0, 1) }, false),
            new GridClearEvent(1),
            new FlushEvent(),
        });

        var screen = client.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo(" "));
        Assert.That(screen.Cells[0, 1].Character, Is.EqualTo(" "));
    }

    /// <summary>
    /// ext_linegrid: empty-string cells should store null (double-width right cell).
    /// </summary>
    [Test]
    public void ExtLinegrid_EmptyStringCell_StoresNull()
    {
        var client = new NeovimClient();

        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new DefaultColorsSetEvent(0x000000, 0xFFFFFF, 0x000000, 0, 0),
            new GridResizeEvent(1, 4, 1),
            new GridLineEvent(
                1,
                0,
                0,
                new[]
                {
                    new GridLineCell("\u4e16", 0, 1),
                    new GridLineCell(string.Empty, null, 1),
                    new GridLineCell("x", 0, 1),
                },
                false),
            new FlushEvent(),
        });

        var screen = client.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cells[0, 0].Character, Is.EqualTo("\u4e16"));
        Assert.That(screen.Cells[0, 1].Character, Is.Null);
        Assert.That(screen.Cells[0, 2].Character, Is.EqualTo("x"));
    }

    /// <summary>
    /// ext_popupmenu: popupmenu_show should populate screen popup state.
    /// </summary>
    [Test]
    public void ExtPopupmenu_ShowEvent_PopulatesScreenState()
    {
        var client = new NeovimClient();

        var items = new[]
        {
            new PopupMenuItem("println", "f", "io", "Print to stdout"),
            new PopupMenuItem("print", "f", "io", "Print without newline"),
        };

        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new DefaultColorsSetEvent(0x000000, 0xFFFFFF, 0x000000, 0, 0),
            new GridResizeEvent(1, 10, 5),
            new PopupmenuShowEvent(items, 0, 2, 3, 1),
            new FlushEvent(),
        });

        var screen = client.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.PopupItems, Is.Not.Null);
        var popupItems = screen.PopupItems!;
        Assert.That(popupItems, Has.Length.EqualTo(2));
        Assert.That(popupItems[0].Word, Is.EqualTo("println"));
        Assert.That(popupItems[1].Word, Is.EqualTo("print"));
        Assert.That(screen.PopupSelected, Is.EqualTo(0));
        Assert.That(screen.PopupAnchor, Is.EqualTo((2, 3)));
    }

    /// <summary>
    /// ext_popupmenu: popupmenu_select should update the selected index.
    /// </summary>
    [Test]
    public void ExtPopupmenu_SelectEvent_UpdatesSelectedIndex()
    {
        var client = new NeovimClient();

        var items = new[]
        {
            new PopupMenuItem("a", string.Empty, string.Empty, string.Empty),
            new PopupMenuItem("b", string.Empty, string.Empty, string.Empty),
        };

        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new DefaultColorsSetEvent(0x000000, 0xFFFFFF, 0x000000, 0, 0),
            new GridResizeEvent(1, 10, 5),
            new PopupmenuShowEvent(items, 0, 1, 1, 1),
            new PopupmenuSelectEvent(1),
            new FlushEvent(),
        });

        var screen = client.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.PopupSelected, Is.EqualTo(1));
    }

    /// <summary>
    /// ext_popupmenu: popupmenu_hide should clear popup state.
    /// </summary>
    [Test]
    public void ExtPopupmenu_HideEvent_ClearsPopupState()
    {
        var client = new NeovimClient();

        var items = new[]
        {
            new PopupMenuItem("a", string.Empty, string.Empty, string.Empty),
        };

        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new DefaultColorsSetEvent(0x000000, 0xFFFFFF, 0x000000, 0, 0),
            new GridResizeEvent(1, 10, 5),
            new PopupmenuShowEvent(items, 0, 1, 1, 1),
            new FlushEvent(),
        });

        var screen = client.GetScreen();
        Assert.That(screen!.PopupItems, Is.Not.Null);

        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new PopupmenuHideEvent(),
            new FlushEvent(),
        });

        screen = client.GetScreen();
        Assert.That(screen!.PopupItems, Is.Null);
        Assert.That(screen.PopupSelected, Is.EqualTo(-1));
        Assert.That(screen.PopupAnchor, Is.Null);
    }

    /// <summary>
    /// ext_cmdline: cmdline_show should populate screen cmdline state.
    /// </summary>
    [Test]
    public void ExtCmdline_ShowEvent_PopulatesScreenState()
    {
        var client = new NeovimClient();

        var content = new List<(int HlId, string Text)> { (0, "set number") };

        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new DefaultColorsSetEvent(0x000000, 0xFFFFFF, 0x000000, 0, 0),
            new GridResizeEvent(1, 10, 5),
            new CmdlineShowEvent(content, 3, ":", string.Empty, 0, 1),
            new FlushEvent(),
        });

        var screen = client.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cmdline, Is.Not.Null);
        Assert.That(screen.Cmdline!.Content, Has.Count.EqualTo(1));
        Assert.That(screen.Cmdline.Content[0].Text, Is.EqualTo("set number"));
        Assert.That(screen.Cmdline.CursorPos, Is.EqualTo(3));
        Assert.That(screen.Cmdline.FirstChar, Is.EqualTo(":"));
        Assert.That(screen.Cmdline.Level, Is.EqualTo(1));
    }

    /// <summary>
    /// ext_cmdline: cmdline_pos should update cursor position.
    /// </summary>
    [Test]
    public void ExtCmdline_PosEvent_UpdatesCursorPosition()
    {
        var client = new NeovimClient();

        var content = new List<(int HlId, string Text)> { (0, "hello") };

        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new DefaultColorsSetEvent(0x000000, 0xFFFFFF, 0x000000, 0, 0),
            new GridResizeEvent(1, 10, 5),
            new CmdlineShowEvent(content, 0, ":", string.Empty, 0, 1),
            new CmdlinePosEvent(5, 1),
            new FlushEvent(),
        });

        var screen = client.GetScreen();
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen!.Cmdline, Is.Not.Null);
        Assert.That(screen.Cmdline!.CursorPos, Is.EqualTo(5));
    }

    /// <summary>
    /// ext_cmdline: cmdline_hide should clear cmdline state.
    /// </summary>
    [Test]
    public void ExtCmdline_HideEvent_ClearsCmdlineState()
    {
        var client = new NeovimClient();

        var content = new List<(int HlId, string Text)> { (0, "test") };

        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new DefaultColorsSetEvent(0x000000, 0xFFFFFF, 0x000000, 0, 0),
            new GridResizeEvent(1, 10, 5),
            new CmdlineShowEvent(content, 0, ":", string.Empty, 0, 1),
            new FlushEvent(),
        });

        var screen = client.GetScreen();
        Assert.That(screen!.Cmdline, Is.Not.Null);

        client.ProcessRedrawForTesting(new IRedrawEvent[]
        {
            new CmdlineHideEvent(),
            new FlushEvent(),
        });

        screen = client.GetScreen();
        Assert.That(screen!.Cmdline, Is.Null);
    }
}
