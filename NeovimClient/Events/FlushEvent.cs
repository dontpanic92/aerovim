// <copyright file="FlushEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The <c>flush</c> event. Signals that a complete redraw batch is finished
/// and the UI should present the current state to the user.
/// </summary>
public class FlushEvent : IRedrawEvent
{
}
