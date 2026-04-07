// <copyright file="FileAssocItem.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Dialogs;

using AeroVim.Services;
using Avalonia.Media;

/// <summary>
/// View model for a single row in the file association list.
/// </summary>
internal sealed class FileAssocItem
{
    /// <summary>
    /// Gets the file extension (e.g. ".txt").
    /// </summary>
    public required string Extension { get; init; }

    /// <summary>
    /// Gets the display status text.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets the brush used for the status text.
    /// </summary>
    public required IBrush StatusBrush { get; init; }

    /// <summary>
    /// Creates a <see cref="FileAssocItem"/> by querying the current
    /// registration state for <paramref name="extension"/>.
    /// </summary>
    /// <param name="extension">The file extension.</param>
    /// <returns>A new item instance.</returns>
    public static FileAssocItem Create(string extension)
    {
        bool registered = ShellIntegrationService.IsExtensionRegistered(extension);
        return new FileAssocItem
        {
            Extension = extension,
            Status = registered ? "Registered" : "Not Registered",
            StatusBrush = registered ? Brushes.Green : Brushes.Gray,
        };
    }
}
