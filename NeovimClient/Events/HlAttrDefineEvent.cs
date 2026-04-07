// <copyright file="HlAttrDefineEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

using AeroVim.Editor.Utilities;

/// <summary>
/// The <c>hl_attr_define</c> event. Adds a highlight entry to the highlight
/// attribute table used by <c>grid_line</c> events.
/// </summary>
/// <param name="Id">The highlight ID.</param>
/// <param name="RgbAttrs">The RGB highlight attributes.</param>
public record HlAttrDefineEvent(int Id, HighlightAttributes RgbAttrs) : IRedrawEvent;
