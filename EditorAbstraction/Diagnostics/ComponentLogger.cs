// <copyright file="ComponentLogger.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor.Diagnostics;

/// <summary>
/// Default <see cref="IComponentLogger"/> that delegates to an
/// <see cref="IAppLogger"/> with a fixed component name.
/// </summary>
internal sealed class ComponentLogger : IComponentLogger
{
    private readonly IAppLogger inner;
    private readonly string component;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentLogger"/> class.
    /// </summary>
    /// <param name="inner">The underlying application logger.</param>
    /// <param name="component">The component name to attach to every message.</param>
    public ComponentLogger(IAppLogger inner, string component)
    {
        this.inner = inner;
        this.component = component;
    }

    /// <inheritdoc/>
    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        this.inner.Log(level, this.component, message, exception);
    }
}
