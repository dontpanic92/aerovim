// <copyright file="IStartupDiagnostics.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor.Capabilities;

/// <summary>
/// Capability interface for backends that can report a classified startup
/// error message, allowing the frontend to show specific diagnostics
/// rather than generic failure messages.
/// </summary>
public interface IStartupDiagnostics
{
    /// <summary>
    /// Gets a classified error message describing why the last startup
    /// attempt failed, or <c>null</c> if startup succeeded or has not been
    /// attempted.
    /// </summary>
    string? LastStartupError { get; }
}
