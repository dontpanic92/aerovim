// <copyright file="EditorStartupException.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Utilities;

/// <summary>
/// Base exception for classified editor startup failures. Each subclass
/// carries a <see cref="UserMessage"/> suitable for direct display in a
/// dialog and preserves the original exception for logging.
/// </summary>
public class EditorStartupException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EditorStartupException"/> class.
    /// </summary>
    /// <param name="userMessage">User-facing description of the failure.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    public EditorStartupException(string userMessage, Exception? innerException = null)
        : base(userMessage, innerException)
    {
        this.UserMessage = userMessage;
    }

    /// <summary>
    /// Gets a user-friendly, actionable description of the failure suitable
    /// for display in a dialog.
    /// </summary>
    public string UserMessage { get; }
}
