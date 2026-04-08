// <copyright file="PopupMenuItem.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor;

/// <summary>
/// Represents a single item in the popup completion menu.
/// </summary>
public sealed class PopupMenuItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PopupMenuItem"/> class.
    /// </summary>
    /// <param name="word">The completion word (or abbreviation).</param>
    /// <param name="kind">The kind of completion (e.g. function, variable).</param>
    /// <param name="menu">Extra menu text (e.g. source file or type info).</param>
    /// <param name="info">Additional info text (e.g. documentation).</param>
    public PopupMenuItem(string word, string kind, string menu, string info)
    {
        this.Word = word;
        this.Kind = kind;
        this.Menu = menu;
        this.Info = info;
    }

    /// <summary>
    /// Gets the completion word (or abbreviation when present).
    /// </summary>
    public string Word { get; }

    /// <summary>
    /// Gets the kind of completion (e.g. function, variable).
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Gets extra menu text.
    /// </summary>
    public string Menu { get; }

    /// <summary>
    /// Gets additional info text (e.g. documentation).
    /// </summary>
    public string Info { get; }
}
