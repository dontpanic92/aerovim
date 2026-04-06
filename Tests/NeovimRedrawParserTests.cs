// <copyright file="NeovimRedrawParserTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests;

using AeroVim.Editor.Diagnostics;
using AeroVim.Editor.Utilities;
using AeroVim.NeovimClient;
using AeroVim.NeovimClient.Events;
using NUnit.Framework;

/// <summary>
/// Tests redraw event parsing.
/// </summary>
public class NeovimRedrawParserTests
{
    /// <summary>
    /// Batched put commands should produce multiple put events.
    /// </summary>
    [Test]
    public void Parse_BatchedPutCommands_ReturnsMultiplePutEvents()
    {
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory(), NullLogger.Instance);
        var redrawCommands = new List<MsgPack.MessagePackObject>
        {
            Command(
                "put",
                Args(new MsgPack.MessagePackObject("a")),
                Args(new MsgPack.MessagePackObject("b"), new MsgPack.MessagePackObject("c"))),
        };

        var events = parser.Parse(redrawCommands);

        Assert.That(events, Has.Count.EqualTo(2));
        Assert.That(events[0], Is.TypeOf<PutEvent>());
        Assert.That(((PutEvent)events[0]).Text, Is.EqualTo(new string?[] { "a" }));
        Assert.That(((PutEvent)events[1]).Text, Is.EqualTo(new string?[] { "b", "c" }));
    }

    /// <summary>
    /// Invalid redraw payloads should be ignored rather than terminating parsing.
    /// </summary>
    [Test]
    public void Parse_InvalidCommand_DoesNotAbortFollowingCommands()
    {
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory(), NullLogger.Instance);
        var redrawCommands = new List<MsgPack.MessagePackObject>
        {
            Command("resize", Args(new MsgPack.MessagePackObject(80))),
            Command("set_title", Args(new MsgPack.MessagePackObject("AeroVim"))),
        };

        var events = parser.Parse(redrawCommands);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0], Is.TypeOf<SetTitleEvent>());
        Assert.That(((SetTitleEvent)events[0]).Title, Is.EqualTo("AeroVim"));
    }

    /// <summary>
    /// Option-set guifont events should parse font names and style modifiers.
    /// </summary>
    [Test]
    public void Parse_OptionSetGuifont_ReturnsGuiFontEvent()
    {
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory(), NullLogger.Instance);
        var redrawCommands = new List<MsgPack.MessagePackObject>
        {
            Command("option_set", Args("guifont", "Cascadia_Code,Fira_Code:h12:b")),
        };

        var events = parser.Parse(redrawCommands);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0], Is.TypeOf<GuiFontEvent>());
        var font = ((GuiFontEvent)events[0]).FontSettings;
        Assert.That(font.FontNames, Is.EqualTo(new[] { "Cascadia Code", "Fira Code" }));
        Assert.That(font.FontPointSize, Is.EqualTo(12).Within(0.001));
        Assert.That(font.Bold, Is.True);
    }

    /// <summary>
    /// Mode-info payloads should preserve cursor and pointer metadata.
    /// </summary>
    [Test]
    public void Parse_ModeInfoSet_ReturnsParsedModeInfo()
    {
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory(), NullLogger.Instance);
        var redrawCommands = new List<MsgPack.MessagePackObject>
        {
            Command(
                "mode_info_set",
                Args(
                    true,
                    List(
                        Map(
                            ("cursor_shape", "vertical"),
                            ("cell_percentage", "25"),
                            ("blinkon", "1"),
                            ("mouse_shape", "beam"))))),
        };

        var events = parser.Parse(redrawCommands);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0], Is.TypeOf<ModeInfoSetEvent>());
        var modeInfoEvent = (ModeInfoSetEvent)events[0];
        var modeInfo = modeInfoEvent.ModeInfo;
        Assert.That(modeInfoEvent.CursorStyleEnabled, Is.True);
        Assert.That(modeInfo, Has.Count.EqualTo(1));
        Assert.That(modeInfo[0].CursorShape, Is.EqualTo(AeroVim.Editor.Utilities.CursorShape.Vertical));
        Assert.That(modeInfo[0].CellPercentage, Is.EqualTo(25));
        Assert.That(modeInfo[0].CursorBlinking, Is.EqualTo(CursorBlinking.BlinkOn));
        Assert.That(modeInfo[0].CursorStyleEnabled, Is.True);
        Assert.That(modeInfo[0].CursorVisible, Is.True);
        Assert.That(modeInfo[0].PointerShape, Is.EqualTo("beam"));
    }

    /// <summary>
    /// Repeated no-arg mouse events should produce one event per argument set.
    /// </summary>
    [Test]
    public void Parse_MouseEvents_ReturnsOneEventPerArgumentSet()
    {
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory(), NullLogger.Instance);
        var redrawCommands = new List<MsgPack.MessagePackObject>
        {
            Command("mouse_on", Args(), Args()),
            Command("mouse_off", Args()),
        };

        var events = parser.Parse(redrawCommands);

        Assert.That(events, Has.Count.EqualTo(3));
        Assert.That(events[0], Is.TypeOf<MouseOnEvent>());
        Assert.That(events[1], Is.TypeOf<MouseOnEvent>());
        Assert.That(events[2], Is.TypeOf<MouseOffEvent>());
    }

    /// <summary>
    /// hl_attr_define should parse RGB attributes from the dict.
    /// </summary>
    [Test]
    public void Parse_HlAttrDefine_ParsesRgbAttributes()
    {
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory(), NullLogger.Instance);
        var rgbAttrs = new MsgPack.MessagePackObjectDictionary
        {
            [new MsgPack.MessagePackObject("foreground")] = new MsgPack.MessagePackObject(0xFF0000),
            [new MsgPack.MessagePackObject("background")] = new MsgPack.MessagePackObject(0x00FF00),
            [new MsgPack.MessagePackObject("bold")] = new MsgPack.MessagePackObject(true),
            [new MsgPack.MessagePackObject("italic")] = new MsgPack.MessagePackObject(true),
            [new MsgPack.MessagePackObject("undercurl")] = new MsgPack.MessagePackObject(true),
        };
        var ctermAttrs = new MsgPack.MessagePackObjectDictionary();
        var info = new List<MsgPack.MessagePackObject>();

        var redrawCommands = new List<MsgPack.MessagePackObject>
        {
            Command("hl_attr_define", Args(42, rgbAttrs, ctermAttrs, info)),
        };

        var events = parser.Parse(redrawCommands);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0], Is.TypeOf<HlAttrDefineEvent>());
        var e = (HlAttrDefineEvent)events[0];
        Assert.That(e.Id, Is.EqualTo(42));
        Assert.That(e.RgbAttrs.Foreground, Is.EqualTo(0xFF0000));
        Assert.That(e.RgbAttrs.Background, Is.EqualTo(0x00FF00));
        Assert.That(e.RgbAttrs.Bold, Is.True);
        Assert.That(e.RgbAttrs.Italic, Is.True);
        Assert.That(e.RgbAttrs.Undercurl, Is.True);
        Assert.That(e.RgbAttrs.Reverse, Is.False);
    }

    /// <summary>
    /// default_colors_set should parse all five color values.
    /// </summary>
    [Test]
    public void Parse_DefaultColorsSet_ParsesFiveValues()
    {
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory(), NullLogger.Instance);
        var redrawCommands = new List<MsgPack.MessagePackObject>
        {
            Command("default_colors_set", Args(0x112233, 0x445566, 0x778899, 7, 0)),
        };

        var events = parser.Parse(redrawCommands);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0], Is.TypeOf<DefaultColorsSetEvent>());
        var e = (DefaultColorsSetEvent)events[0];
        Assert.That(e.RgbFg, Is.EqualTo(0x112233));
        Assert.That(e.RgbBg, Is.EqualTo(0x445566));
        Assert.That(e.RgbSp, Is.EqualTo(0x778899));
        Assert.That(e.CtermFg, Is.EqualTo(7));
        Assert.That(e.CtermBg, Is.EqualTo(0));
    }

    /// <summary>
    /// grid_resize should parse grid, width, and height.
    /// </summary>
    [Test]
    public void Parse_GridResize_ParsesGridWidthHeight()
    {
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory(), NullLogger.Instance);
        var redrawCommands = new List<MsgPack.MessagePackObject>
        {
            Command("grid_resize", Args(1, 80, 24)),
        };

        var events = parser.Parse(redrawCommands);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0], Is.TypeOf<GridResizeEvent>());
        var e = (GridResizeEvent)events[0];
        Assert.That(e.Grid, Is.EqualTo(1));
        Assert.That(e.Width, Is.EqualTo(80));
        Assert.That(e.Height, Is.EqualTo(24));
    }

    /// <summary>
    /// grid_line should parse cells with text, optional hl_id, and optional repeat.
    /// </summary>
    [Test]
    public void Parse_GridLine_ParsesCellsWithHlIdAndRepeat()
    {
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory(), NullLogger.Instance);

        // [grid, row, col_start, cells, wrap]
        // cells: [["H", 1], ["e"], [" ", 0, 5]]
        var cellsArray = new List<MsgPack.MessagePackObject>
        {
            new MsgPack.MessagePackObject(new List<MsgPack.MessagePackObject>
            {
                new MsgPack.MessagePackObject("H"),
                new MsgPack.MessagePackObject(1),
            }),
            new MsgPack.MessagePackObject(new List<MsgPack.MessagePackObject>
            {
                new MsgPack.MessagePackObject("e"),
            }),
            new MsgPack.MessagePackObject(new List<MsgPack.MessagePackObject>
            {
                new MsgPack.MessagePackObject(" "),
                new MsgPack.MessagePackObject(0),
                new MsgPack.MessagePackObject(5),
            }),
        };

        var redrawCommands = new List<MsgPack.MessagePackObject>
        {
            Command("grid_line", Args(2, 0, 0, cellsArray, false)),
        };

        var events = parser.Parse(redrawCommands);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0], Is.TypeOf<GridLineEvent>());
        var e = (GridLineEvent)events[0];
        Assert.That(e.Grid, Is.EqualTo(2));
        Assert.That(e.Row, Is.EqualTo(0));
        Assert.That(e.ColStart, Is.EqualTo(0));
        Assert.That(e.Wrap, Is.False);
        Assert.That(e.Cells, Has.Length.EqualTo(3));
        Assert.That(e.Cells[0].Text, Is.EqualTo("H"));
        Assert.That(e.Cells[0].HlId, Is.EqualTo(1));
        Assert.That(e.Cells[0].Repeat, Is.EqualTo(1));
        Assert.That(e.Cells[1].Text, Is.EqualTo("e"));
        Assert.That(e.Cells[1].HlId, Is.Null);
        Assert.That(e.Cells[1].Repeat, Is.EqualTo(1));
        Assert.That(e.Cells[2].Text, Is.EqualTo(" "));
        Assert.That(e.Cells[2].HlId, Is.EqualTo(0));
        Assert.That(e.Cells[2].Repeat, Is.EqualTo(5));
    }

    /// <summary>
    /// grid_clear should parse grid id.
    /// </summary>
    [Test]
    public void Parse_GridClear_ParsesGridId()
    {
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory(), NullLogger.Instance);
        var redrawCommands = new List<MsgPack.MessagePackObject>
        {
            Command("grid_clear", Args(1)),
        };

        var events = parser.Parse(redrawCommands);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0], Is.TypeOf<GridClearEvent>());
        Assert.That(((GridClearEvent)events[0]).Grid, Is.EqualTo(1));
    }

    /// <summary>
    /// grid_cursor_goto should parse grid, row, and col.
    /// </summary>
    [Test]
    public void Parse_GridCursorGoto_ParsesGridRowCol()
    {
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory(), NullLogger.Instance);
        var redrawCommands = new List<MsgPack.MessagePackObject>
        {
            Command("grid_cursor_goto", Args(1, 5, 10)),
        };

        var events = parser.Parse(redrawCommands);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0], Is.TypeOf<GridCursorGotoEvent>());
        var e = (GridCursorGotoEvent)events[0];
        Assert.That(e.Grid, Is.EqualTo(1));
        Assert.That(e.Row, Is.EqualTo(5));
        Assert.That(e.Col, Is.EqualTo(10));
    }

    /// <summary>
    /// grid_scroll should parse all seven parameters.
    /// </summary>
    [Test]
    public void Parse_GridScroll_ParsesAllParameters()
    {
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory(), NullLogger.Instance);
        var redrawCommands = new List<MsgPack.MessagePackObject>
        {
            Command("grid_scroll", Args(1, 0, 24, 0, 80, 3, 0)),
        };

        var events = parser.Parse(redrawCommands);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0], Is.TypeOf<GridScrollEvent>());
        var e = (GridScrollEvent)events[0];
        Assert.That(e.Grid, Is.EqualTo(1));
        Assert.That(e.Top, Is.EqualTo(0));
        Assert.That(e.Bot, Is.EqualTo(24));
        Assert.That(e.Left, Is.EqualTo(0));
        Assert.That(e.Right, Is.EqualTo(80));
        Assert.That(e.Rows, Is.EqualTo(3));
        Assert.That(e.Cols, Is.EqualTo(0));
    }

    /// <summary>
    /// flush should produce a FlushEvent.
    /// </summary>
    [Test]
    public void Parse_Flush_ProducesFlushEvent()
    {
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory(), NullLogger.Instance);
        var redrawCommands = new List<MsgPack.MessagePackObject>
        {
            Command("flush", Args()),
        };

        var events = parser.Parse(redrawCommands);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0], Is.TypeOf<FlushEvent>());
    }

    /// <summary>
    /// popupmenu_show should parse items, selected, row, col, and grid.
    /// </summary>
    [Test]
    public void Parse_PopupmenuShow_ParsesItemsAndAnchor()
    {
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory(), NullLogger.Instance);

        var item1 = new List<MsgPack.MessagePackObject>
        {
            new MsgPack.MessagePackObject("println"),
            new MsgPack.MessagePackObject("f"),
            new MsgPack.MessagePackObject("io"),
            new MsgPack.MessagePackObject("Print to stdout"),
        };
        var item2 = new List<MsgPack.MessagePackObject>
        {
            new MsgPack.MessagePackObject("print"),
            new MsgPack.MessagePackObject("f"),
            new MsgPack.MessagePackObject("io"),
            new MsgPack.MessagePackObject(string.Empty),
        };
        var items = new List<MsgPack.MessagePackObject>
        {
            new MsgPack.MessagePackObject(item1),
            new MsgPack.MessagePackObject(item2),
        };

        var redrawCommands = new List<MsgPack.MessagePackObject>
        {
            Command("popupmenu_show", Args(items, 0, 5, 10, 1)),
        };

        var events = parser.Parse(redrawCommands);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0], Is.TypeOf<PopupmenuShowEvent>());
        var e = (PopupmenuShowEvent)events[0];
        Assert.That(e.Items, Has.Length.EqualTo(2));
        Assert.That(e.Items[0].Word, Is.EqualTo("println"));
        Assert.That(e.Items[0].Kind, Is.EqualTo("f"));
        Assert.That(e.Items[0].Menu, Is.EqualTo("io"));
        Assert.That(e.Items[0].Info, Is.EqualTo("Print to stdout"));
        Assert.That(e.Items[1].Word, Is.EqualTo("print"));
        Assert.That(e.Selected, Is.EqualTo(0));
        Assert.That(e.Row, Is.EqualTo(5));
        Assert.That(e.Col, Is.EqualTo(10));
        Assert.That(e.Grid, Is.EqualTo(1));
    }

    /// <summary>
    /// popupmenu_select should parse the selected index.
    /// </summary>
    [Test]
    public void Parse_PopupmenuSelect_ParsesSelectedIndex()
    {
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory(), NullLogger.Instance);
        var redrawCommands = new List<MsgPack.MessagePackObject>
        {
            Command("popupmenu_select", Args(3)),
        };

        var events = parser.Parse(redrawCommands);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0], Is.TypeOf<PopupmenuSelectEvent>());
        Assert.That(((PopupmenuSelectEvent)events[0]).Selected, Is.EqualTo(3));
    }

    /// <summary>
    /// popupmenu_hide should produce a PopupmenuHideEvent.
    /// </summary>
    [Test]
    public void Parse_PopupmenuHide_ProducesEvent()
    {
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory(), NullLogger.Instance);
        var redrawCommands = new List<MsgPack.MessagePackObject>
        {
            Command("popupmenu_hide", Args()),
        };

        var events = parser.Parse(redrawCommands);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0], Is.TypeOf<PopupmenuHideEvent>());
    }

    /// <summary>
    /// cmdline_show should parse content, pos, firstc, prompt, indent, and level.
    /// </summary>
    [Test]
    public void Parse_CmdlineShow_ParsesContentAndMetadata()
    {
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory(), NullLogger.Instance);

        // content: [[{}, "set number", 0]]
        var chunk = new List<MsgPack.MessagePackObject>
        {
            new MsgPack.MessagePackObject(new MsgPack.MessagePackObjectDictionary()),
            new MsgPack.MessagePackObject("set number"),
            new MsgPack.MessagePackObject(0),
        };
        var content = new List<MsgPack.MessagePackObject>
        {
            new MsgPack.MessagePackObject(chunk),
        };

        var redrawCommands = new List<MsgPack.MessagePackObject>
        {
            Command("cmdline_show", Args(content, 3, ":", string.Empty, 0, 1)),
        };

        var events = parser.Parse(redrawCommands);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0], Is.TypeOf<CmdlineShowEvent>());
        var e = (CmdlineShowEvent)events[0];
        Assert.That(e.Content, Has.Count.EqualTo(1));
        Assert.That(e.Content[0].Text, Is.EqualTo("set number"));
        Assert.That(e.Pos, Is.EqualTo(3));
        Assert.That(e.Firstc, Is.EqualTo(":"));
        Assert.That(e.Level, Is.EqualTo(1));
    }

    /// <summary>
    /// cmdline_pos should parse pos and level.
    /// </summary>
    [Test]
    public void Parse_CmdlinePos_ParsesPosAndLevel()
    {
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory(), NullLogger.Instance);
        var redrawCommands = new List<MsgPack.MessagePackObject>
        {
            Command("cmdline_pos", Args(5, 1)),
        };

        var events = parser.Parse(redrawCommands);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0], Is.TypeOf<CmdlinePosEvent>());
        var e = (CmdlinePosEvent)events[0];
        Assert.That(e.Pos, Is.EqualTo(5));
        Assert.That(e.Level, Is.EqualTo(1));
    }

    /// <summary>
    /// cmdline_hide should produce a CmdlineHideEvent.
    /// </summary>
    [Test]
    public void Parse_CmdlineHide_ProducesEvent()
    {
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory(), NullLogger.Instance);
        var redrawCommands = new List<MsgPack.MessagePackObject>
        {
            Command("cmdline_hide", Args()),
        };

        var events = parser.Parse(redrawCommands);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0], Is.TypeOf<CmdlineHideEvent>());
    }

    private static MsgPack.MessagePackObject Command(string name, params IList<MsgPack.MessagePackObject>[] args)
    {
        var list = new List<MsgPack.MessagePackObject>
        {
            new MsgPack.MessagePackObject(name),
        };

        foreach (var arg in args)
        {
            list.Add(new MsgPack.MessagePackObject(arg));
        }

        return new MsgPack.MessagePackObject(list);
    }

    private static IList<MsgPack.MessagePackObject> Args(params object[] values)
    {
        var list = new List<MsgPack.MessagePackObject>(values.Length);
        foreach (object value in values)
        {
            list.Add(ToMessagePackObject(value));
        }

        return list;
    }

    private static IList<MsgPack.MessagePackObject> List(params object[] values)
    {
        return Args(values);
    }

    private static MsgPack.MessagePackObjectDictionary Map(params (string Key, string Value)[] entries)
    {
        var dict = new MsgPack.MessagePackObjectDictionary();
        foreach (var (key, value) in entries)
        {
            dict[new MsgPack.MessagePackObject(key)] = new MsgPack.MessagePackObject(value);
        }

        return dict;
    }

    private static MsgPack.MessagePackObject ToMessagePackObject(object value)
    {
        return value switch
        {
            MsgPack.MessagePackObject messagePackObject => messagePackObject,
            string text => new MsgPack.MessagePackObject(text),
            bool boolean => new MsgPack.MessagePackObject(boolean),
            int intValue => new MsgPack.MessagePackObject(intValue),
            uint uintValue => new MsgPack.MessagePackObject(uintValue),
            IList<MsgPack.MessagePackObject> list => new MsgPack.MessagePackObject(list),
            MsgPack.MessagePackObjectDictionary dictionary => new MsgPack.MessagePackObject(dictionary),
            _ => throw new InvalidOperationException($"Unsupported test value type {value.GetType().FullName}."),
        };
    }
}
