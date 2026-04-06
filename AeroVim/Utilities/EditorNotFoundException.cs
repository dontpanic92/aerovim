// <copyright file="EditorNotFoundException.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Utilities;

/// <summary>
/// The configured editor executable was not found on disk or the path is
/// empty/unset.
/// </summary>
public class EditorNotFoundException : EditorStartupException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EditorNotFoundException"/> class.
    /// </summary>
    /// <param name="editorName">Display name of the editor (e.g. "Neovim").</param>
    /// <param name="path">The path that was tried.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    public EditorNotFoundException(string editorName, string? path, Exception? innerException = null)
        : base(BuildMessage(editorName, path), innerException)
    {
        this.EditorName = editorName;
        this.AttemptedPath = path;
    }

    /// <summary>
    /// Gets the display name of the editor that was not found.
    /// </summary>
    public string EditorName { get; }

    /// <summary>
    /// Gets the path that was tried, or <c>null</c> if no path was configured.
    /// </summary>
    public string? AttemptedPath { get; }

    private static string BuildMessage(string editorName, string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return $"{editorName} executable path is not configured.\nPlease set the path in Settings.";
        }

        return $"{editorName} executable was not found at:\n{path}\n\nPlease verify the path in Settings or use Detect to find it.";
    }
}
