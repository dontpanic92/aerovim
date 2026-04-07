// <copyright file="ScrollEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// Scroll event.
/// </summary>
/// <param name="Count">The count of lines to scroll.</param>
public record ScrollEvent(int Count) : IRedrawEvent;
