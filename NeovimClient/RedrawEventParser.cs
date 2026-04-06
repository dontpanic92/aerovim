// <copyright file="RedrawEventParser.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient;

using AeroVim.Editor.Diagnostics;
using AeroVim.Editor.Utilities;

/// <summary>
/// Parses Neovim redraw notifications into redraw events.
/// </summary>
/// <typeparam name="TRedrawEvent">The redraw event type.</typeparam>
public sealed class RedrawEventParser<TRedrawEvent>
{
    private readonly IRedrawEventFactory<TRedrawEvent> factory;
    private readonly IComponentLogger log;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedrawEventParser{TRedrawEvent}"/> class.
    /// </summary>
    /// <param name="factory">The event factory.</param>
    /// <param name="logger">Application logger.</param>
    public RedrawEventParser(IRedrawEventFactory<TRedrawEvent> factory, IAppLogger logger)
    {
        this.factory = factory;
        this.log = logger.For<RedrawEventParser<TRedrawEvent>>();
    }

    /// <summary>
    /// Parse a redraw notification into events.
    /// </summary>
    /// <param name="rawEvents">The raw redraw payload.</param>
    /// <returns>The parsed redraw events.</returns>
    public IList<TRedrawEvent> Parse(IList<MsgPack.MessagePackObject> rawEvents)
    {
        var events = new List<TRedrawEvent>();
        foreach (var rawEvent in rawEvents)
        {
            try
            {
                this.ParseRedrawCommand(rawEvent, events);
            }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException or FormatException or InvalidOperationException)
            {
                this.log.Error("Failed to parse redraw command.", ex);
            }
        }

