// <copyright file="CmdlinePosEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The <c>cmdline_pos</c> event. Updates the cursor position in the
/// externalized command line.
/// </summary>
/// <param name="Pos">The cursor byte position.</param>
/// <param name="Level">The nesting level.</param>
public record CmdlinePosEvent(int Pos, int Level) : IRedrawEvent;
