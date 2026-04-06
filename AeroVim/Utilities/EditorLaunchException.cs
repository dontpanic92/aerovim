// <copyright file="EditorLaunchException.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Utilities;

/// <summary>
/// The editor executable exists but could not be launched (e.g. permission
/// denied, not a valid executable, OS error).
/// </summary>
public class EditorLaunchException : EditorStartupException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EditorLaunchException"/> class.
    /// </summary>
    /// <param name="editorName">Display name of the editor.</param>
    /// <param name="path">The executable path that was tried.</param>
    /// <param name="innerException">The OS-level exception from the launch attempt.</param>
    public EditorLaunchException(string editorName, string path, Exception innerException)
        : base($"{editorName} could not be started from:\n{path}\n\n{innerException.Message}", innerException)
    {
        this.EditorName = editorName;
        this.AttemptedPath = path;
    }

    /// <summary>
    /// Gets the display name of the editor.
    /// </summary>
    public string EditorName { get; }

    /// <summary>
    /// Gets the path that was tried.
    /// </summary>
    public string AttemptedPath { get; }
}
