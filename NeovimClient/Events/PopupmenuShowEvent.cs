// <copyright file="PopupmenuShowEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

using AeroVim.Editor;

/// <summary>
/// The <c>popupmenu_show</c> event. Displays the popup completion menu.
/// </summary>
/// <param name="Items">The completion items.</param>
/// <param name="Selected">The initially selected item index (-1 if none).</param>
/// <param name="Row">The anchor row.</param>
/// <param name="Col">The anchor column.</param>
/// <param name="Grid">The anchor grid (-1 for ext_cmdline).</param>
public record PopupmenuShowEvent(PopupMenuItem[] Items, int Selected, int Row, int Col, int Grid) : IRedrawEvent;
