// <copyright file="CmdlinePosEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The <c>cmdline_pos</c> event. Updates the cursor position in the
/// externalized command line.
/// </summary>
public class CmdlinePosEvent : IRedrawEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CmdlinePosEvent"/> class.
    /// </summary>
    /// <param name="pos">The cursor byte position.</param>
    /// <param name="level">The nesting level.</param>
    public CmdlinePosEvent(int pos, int level)
    {
        this.Pos = pos;
        this.Level = level;
    }

    /// <summary>
    /// Gets the cursor byte position.
    /// </summary>
    public int Pos { get; }

    /// <summary>
    /// Gets the nesting level.
    /// </summary>
    public int Level { get; }
}
