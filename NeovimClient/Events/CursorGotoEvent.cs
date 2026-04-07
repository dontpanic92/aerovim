// <copyright file="CursorGotoEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The CursorGoto event.
/// </summary>
/// <param name="Row">The row.</param>
/// <param name="Col">The column.</param>
public record CursorGotoEvent(uint Row, uint Col) : IRedrawEvent;
