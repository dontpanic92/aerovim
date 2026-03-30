// <copyright file="EditorPathDetector.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Utilities;

using System.Runtime.InteropServices;
using AeroVim.Settings;

/// <summary>
/// Detects editor executables and populates unset editor paths from the system PATH.
/// </summary>
public static class EditorPathDetector
{
    /// <summary>
    /// Searches the system PATH for a Neovim executable.
    /// </summary>
    /// <returns>The full absolute path to the nvim executable, or <c>null</c> if not found.</returns>
    public static string? FindNeovimInPath()
    {
        var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "nvim.exe" : "nvim";
        return FindInPath(executableName);
    }

    /// <summary>
    /// Searches the system PATH for a Vim executable.
    /// </summary>
    /// <returns>The full absolute path to the vim executable, or <c>null</c> if not found.</returns>
    public static string? FindVimInPath()
    {
        var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "vim.exe" : "vim";
        return FindInPath(executableName);
    }

    /// <summary>
    /// Fill unset editor paths using PATH-based detection.
    /// </summary>
    /// <param name="settings">Application settings.</param>
    public static void PopulateUnsetPaths(AppSettings settings)
    {
        if (settings.EditorType == EditorType.Vim)
        {
            if (string.IsNullOrEmpty(settings.VimPath))
            {
                var detectedVimPath = FindVimInPath();
                if (detectedVimPath != null)
                {
                    settings.VimPath = detectedVimPath;
                    settings.Save();
                }
            }

            return;
        }

        if (string.IsNullOrEmpty(settings.NeovimPath))
        {
            var detectedNeovimPath = FindNeovimInPath();
            if (detectedNeovimPath != null)
            {
                settings.NeovimPath = detectedNeovimPath;
                settings.Save();
            }
        }
    }

    /// <summary>
    /// Searches the system PATH for a given executable.
    /// </summary>
    /// <param name="executableName">The executable file name to search for.</param>
    /// <returns>The full absolute path to the executable, or <c>null</c> if not found.</returns>
    private static string? FindInPath(string executableName)
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
