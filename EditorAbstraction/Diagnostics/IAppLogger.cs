// <copyright file="IAppLogger.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor.Diagnostics;

/// <summary>
/// A minimal logging abstraction shared by all AeroVim assemblies.
/// Implementations must be thread-safe.
/// </summary>
public interface IAppLogger
{
    /// <summary>
    /// Writes a log entry.
    /// </summary>
    /// <param name="level">Severity of the message.</param>
    /// <param name="component">
    /// Short identifier for the subsystem producing the message
    /// (e.g. "MsgPackRpc", "VimClient").
    /// </param>
    /// <param name="message">Human-readable description.</param>
    /// <param name="exception">Optional associated exception.</param>
    void Log(LogLevel level, string component, string message, Exception? exception = null);

    /// <summary>
    /// Logs an <see cref="LogLevel.Error"/> message.
    /// </summary>
    /// <param name="component">Subsystem identifier.</param>
    /// <param name="message">Human-readable description.</param>
    /// <param name="exception">Optional associated exception.</param>
    void Error(string component, string message, Exception? exception = null)
    {
        this.Log(LogLevel.Error, component, message, exception);
    }

    /// <summary>
    /// Logs a <see cref="LogLevel.Warning"/> message.
    /// </summary>
    /// <param name="component">Subsystem identifier.</param>
    /// <param name="message">Human-readable description.</param>
    /// <param name="exception">Optional associated exception.</param>
    void Warning(string component, string message, Exception? exception = null)
    {
        this.Log(LogLevel.Warning, component, message, exception);
    }

    /// <summary>
    /// Logs an <see cref="LogLevel.Info"/> message.
    /// </summary>
    /// <param name="component">Subsystem identifier.</param>
    /// <param name="message">Human-readable description.</param>
    void Info(string component, string message)
    {
        this.Log(LogLevel.Info, component, message);
    }

    /// <summary>
    /// Logs a <see cref="LogLevel.Debug"/> message.
    /// </summary>
    /// <param name="component">Subsystem identifier.</param>
    /// <param name="message">Human-readable description.</param>
    void Debug(string component, string message)
    {
        this.Log(LogLevel.Debug, component, message);
    }
}
