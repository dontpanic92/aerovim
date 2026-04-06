// <copyright file="EditorCrashedException.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Utilities;

/// <summary>
/// The editor process started but exited immediately with a non-zero exit
/// code, before producing any useful output.
/// </summary>
public class EditorCrashedException : EditorStartupException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EditorCrashedException"/> class.
    /// </summary>
    /// <param name="editorName">Display name of the editor.</param>
    /// <param name="path">The executable path.</param>
    /// <param name="exitCode">The process exit code.</param>
    /// <param name="innerException">Optional underlying exception.</param>
    public EditorCrashedException(string editorName, string path, int exitCode, Exception? innerException = null)
        : base($"{editorName} exited immediately (exit code {exitCode}).\nThe executable at '{path}' may be incompatible or misconfigured.", innerException)
    {
        this.EditorName = editorName;
        this.AttemptedPath = path;
        this.ExitCode = exitCode;
    }

    /// <summary>
    /// Gets the display name of the editor.
    /// </summary>
    public string EditorName { get; }

    /// <summary>
    /// Gets the path that was tried.
    /// </summary>
    public string AttemptedPath { get; }

    /// <summary>
    /// Gets the process exit code.
    /// </summary>
    public int ExitCode { get; }
}
