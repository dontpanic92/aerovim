// <copyright file="LogLevel.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor.Diagnostics;

/// <summary>
/// Specifies the severity of a log message.
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// Verbose diagnostic detail, typically only useful during development.
    /// </summary>
    Debug,

    /// <summary>
    /// Informational messages that describe normal operation.
    /// </summary>
    Info,

    /// <summary>
    /// An unexpected condition that does not prevent the application from
    /// continuing, but may indicate a problem.
    /// </summary>
    Warning,

    /// <summary>
    /// A failure that prevents an operation from completing.
    /// </summary>
    Error,
}
