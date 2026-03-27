// <copyright file="NeovimPathDetector.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Utilities
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Detects the Neovim executable in the system PATH.
    /// </summary>
    public static class NeovimPathDetector
    {
        /// <summary>
        /// Searches the system PATH for a Neovim executable.
        /// </summary>
        /// <returns>The full absolute path to the nvim executable, or <c>null</c> if not found.</returns>
        public static string FindNeovimInPath()
        {
            var pathVar = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVar))
            {
                return null;
            }

            var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
            var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "nvim.exe" : "nvim";

            foreach (var directory in pathVar.Split(separator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(directory.Trim(), executableName);
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }

            return null;
        }
    }
}
