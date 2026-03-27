// <copyright file="ModeInfo.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Utilities
{
    using System.Collections.Generic;

    /// <summary>
    /// nvim mode info. ref: https://neovim.io/doc/user/ui.html.
    /// </summary>
    public class ModeInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModeInfo"/> class.
        /// </summary>
        /// <param name="info">Mode info.</param>
        public ModeInfo(IDictionary<string, string> info)
        {
            this.CursorShape = CursorShape.Block;
            if (info.TryGetValue("cursor_shape", out var cursorShape))
            {
                if (System.Enum.TryParse<CursorShape>(cursorShape, true, out var shape))
                {
                    this.CursorShape = shape;
                }
            }

            this.CellPercentage = 100;
            if (info.TryGetValue("cell_percentage", out var cellPercentage))
            {
                if (int.TryParse(cellPercentage, out var percentage))
                {
                    this.CellPercentage = percentage;
                }
            }

            this.CursorBlinking = CursorBlinking.BlinkOff;
            if (info.TryGetValue("blinkwait", out var wait) && int.TryParse(wait, out var intWait) && intWait == 1)
            {
                this.CursorBlinking = CursorBlinking.BlinkWait;
            }
            else if (info.TryGetValue("blinkon", out var on) && int.TryParse(wait, out var intOn) && intOn == 1)
            {
                this.CursorBlinking = CursorBlinking.BlinkOn;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ModeInfo"/> class.
        /// </summary>
        /// <param name="cursorShape">Cursor shape info.</param>
        /// <param name="cellPercentage">Cursor size info.</param>
        /// <param name="cursorBlinking">Cursor blinking info.</param>
        public ModeInfo(CursorShape cursorShape, int cellPercentage, CursorBlinking cursorBlinking)
        {
            this.CursorShape = cursorShape;
            this.CellPercentage = cellPercentage;
            this.CursorBlinking = cursorBlinking;
        }

        /// <summary>
        /// Gets the cursor shape.
        /// </summary>
        public CursorShape CursorShape { get; }

        /// <summary>
        /// Gets the percentage of the cursor should occupy.
        /// </summary>
        public int CellPercentage { get; }

        /// <summary>
        /// Gets the blinking setting for the cursor.
        /// </summary>
        public CursorBlinking CursorBlinking { get; }
    }
}
