// <copyright file="NeovimRedrawParserTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests;

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
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory());
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
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory());
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
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory());
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
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory());
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
        var parser = new RedrawEventParser<IRedrawEvent>(new DefaultRedrawEventFactory());
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
