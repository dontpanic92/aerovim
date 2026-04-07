// <copyright file="NopEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// A event that does nothing.
/// </summary>
public record NopEvent() : IRedrawEvent;
