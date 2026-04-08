// <copyright file="FontPriorityItem.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Dialogs;

/// <summary>
/// Represents a non-removable sentinel item in the font priority list.
/// The <see cref="Sentinel"/> property holds the raw sentinel string
/// (e.g. <c>$GUIFONT</c>) for persistence, and <see cref="DisplayLabel"/>
/// holds the user-visible label shown in the list box.
/// </summary>
internal sealed class FontPriorityItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FontPriorityItem"/> class.
    /// </summary>
    /// <param name="sentinel">The raw sentinel string.</param>
    /// <param name="displayLabel">The user-visible display label.</param>
    public FontPriorityItem(string sentinel, string displayLabel)
    {
        this.Sentinel = sentinel;
        this.DisplayLabel = displayLabel;
    }

    /// <summary>
    /// Gets the raw sentinel string (e.g. <c>$GUIFONT</c> or <c>$SYSTEM_MONO</c>).
    /// </summary>
    public string Sentinel { get; }

    /// <summary>
    /// Gets the user-visible display label.
    /// </summary>
    public string DisplayLabel { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return this.DisplayLabel;
    }
}
