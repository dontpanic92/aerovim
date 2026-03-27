// <copyright file="Cell.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor
{
    /// <summary>
    /// One cell in the editor screen grid.
    /// </summary>
    public struct Cell
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Cell"/> struct.
        /// </summary>
        /// <param name="character">The character in the cell.</param>
        /// <param name="foreground">Foreground color.</param>
        /// <param name="background">Background color.</param>
        /// <param name="special">Special color.</param>
        /// <param name="reverse">IsReverse.</param>
        /// <param name="italic">IsItalic.</param>
        /// <param name="bold">IsBold.</param>
        /// <param name="underline">IsUnderline.</param>
        /// <param name="undercurl">IsUnderCurl.</param>
        public Cell(int? character, int foreground, int background, int special, bool reverse, bool italic, bool bold, bool underline, bool undercurl)
        {
            this.ForegroundColor = foreground;
            this.BackgroundColor = background;
            this.SpecialColor = special;
            this.Reverse = reverse;
            this.Italic = italic;
            this.Bold = bold;
            this.Underline = underline;
            this.Undercurl = undercurl;
            this.Character = character;
        }

        /// <summary>
        /// Gets the color for foreground.
        /// </summary>
        public int ForegroundColor { get; private set; }

        /// <summary>
        /// Gets the color for background.
        /// </summary>
        public int BackgroundColor { get; private set; }

        /// <summary>
        /// Gets the color for undercurl.
        /// </summary>
        public int SpecialColor { get; private set; }

        /// <summary>
        /// Gets the character in the cell.
        /// </summary>
        public int? Character { get; private set; }

        /// <summary>
        /// Gets a value indicating whether foreground color and background color need to reverse.
        /// </summary>
        public bool Reverse { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the text is italic.
        /// </summary>
        public bool Italic { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the text is bold.
        /// </summary>
        public bool Bold { get; private set; }

        /// <summary>
        /// Gets a value indicating whether Underline is needed.
        /// </summary>
        public bool Underline { get; private set; }

        /// <summary>
        /// Gets a value indicating whether Undercurl is needed.
        /// </summary>
        public bool Undercurl { get; private set; }

        /// <summary>
        /// Set cell properties.
        /// </summary>
        /// <param name="character">The character in the cell.</param>
        /// <param name="foreground">Foreground color.</param>
        /// <param name="background">Background color.</param>
        /// <param name="special">Special color.</param>
        /// <param name="reverse">IsReverse.</param>
        /// <param name="italic">IsItalic.</param>
        /// <param name="bold">IsBold.</param>
        /// <param name="underline">IsUnderline.</param>
        /// <param name="undercurl">IsUnderCurl.</param>
        public void Set(int? character, int foreground, int background, int special, bool reverse, bool italic, bool bold, bool underline, bool undercurl)
        {
            this.ForegroundColor = foreground;
            this.BackgroundColor = background;
            this.SpecialColor = special;
            this.Reverse = reverse;
            this.Italic = italic;
            this.Bold = bold;
            this.Underline = underline;
            this.Undercurl = undercurl;
            this.Character = character;
        }

        /// <summary>
        /// Clear the cell.
        /// </summary>
        /// <param name="foreground">foreground color.</param>
        /// <param name="background">background color.</param>
        /// <param name="special">special color.</param>
        public void Clear(int foreground, int background, int special)
        {
            this.Set(' ', foreground, background, special, false, false, false, false, false);
        }
    }
}
