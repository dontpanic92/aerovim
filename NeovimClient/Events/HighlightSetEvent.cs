// <copyright file="HighlightSetEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events
{
    /// <summary>
    /// The HightlightSet event.
    /// </summary>
    public class HighlightSetEvent : IRedrawEvent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HighlightSetEvent"/> class.
        /// </summary>
        public HighlightSetEvent()
        {
            this.Foreground = null;
            this.Background = null;
            this.Special = null;
            this.Reverse = false;
            this.Italic = false;
            this.Bold = false;
            this.Underline = false;
            this.Undercurl = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HighlightSetEvent"/> class.
        /// </summary>
        /// <param name="foreground">Foreground color.</param>
        /// <param name="background">Background color.</param>
        /// <param name="special">Special color.</param>
        /// <param name="reverse">IsReverse.</param>
        /// <param name="italic">IsItalic.</param>
        /// <param name="bold">IsBold.</param>
        /// <param name="underline">IsUnderline.</param>
        /// <param name="undercurl">IsUnderCurl.</param>
        public HighlightSetEvent(int? foreground, int? background, int? special, bool reverse, bool italic, bool bold, bool underline, bool undercurl)
        {
            this.Foreground = foreground;
            this.Background = background;
            this.Special = special;
            this.Reverse = reverse;
            this.Italic = italic;
            this.Bold = bold;
            this.Underline = underline;
            this.Undercurl = undercurl;
        }

        /// <summary>
        /// Gets the Forground color.
        /// </summary>
        public int? Foreground { get; }

        /// <summary>
        /// Gets the Background color.
        /// </summary>
        public int? Background { get; }

        /// <summary>
        /// Gets the Special color.
        /// </summary>
        public int? Special { get; }

        /// <summary>
        /// Gets a value indicating whether foreground color and background color need to reverse.
        /// </summary>
        public bool Reverse { get; }

        /// <summary>
        /// Gets a value indicating whether the text is italic.
        /// </summary>
        public bool Italic { get; }

        /// <summary>
        /// Gets a value indicating whether the text is bold.
        /// </summary>
        public bool Bold { get; }

        /// <summary>
        /// Gets a value indicating whether Underline is needed.
        /// </summary>
        public bool Underline { get; }

        /// <summary>
        /// Gets a value indicating whether Undercurl is needed.
        /// </summary>
        public bool Undercurl { get; }
    }
}
