// <copyright file="CmdlineShowEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The <c>cmdline_show</c> event. Displays or updates the externalized
/// command line.
/// </summary>
/// <param name="Content">The content chunks (highlight ID + text).</param>
/// <param name="Pos">The cursor byte position.</param>
/// <param name="Firstc">The first character (e.g. ':', '/').</param>
/// <param name="Prompt">The input() prompt text.</param>
/// <param name="Indent">The indent level.</param>
/// <param name="Level">The nesting level.</param>
public record CmdlineShowEvent(IList<(int HlId, string Text)> Content, int Pos, string Firstc, string Prompt, int Indent, int Level) : IRedrawEvent;
