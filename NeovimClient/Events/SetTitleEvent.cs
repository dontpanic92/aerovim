// <copyright file="SetTitleEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The SetTitle event.
/// </summary>
/// <param name="Title">The title.</param>
public record SetTitleEvent(string Title) : IRedrawEvent;
