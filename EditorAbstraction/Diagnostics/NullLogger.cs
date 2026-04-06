// <copyright file="NullLogger.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor.Diagnostics;

/// <summary>
/// A no-op <see cref="IAppLogger"/> implementation that silently discards
/// all messages. Useful as a default when no real logger is configured and
/// in unit tests.
/// </summary>
public sealed class NullLogger : IAppLogger
{
    /// <summary>
    /// Gets a shared singleton instance.
    /// </summary>
    public static NullLogger Instance { get; } = new();

    /// <inheritdoc/>
    public void Log(LogLevel level, string component, string message, Exception? exception = null)
    {
        // Intentionally empty.
    }
}
