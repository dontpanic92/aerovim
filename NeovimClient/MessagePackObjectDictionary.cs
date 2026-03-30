// <copyright file="MessagePackObjectDictionary.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace MsgPack
{
    using System.Collections.Generic;

    /// <summary>
    /// Compatibility dictionary wrapper.
    /// </summary>
    public sealed class MessagePackObjectDictionary : Dictionary<MessagePackObject, MessagePackObject>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MessagePackObjectDictionary"/> class.
        /// </summary>
        public MessagePackObjectDictionary()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagePackObjectDictionary"/> class.
        /// </summary>
        /// <param name="capacity">Initial capacity.</param>
        public MessagePackObjectDictionary(int capacity)
            : base(capacity)
        {
        }

        /// <summary>
        /// Try to get a value by string key.
        /// </summary>
        /// <param name="key">Key value.</param>
        /// <param name="value">Output value.</param>
        /// <returns><see langword="true"/> when found.</returns>
        public bool TryGetValue(string key, out MessagePackObject? value)
        {
            return this.TryGetValue(new MessagePackObject(key), out value);
        }
    }
}
