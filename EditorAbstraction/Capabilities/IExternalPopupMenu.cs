// <copyright file="IExternalPopupMenu.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor.Capabilities;

/// <summary>
/// Capability interface for backends that support an externalized popup
/// completion menu (e.g. Neovim's <c>ext_popupmenu</c>).
/// </summary>
public interface IExternalPopupMenu
{
    /// <summary>
    /// Gets the current popup completion menu items, or <c>null</c> if the
    /// popup menu is not visible.
    /// </summary>
    PopupMenuItem[]? PopupItems { get; }

    /// <summary>
    /// Gets the currently selected popup menu item index, or -1 if no item
    /// is selected.
    /// </summary>
    int PopupSelected { get; }

    /// <summary>
    /// Gets the popup menu anchor position (row, col) in the grid, or
    /// <c>null</c> if the popup menu is not visible.
    /// </summary>
    (int Row, int Col)? PopupAnchor { get; }
}
