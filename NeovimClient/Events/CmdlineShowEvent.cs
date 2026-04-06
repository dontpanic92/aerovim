// <copyright file="CmdlineShowEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The <c>cmdline_show</c> event. Displays or updates the externalized
/// command line.
/// </summary>
public class CmdlineShowEvent : IRedrawEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CmdlineShowEvent"/> class.
    /// </summary>
    /// <param name="content">The content chunks (highlight ID + text).</param>
    /// <param name="pos">The cursor byte position.</param>
    /// <param name="firstc">The first character (e.g. ':', '/').</param>
    /// <param name="prompt">The input() prompt text.</param>
    /// <param name="indent">The indent level.</param>
    /// <param name="level">The nesting level.</param>
    public CmdlineShowEvent(
        IList<(int HlId, string Text)> content,
        int pos,
        string firstc,
        string prompt,
        int indent,
        int level)
    {
        this.Content = content;
        this.Pos = pos;
        this.Firstc = firstc;
        this.Prompt = prompt;
        this.Indent = indent;
        this.Level = level;
    }

    /// <summary>
    /// Gets the content chunks.
    /// </summary>
    public IList<(int HlId, string Text)> Content { get; }

    /// <summary>
    /// Gets the cursor byte position.
    /// </summary>
    public int Pos { get; }

    /// <summary>
    /// Gets the first character of the command line.
    /// </summary>
    public string Firstc { get; }

    /// <summary>
    /// Gets the input() prompt text.
    /// </summary>
    public string Prompt { get; }

    /// <summary>
    /// Gets the indent level.
    /// </summary>
    public int Indent { get; }

    /// <summary>
    /// Gets the nesting level.
    /// </summary>
    public int Level { get; }
}
