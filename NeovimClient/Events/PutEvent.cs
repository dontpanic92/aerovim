// <copyright file="PutEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The Put event.
/// </summary>
/// <param name="Text">The text to be rendered.</param>
public record PutEvent(IList<string?> Text) : IRedrawEvent;
