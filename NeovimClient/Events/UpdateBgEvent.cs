// <copyright file="UpdateBgEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events
{
    /// <summary>
    /// UpdateFg event.
    /// </summary>
    public class UpdateBgEvent : IRedrawEvent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateBgEvent"/> class.
        /// </summary>
        /// <param name="color">Background color.</param>
        public UpdateBgEvent(int color)
        {
            this.Color = color;
        }

        /// <summary>
        /// Gets the color.
        /// </summary>
        public int Color { get; }
    }
}