        return events;
    }

    private static MsgPack.MessagePackObject? TryGetValueFromDictionary(MsgPack.MessagePackObjectDictionary dict, string key)
    {
        if (dict.TryGetValue(key, out var value))
        {
            return value;
        }

        return null;
    }

    private static IList<MsgPack.MessagePackObject> RequireList(MsgPack.MessagePackObject value, string context)
    {
        var list = value.AsList();
        if (list is null)
        {
            throw new InvalidDataException($"{context} must be a list.");
        }

        return list;
    }

    private static IList<MsgPack.MessagePackObject> RequireArgumentList(IList<MsgPack.MessagePackObject> command, int index, int minimumCount, string eventName)
    {
        if (index >= command.Count)
        {
            throw new InvalidDataException($"{eventName} is missing argument set {index}.");
        }

        var args = RequireList(command[index], $"{eventName}[{index}]");
        if (args.Count < minimumCount)
        {
            throw new InvalidDataException($"{eventName}[{index}] must contain at least {minimumCount} item(s).");
        }

        return args;
    }

    private void ParseRedrawCommand(MsgPack.MessagePackObject rawEvent, ICollection<TRedrawEvent> events)
    {
        var command = RequireList(rawEvent, "redraw command");
        if (command.Count == 0)
        {
            throw new InvalidDataException("redraw command must contain an event name.");
        }

        var eventName = command[0].AsString();
        switch (eventName)
        {
            case "set_title":
                this.ParseSetTitle(command, events);
                break;
            case "set_icon":
                this.ParseSetIcon(command, events);
                break;
            case "mode_info_set":
                this.ParseModeInfoSet(command, events);
                break;
            case "mode_change":
                this.ParseModeChange(command, events);
                break;
            case "cursor_goto":
                this.ParseCursorGoto(command, events);
                break;
            case "put":
                this.ParsePut(command, events);
                break;
            case "clear":
                this.ParseNoArgRepeating(command, events, this.factory.CreateClearEvent, eventName);
                break;
            case "eol_clear":
                this.ParseNoArgRepeating(command, events, this.factory.CreateEolClearEvent, eventName);
                break;
            case "resize":
                this.ParseResize(command, events);
                break;
            case "highlight_set":
                this.ParseHighlightSet(command, events);
                break;
            case "update_fg":
                this.ParseUpdateColor(command, events, this.factory.CreateUpdateFgEvent, eventName);
                break;
            case "update_bg":
                this.ParseUpdateColor(command, events, this.factory.CreateUpdateBgEvent, eventName);
                break;
            case "update_sp":
                this.ParseUpdateColor(command, events, this.factory.CreateUpdateSpEvent, eventName);
                break;
            case "set_scroll_region":
                this.ParseSetScrollRegion(command, events);
                break;
            case "scroll":
                this.ParseScroll(command, events);
                break;
            case "option_set":
                this.ParseOptionSet(command, events);
                break;
            case "mouse_on":
                this.ParseNoArgRepeating(command, events, this.factory.CreateMouseOnEvent, eventName);
                break;
            case "mouse_off":
                this.ParseNoArgRepeating(command, events, this.factory.CreateMouseOffEvent, eventName);
                break;
            case "hl_attr_define":
                this.ParseHlAttrDefine(command, events);
                break;
            case "default_colors_set":
                this.ParseDefaultColorsSet(command, events);
                break;
            case "grid_resize":
                this.ParseGridResize(command, events);
                break;
            case "grid_line":
                this.ParseGridLine(command, events);
                break;
            case "grid_clear":
                this.ParseGridClear(command, events);
                break;
            case "grid_cursor_goto":
                this.ParseGridCursorGoto(command, events);
                break;
            case "grid_scroll":
                this.ParseGridScroll(command, events);
                break;
            case "flush":
                this.ParseNoArgRepeating(command, events, this.factory.CreateFlushEvent, eventName);
                break;
            case "popupmenu_show":
                this.ParsePopupmenuShow(command, events);
                break;
            case "popupmenu_select":
                this.ParsePopupmenuSelect(command, events);
                break;
            case "popupmenu_hide":
                this.ParseNoArgRepeating(command, events, this.factory.CreatePopupmenuHideEvent, eventName);
                break;
            case "cmdline_show":
                this.ParseCmdlineShow(command, events);
                break;
            case "cmdline_pos":
                this.ParseCmdlinePos(command, events);
                break;
            case "cmdline_hide":
                this.ParseNoArgRepeating(command, events, this.factory.CreateCmdlineHideEvent, eventName);
                break;
            default:
                this.log.Warning($"Unsupported redraw event '{eventName}' was ignored.");
                break;
        }
    }

    private void ParseSetTitle(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 1, "set_title");
            events.Add(this.factory.CreateSetTitleEvent(args[0].AsStringUtf8()));
        }
    }

    private void ParseSetIcon(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 1, "set_icon");
            events.Add(this.factory.CreateSetIconTitleEvent(args[0].AsStringUtf8()));
        }
    }

    private void ParseModeInfoSet(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 2, "mode_info_set");
            var mode = RequireList(args[1], "mode_info_set mode list").Select(
                item => (IDictionary<string, string>)item.AsDictionary().ToDictionary(
                    k => k.Key.AsStringUtf8(),
                    v => v.Value.ToString())).ToList();
            events.Add(this.factory.CreateModeInfoSetEvent(args[0].AsBoolean(), mode));
        }
    }

    private void ParseModeChange(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 2, "mode_change");
            events.Add(this.factory.CreateModeChangeEvent(args[0].AsStringUtf8(), args[1].AsInt32()));
        }
    }

    private void ParseCursorGoto(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 2, "cursor_goto");
            events.Add(this.factory.CreateCursorGotoEvent(args[0].AsUInt32(), args[1].AsUInt32()));
        }
    }

    private void ParsePut(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireList(command[i], $"put[{i}]");
            IList<string?> result = new List<string?>(args.Count);
            foreach (var item in args)
            {
                var ch = item.AsString();
                string? text = ch.Length > 0 ? ch : null;
                result.Add(text);
            }

            events.Add(this.factory.CreatePutEvent(result));
        }
    }

    private void ParseNoArgRepeating(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events, Func<TRedrawEvent> factoryMethod, string eventName)
    {
        for (int i = 1; i < command.Count; i++)
        {
            RequireList(command[i], $"{eventName}[{i}]");
            events.Add(factoryMethod());
        }
    }

    private void ParseResize(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 2, "resize");
            uint col = args[0].AsUInt32();
            uint row = args[1].AsUInt32();
            events.Add(this.factory.CreateResizeEvent(row, col));
        }
    }

    private void ParseHighlightSet(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 1, "highlight_set");
            var dict = args[0].AsDictionary();
            int? foreground = TryGetValueFromDictionary(dict, "foreground")?.AsInt32();
            int? background = TryGetValueFromDictionary(dict, "background")?.AsInt32();
            int? special = TryGetValueFromDictionary(dict, "special")?.AsInt32();
            bool reverse = TryGetValueFromDictionary(dict, "reverse")?.AsBoolean() == true;
            bool italic = TryGetValueFromDictionary(dict, "italic")?.AsBoolean() == true;
            bool bold = TryGetValueFromDictionary(dict, "bold")?.AsBoolean() == true;
            bool underline = TryGetValueFromDictionary(dict, "underline")?.AsBoolean() == true;
            bool undercurl = TryGetValueFromDictionary(dict, "undercurl")?.AsBoolean() == true;

            events.Add(this.factory.CreateHightlightSetEvent(
                foreground,
                background,
                special,
                reverse,
                italic,
                bold,
                underline,
                undercurl));
        }
    }

    private void ParseUpdateColor(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events, Func<int, TRedrawEvent> factoryMethod, string eventName)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 1, eventName);
            events.Add(factoryMethod(args[0].AsInt32()));
        }
    }

    private void ParseSetScrollRegion(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 4, "set_scroll_region");
            events.Add(this.factory.CreateSetScrollRegionEvent(
                args[0].AsInt32(),
                args[1].AsInt32(),
                args[2].AsInt32(),
                args[3].AsInt32()));
        }
    }

    private void ParseScroll(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 1, "scroll");
            events.Add(this.factory.CreateScrollEvent(args[0].AsInt32()));
        }
    }

    private void ParseOptionSet(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 2, "option_set");
            events.Add(this.factory.CreateOptionSetEvent(args[0].AsString(), args[1].ToString()));
        }
    }

    private void ParseHlAttrDefine(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 4, "hl_attr_define");
            int id = args[0].AsInt32();
            var rgbDict = args[1].AsDictionary();

            var attrs = new HighlightAttributes
            {
                Foreground = TryGetValueFromDictionary(rgbDict, "foreground")?.AsInt32(),
                Background = TryGetValueFromDictionary(rgbDict, "background")?.AsInt32(),
                Special = TryGetValueFromDictionary(rgbDict, "special")?.AsInt32(),
                Reverse = TryGetValueFromDictionary(rgbDict, "reverse")?.AsBoolean() == true,
                Italic = TryGetValueFromDictionary(rgbDict, "italic")?.AsBoolean() == true,
                Bold = TryGetValueFromDictionary(rgbDict, "bold")?.AsBoolean() == true,
                Underline = TryGetValueFromDictionary(rgbDict, "underline")?.AsBoolean() == true,
                Undercurl = TryGetValueFromDictionary(rgbDict, "undercurl")?.AsBoolean() == true,
                Strikethrough = TryGetValueFromDictionary(rgbDict, "strikethrough")?.AsBoolean() == true,
            };

            events.Add(this.factory.CreateHlAttrDefineEvent(id, attrs));
        }
    }

    private void ParseDefaultColorsSet(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 5, "default_colors_set");
            events.Add(this.factory.CreateDefaultColorsSetEvent(
                args[0].AsInt32(),
                args[1].AsInt32(),
                args[2].AsInt32(),
                args[3].AsInt32(),
                args[4].AsInt32()));
        }
    }

    private void ParseGridResize(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 3, "grid_resize");
            events.Add(this.factory.CreateGridResizeEvent(
                args[0].AsInt32(),
                args[1].AsInt32(),
                args[2].AsInt32()));
        }
    }

    private void ParseGridLine(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 4, "grid_line");
            int grid = args[0].AsInt32();
            int row = args[1].AsInt32();
            int colStart = args[2].AsInt32();
            var rawCells = RequireList(args[3], "grid_line cells");
            bool wrap = args.Count > 4 && args[4].AsBoolean();

            var cells = new Events.GridLineCell[rawCells.Count];
            for (int c = 0; c < rawCells.Count; c++)
            {
                var cellArray = RequireList(rawCells[c], $"grid_line cell[{c}]");
                string text = cellArray[0].AsString();
                int? hlId = cellArray.Count > 1 ? cellArray[1].AsInt32() : null;
                int repeat = cellArray.Count > 2 ? cellArray[2].AsInt32() : 1;
                cells[c] = new Events.GridLineCell(text, hlId, repeat);
            }

            events.Add(this.factory.CreateGridLineEvent(grid, row, colStart, cells, wrap));
        }
    }

    private void ParseGridClear(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 1, "grid_clear");
            events.Add(this.factory.CreateGridClearEvent(args[0].AsInt32()));
        }
    }

    private void ParseGridCursorGoto(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 3, "grid_cursor_goto");
            events.Add(this.factory.CreateGridCursorGotoEvent(
                args[0].AsInt32(),
                args[1].AsInt32(),
                args[2].AsInt32()));
        }
    }

    private void ParseGridScroll(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 7, "grid_scroll");
            events.Add(this.factory.CreateGridScrollEvent(
                args[0].AsInt32(),
                args[1].AsInt32(),
                args[2].AsInt32(),
                args[3].AsInt32(),
                args[4].AsInt32(),
                args[5].AsInt32(),
                args[6].AsInt32()));
        }
    }

    private void ParsePopupmenuShow(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 5, "popupmenu_show");
            var rawItems = RequireList(args[0], "popupmenu_show items");
            var items = new AeroVim.Editor.PopupMenuItem[rawItems.Count];
            for (int j = 0; j < rawItems.Count; j++)
            {
                var itemArray = RequireList(rawItems[j], $"popupmenu_show item[{j}]");
                string word = itemArray.Count > 0 ? itemArray[0].AsString() : string.Empty;
                string kind = itemArray.Count > 1 ? itemArray[1].AsString() : string.Empty;
                string menu = itemArray.Count > 2 ? itemArray[2].AsString() : string.Empty;
                string info = itemArray.Count > 3 ? itemArray[3].AsString() : string.Empty;
                items[j] = new AeroVim.Editor.PopupMenuItem(word, kind, menu, info);
            }

            events.Add(this.factory.CreatePopupmenuShowEvent(
                items,
                args[1].AsInt32(),
                args[2].AsInt32(),
                args[3].AsInt32(),
                args[4].AsInt32()));
        }
    }

    private void ParsePopupmenuSelect(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 1, "popupmenu_select");
            events.Add(this.factory.CreatePopupmenuSelectEvent(args[0].AsInt32()));
        }
    }

    private void ParseCmdlineShow(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 6, "cmdline_show");
            var rawContent = RequireList(args[0], "cmdline_show content");
            var content = new List<(int HlId, string Text)>(rawContent.Count);
            foreach (var chunk in rawContent)
            {
                var chunkList = RequireList(chunk, "cmdline_show chunk");
                int hlId = chunkList.Count > 2 ? chunkList[2].AsInt32() : 0;
                string text = chunkList.Count > 1 ? chunkList[1].AsString() : string.Empty;
                content.Add((hlId, text));
            }

            events.Add(this.factory.CreateCmdlineShowEvent(
                content,
                args[1].AsInt32(),
                args[2].AsString(),
                args[3].AsString(),
                args[4].AsInt32(),
                args[5].AsInt32()));
        }
    }

    private void ParseCmdlinePos(IList<MsgPack.MessagePackObject> command, ICollection<TRedrawEvent> events)
    {
        for (int i = 1; i < command.Count; i++)
        {
            var args = RequireArgumentList(command, i, 2, "cmdline_pos");
            events.Add(this.factory.CreateCmdlinePosEvent(args[0].AsInt32(), args[1].AsInt32()));
        }
    }
}
