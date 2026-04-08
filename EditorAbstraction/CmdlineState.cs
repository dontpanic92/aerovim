// <copyright file="CmdlineState.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor;

/// <summary>
/// Represents the state of the externalized command line, driven by
/// backends that support external command line rendering (e.g. Neovim's
/// <c>ext_cmdline</c> extension).
/// </summary>
public sealed class CmdlineState
{
    /// <summary>
    /// Gets or sets the content chunks. Each chunk is a tuple of (highlight
    /// attribute ID, text).
    /// </summary>
    public IList<(int HlId, string Text)> Content { get; set; } = new List<(int, string)>();

    /// <summary>
    /// Gets or sets the cursor byte position within the content.
    /// </summary>
    public int CursorPos { get; set; }

    /// <summary>
    /// Gets or sets the first character of the command line (e.g. ':', '/', '?').
    /// Empty when no built-in command line prompt is active.
    /// </summary>
    public string FirstChar { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the prompt text (for <c>input()</c> prompts).
    /// Empty when no prompt is active.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the indent level of the content.
    /// </summary>
    public int Indent { get; set; }

    /// <summary>
    /// Gets or sets the nesting level for recursive command lines.
    /// </summary>
    public int Level { get; set; }
}
