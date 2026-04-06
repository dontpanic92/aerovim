// <copyright file="IComponentLogger.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor.Diagnostics;

/// <summary>
/// A logger scoped to a specific component. All messages are automatically
/// tagged with the component name, removing the need to pass it on every call.
/// </summary>
/// <remarks>
/// Obtain an instance via <see cref="AppLoggerExtensions.For{T}"/> or
/// <see cref="AppLoggerExtensions.For"/>.
/// </remarks>
public interface IComponentLogger
{
    /// <summary>
    /// Logs a message at the specified level.
    /// </summary>
    /// <param name="level">Severity of the message.</param>
    /// <param name="message">Human-readable description.</param>
    /// <param name="exception">Optional associated exception.</param>
    void Log(LogLevel level, string message, Exception? exception = null);

    /// <summary>
    /// Logs an <see cref="LogLevel.Error"/> message.
    /// </summary>
    /// <param name="message">Human-readable description.</param>
    /// <param name="exception">Optional associated exception.</param>
    void Error(string message, Exception? exception = null)
    {
        this.Log(LogLevel.Error, message, exception);
    }

    /// <summary>
    /// Logs a <see cref="LogLevel.Warning"/> message.
    /// </summary>
    /// <param name="message">Human-readable description.</param>
    /// <param name="exception">Optional associated exception.</param>
    void Warning(string message, Exception? exception = null)
    {
        this.Log(LogLevel.Warning, message, exception);
    }

    /// <summary>
    /// Logs an <see cref="LogLevel.Info"/> message.
    /// </summary>
    /// <param name="message">Human-readable description.</param>
    void Info(string message)
    {
        this.Log(LogLevel.Info, message);
    }

    /// <summary>
    /// Logs a <see cref="LogLevel.Debug"/> message.
    /// </summary>
    /// <param name="message">Human-readable description.</param>
    void Debug(string message)
    {
        this.Log(LogLevel.Debug, message);
    }
}
