// <copyright file="PopupmenuSelectEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The <c>popupmenu_select</c> event. Changes the selected item in the
/// popup completion menu.
/// </summary>
/// <param name="Selected">The selected item index (-1 if none).</param>
public record PopupmenuSelectEvent(int Selected) : IRedrawEvent;
