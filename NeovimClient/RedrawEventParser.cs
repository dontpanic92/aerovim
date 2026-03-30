// <copyright file="RedrawEventParser.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Parses Neovim redraw notifications into redraw events.
    /// </summary>
    /// <typeparam name="TRedrawEvent">The redraw event type.</typeparam>
    public sealed class RedrawEventParser<TRedrawEvent>
    {
        private readonly IRedrawEventFactory<TRedrawEvent> factory;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedrawEventParser{TRedrawEvent}"/> class.
        /// </summary>
        /// <param name="factory">The event factory.</param>
        public RedrawEventParser(IRedrawEventFactory<TRedrawEvent> factory)
        {
            this.factory = factory;
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
                    Trace.TraceError($"AeroVim: Failed to parse redraw command: {ex}");
                }
            }

            return events;
        }

        private static MsgPack.MessagePackObject TryGetValueFromDictionary(MsgPack.MessagePackObjectDictionary dict, string key)
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
            if (list == null)
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
                default:
                    Trace.TraceWarning($"AeroVim: Unsupported redraw event '{eventName}' was ignored.");
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
                IList<int?> result = new List<int?>(args.Count);
                foreach (var item in args)
                {
                    var ch = item.AsString();
                    int? codepoint = null;
                    if (ch != string.Empty)
                    {
                        codepoint = char.ConvertToUtf32(ch, 0);
                    }

                    result.Add(codepoint);
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
    }
}
