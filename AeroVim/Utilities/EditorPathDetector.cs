// <copyright file="EditorPathDetector.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Utilities
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Detects editor executables (Vim, Neovim) in the system PATH.
    /// </summary>
    public static class EditorPathDetector
    {
        /// <summary>
        /// Searches the system PATH for a Vim executable.
        /// </summary>
        /// <returns>The full absolute path to the vim executable, or <c>null</c> if not found.</returns>
        public static string FindVimInPath()
        {
            var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "vim.exe" : "vim";
            return FindInPath(executableName);
        }

        /// <summary>
        /// Searches the system PATH for a given executable.
        /// </summary>
        /// <param name="executableName">The executable file name to search for.</param>
        /// <returns>The full absolute path to the executable, or <c>null</c> if not found.</returns>
        private static string FindInPath(string executableName)
        {
            var pathVar = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVar))
            {
                return null;
            }

            var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';

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
