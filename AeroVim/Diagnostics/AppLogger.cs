// <copyright file="AppLogger.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Diagnostics;

using AeroVim.Editor.Diagnostics;

/// <summary>
/// Application-wide logger accessor. Must be initialized once at startup
/// via <see cref="Initialize"/>. Falls back to <see cref="NullLogger"/>
/// when not initialized.
/// </summary>
internal static class AppLogger
{
    private static IAppLogger instance = NullLogger.Instance;

    /// <summary>
    /// Gets the application logger instance.
    /// </summary>
    public static IAppLogger Instance => instance;

    /// <summary>
    /// Gets the log file path when a <see cref="FileLogger"/> is active,
    /// or <c>null</c> if logging was not configured.
    /// </summary>
    public static string? LogFilePath => (instance as FileLogger)?.LogFilePath;

    /// <summary>
    /// Creates a component-scoped logger whose tag is derived from
    /// <typeparamref name="T"/>'s short name.
    /// </summary>
    /// <typeparam name="T">The type whose name becomes the component tag.</typeparam>
    /// <returns>A component-scoped logger.</returns>
    public static IComponentLogger For<T>()
    {
        return instance.For<T>();
    }

    /// <summary>
    /// Creates a component-scoped logger with an explicit component name.
    /// </summary>
    /// <param name="component">The component tag for log messages.</param>
    /// <returns>A component-scoped logger.</returns>
    public static IComponentLogger For(string component)
    {
        return instance.For(component);
    }

    /// <summary>
    /// Sets the global logger instance. Should be called exactly once
    /// during application startup.
    /// </summary>
    /// <param name="logger">The logger to use application-wide.</param>
    public static void Initialize(IAppLogger logger)
    {
        instance = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Disposes the current logger if it implements <see cref="IDisposable"/>
    /// and resets the instance to <see cref="NullLogger"/>.
    /// </summary>
    public static void Shutdown()
    {
        if (instance is IDisposable disposable)
        {
            disposable.Dispose();
        }

        instance = NullLogger.Instance;
    }
}
