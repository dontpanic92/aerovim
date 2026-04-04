// <copyright file="AppSettingsTestScope.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests.Helpers;

using AeroVim.Settings;

/// <summary>
/// Redirects app-settings persistence to a temporary directory for a test.
/// </summary>
internal sealed class AppSettingsTestScope : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppSettingsTestScope"/> class.
    /// </summary>
    public AppSettingsTestScope()
    {
        this.DirectoryPath = Path.Combine(
            Path.GetTempPath(),
            "AeroVim.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.DirectoryPath);
        AppSettings.SetStorageDirectoryForTesting(this.DirectoryPath);
    }

    /// <summary>
    /// Gets the temporary settings directory.
    /// </summary>
    public string DirectoryPath { get; }

    /// <summary>
    /// Gets the redirected settings file path.
    /// </summary>
    public string SettingsPath => AppSettings.GetSettingsPathForTesting();

    /// <inheritdoc />
    public void Dispose()
    {
        AppSettings.ResetForTesting();

        if (Directory.Exists(this.DirectoryPath))
        {
            foreach (string file in Directory.GetFiles(this.DirectoryPath, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(this.DirectoryPath, true);
        }
    }
}
