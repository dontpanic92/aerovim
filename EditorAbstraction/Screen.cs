// <copyright file="Screen.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor
{
    /// <summary>
    /// Represents the editor screen state for rendering.
    /// </summary>
    public sealed class Screen
    {
        /// <summary>
        /// Gets or sets the cell grid.
        /// </summary>
        public Cell[,] Cells { get; set; }

        /// <summary>
        /// Gets or sets the cursor position.
        /// </summary>
        public (int Row, int Col) CursorPosition { get; set; }

        /// <summary>
        /// Gets or sets the foreground color.
        /// </summary>
        public int ForegroundColor { get; set; }

        /// <summary>
        /// Gets or sets the background color.
        /// </summary>
        public int BackgroundColor { get; set; }
    }
}
