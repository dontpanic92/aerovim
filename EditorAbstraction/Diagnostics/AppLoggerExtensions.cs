// <copyright file="AppLoggerExtensions.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor.Diagnostics;

/// <summary>
/// Extension methods for creating component-scoped loggers.
/// </summary>
public static class AppLoggerExtensions
{
    /// <summary>
    /// Creates a <see cref="IComponentLogger"/> whose component name is
    /// derived from the type parameter's short name (e.g. <c>MsgPackRpc</c>).
    /// </summary>
    /// <typeparam name="T">The type whose name becomes the component tag.</typeparam>
    /// <param name="logger">The application logger to wrap.</param>
    /// <returns>A component-scoped logger.</returns>
    public static IComponentLogger For<T>(this IAppLogger logger)
    {
        return new ComponentLogger(logger, typeof(T).Name);
    }

    /// <summary>
    /// Creates a <see cref="IComponentLogger"/> with an explicit component name.
    /// </summary>
    /// <param name="logger">The application logger to wrap.</param>
    /// <param name="component">The component tag for log messages.</param>
    /// <returns>A component-scoped logger.</returns>
    public static IComponentLogger For(this IAppLogger logger, string component)
    {
        return new ComponentLogger(logger, component);
    }
}
