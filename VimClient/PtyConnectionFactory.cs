// <copyright file="PtyConnectionFactory.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.VimClient;

using System.Runtime.InteropServices;

/// <summary>
/// Creates platform-appropriate PTY connections for the Vim backend.
/// </summary>
internal static class PtyConnectionFactory
{
    /// <summary>
    /// Create a PTY connection for the current platform.
    /// </summary>
    /// <param name="app">Absolute path to the executable.</param>
    /// <param name="args">Command-line arguments excluding argv[0].</param>
    /// <param name="environment">Environment variables for the child process.</param>
    /// <param name="cwd">Working directory for the child process.</param>
    /// <param name="rows">Initial terminal row count.</param>
    /// <param name="cols">Initial terminal column count.</param>
    /// <returns>The created PTY connection.</returns>
    public static IPtyConnection Create(
        string app,
        string[] args,
        IDictionary<string, string> environment,
        string cwd,
        int rows,
        int cols)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsPtyConnection(app, args, environment, cwd, rows, cols);
        }

        return new NativePtyConnection(app, args, environment, cwd, rows, cols);
    }
}
