// <copyright file="NeovimRedrawParserTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests
{
    using System.Collections.Generic;
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
            Assert.That(((PutEvent)events[0]).Text, Is.EqualTo(new int?[] { 'a' }));
            Assert.That(((PutEvent)events[1]).Text, Is.EqualTo(new int?[] { 'b', 'c' }));
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

        private static IList<MsgPack.MessagePackObject> Args(params MsgPack.MessagePackObject[] values)
        {
            return new List<MsgPack.MessagePackObject>(values);
        }
    }
}
