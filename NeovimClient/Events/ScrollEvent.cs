// <copyright file="ScrollEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events
{
    /// <summary>
    /// Scroll event.
    /// </summary>
    public class ScrollEvent : IRedrawEvent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ScrollEvent"/> class.
        /// </summary>
        /// <param name="count">Scroll count.</param>
        public ScrollEvent(int count)
        {
            this.Count = count;
        }

        /// <summary>
        /// Gets the count of lines to scroll.
        /// </summary>
        public int Count { get; }
    }
}
