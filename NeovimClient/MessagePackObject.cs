// <copyright file="MessagePackObject.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace MsgPack
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Minimal compatibility wrapper for MessagePack-CSharp.
    /// </summary>
    public sealed class MessagePackObject : IEquatable<MessagePackObject>
    {
        private readonly object? value;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagePackObject"/> class.
        /// </summary>
        /// <param name="value">Underlying value.</param>
        public MessagePackObject(object? value)
        {
            this.value = value;
        }

        /// <summary>
        /// Gets a value indicating whether this instance is nil.
        /// </summary>
        public bool IsNil => this.value == null;

        /// <summary>
        /// Convert to unsigned integer.
        /// </summary>
        /// <returns>Unsigned integer value.</returns>
        public uint AsUInt32() => Convert.ToUInt32(this.value);

        /// <summary>
        /// Convert to signed integer.
        /// </summary>
        /// <returns>Signed integer value.</returns>
        public int AsInt32() => Convert.ToInt32(this.value);

        /// <summary>
        /// Convert to string.
        /// </summary>
        /// <returns>String value.</returns>
        public string AsString()
        {
            return this.value switch
            {
                string text => text,
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                _ => Convert.ToString(this.value) ?? string.Empty,
            };
        }

        /// <summary>
        /// Convert to string.
        /// </summary>
        /// <returns>String value.</returns>
        public string AsStringUtf8() => this.AsString();

        /// <summary>
        /// Convert to boolean.
        /// </summary>
        /// <returns>Boolean value.</returns>
        public bool AsBoolean() => Convert.ToBoolean(this.value);

        /// <summary>
        /// Convert to list.
        /// </summary>
        /// <returns>List value.</returns>
        public IList<MessagePackObject> AsList() => (IList<MessagePackObject>)this.value!;

        /// <summary>
        /// Convert to dictionary.
        /// </summary>
        /// <returns>Dictionary value.</returns>
        public MessagePackObjectDictionary AsDictionary() => (MessagePackObjectDictionary)this.value!;

        /// <summary>
        /// Convert to enumerable.
        /// </summary>
        /// <returns>Enumerable value.</returns>
        public IEnumerable<MessagePackObject> AsEnumerable() => this.AsList();

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.value switch
            {
                null => string.Empty,
                string text => text,
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                IList<MessagePackObject> list => string.Join(string.Empty, list.Select(item => item.ToString())),
                _ => this.value?.ToString() ?? string.Empty,
            };
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => this.Equals(obj as MessagePackObject);

        /// <inheritdoc/>
        public bool Equals(MessagePackObject? other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (this.value is byte[] leftBytes && other.value is byte[] rightBytes)
            {
                return leftBytes.SequenceEqual(rightBytes);
            }

            return Equals(this.value, other.value);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            if (this.value is null)
            {
                return 0;
            }

            if (this.value is byte[] bytes)
            {
                int hash = 17;
                foreach (var item in bytes)
                {
                    hash = (hash * 31) + item;
                }

                return hash;
            }

            return this.value.GetHashCode();
        }

        /// <summary>
        /// Gets the raw value.
        /// </summary>
        /// <returns>The raw value.</returns>
        internal object? GetRawValue() => this.value;
    }
}
