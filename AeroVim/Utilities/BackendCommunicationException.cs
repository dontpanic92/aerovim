// <copyright file="BackendCommunicationException.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Utilities;

/// <summary>
/// The editor process started but communication (RPC or PTY I/O) failed.
/// </summary>
public class BackendCommunicationException : EditorStartupException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BackendCommunicationException"/> class.
    /// </summary>
    /// <param name="editorName">Display name of the editor.</param>
    /// <param name="detail">Description of the communication failure.</param>
    /// <param name="innerException">The underlying exception.</param>
    public BackendCommunicationException(string editorName, string detail, Exception? innerException = null)
        : base($"{editorName} started but communication failed:\n{detail}", innerException)
    {
        this.EditorName = editorName;
    }

    /// <summary>
    /// Gets the display name of the editor.
    /// </summary>
    public string EditorName { get; }
}
