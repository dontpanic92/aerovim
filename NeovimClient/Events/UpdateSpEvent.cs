// <copyright file="UpdateSpEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// UpdateFg event.
/// </summary>
/// <param name="Color">Special color.</param>
public record UpdateSpEvent(int Color) : IRedrawEvent;
