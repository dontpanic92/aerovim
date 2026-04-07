// <copyright file="ModeChangeEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The mode change event.
/// </summary>
/// <param name="ModeName">The name of this mode.</param>
/// <param name="Index">The index of this mode.</param>
public record ModeChangeEvent(string ModeName, int Index) : IRedrawEvent;
