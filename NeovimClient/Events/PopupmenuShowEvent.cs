// <copyright file="PopupmenuShowEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

using AeroVim.Editor;

/// <summary>
/// The <c>popupmenu_show</c> event. Displays the popup completion menu.
/// </summary>
public class PopupmenuShowEvent : IRedrawEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PopupmenuShowEvent"/> class.
    /// </summary>
    /// <param name="items">The completion items.</param>
    /// <param name="selected">The initially selected item index (-1 if none).</param>
    /// <param name="row">The anchor row.</param>
    /// <param name="col">The anchor column.</param>
    /// <param name="grid">The anchor grid (-1 for ext_cmdline).</param>
    public PopupmenuShowEvent(PopupMenuItem[] items, int selected, int row, int col, int grid)
    {
        this.Items = items;
        this.Selected = selected;
        this.Row = row;
        this.Col = col;
        this.Grid = grid;
    }

    /// <summary>
    /// Gets the completion items.
    /// </summary>
    public PopupMenuItem[] Items { get; }

    /// <summary>
    /// Gets the initially selected item index (-1 if none).
    /// </summary>
    public int Selected { get; }

    /// <summary>
    /// Gets the anchor row.
    /// </summary>
    public int Row { get; }

    /// <summary>
    /// Gets the anchor column.
    /// </summary>
    public int Col { get; }

    /// <summary>
    /// Gets the anchor grid (-1 for ext_cmdline).
    /// </summary>
    public int Grid { get; }
}
