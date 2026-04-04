// <copyright file="AppSettingsTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests;

using AeroVim.Settings;
using AeroVim.Tests.Helpers;
using NUnit.Framework;

/// <summary>
/// Tests application settings persistence.
/// </summary>
public class AppSettingsTests
{
    /// <summary>
    /// Round-tripping settings should preserve the persisted values.
    /// </summary>
    [Test]
    public void SaveAndReload_RoundTripsSettings()
    {
        using var scope = new AppSettingsTestScope();
        var settings = new AppSettings
        {
            NeovimPath = @"C:\Tools\nvim.exe",
            VimPath = @"C:\Tools\vim.exe",
            EditorType = EditorType.Vim,
            BackgroundOpacity = 0.42,
            EnableLigature = false,
            EnableBlurBehind = false,
            BlurType = 2,
            IsMaximized = true,
            WindowWidth = 1440,
            WindowHeight = 900,
            BackgroundColor = 0x102030,
            FallbackFonts = new List<string> { "Fira Code", "Noto Sans Mono" },
        };

        Assert.That(settings.Save(), Is.True);
        Assert.That(File.Exists(scope.SettingsPath), Is.True);

        var reloaded = new AppSettings();
        Assert.That(reloaded.Reload(), Is.True);
        Assert.That(reloaded.LastPersistenceError, Is.Empty);
        Assert.That(reloaded.NeovimPath, Is.EqualTo(settings.NeovimPath));
        Assert.That(reloaded.VimPath, Is.EqualTo(settings.VimPath));
        Assert.That(reloaded.EditorType, Is.EqualTo(settings.EditorType));
        Assert.That(reloaded.BackgroundOpacity, Is.EqualTo(settings.BackgroundOpacity));
        Assert.That(reloaded.EnableLigature, Is.EqualTo(settings.EnableLigature));
        Assert.That(reloaded.EnableBlurBehind, Is.EqualTo(settings.EnableBlurBehind));
        Assert.That(reloaded.BlurType, Is.EqualTo(settings.BlurType));
        Assert.That(reloaded.IsMaximized, Is.EqualTo(settings.IsMaximized));
        Assert.That(reloaded.WindowWidth, Is.EqualTo(settings.WindowWidth));
        Assert.That(reloaded.WindowHeight, Is.EqualTo(settings.WindowHeight));
        Assert.That(reloaded.BackgroundColor, Is.EqualTo(settings.BackgroundColor));
        Assert.That(reloaded.FallbackFonts, Is.EqualTo(settings.FallbackFonts));
    }

    /// <summary>
    /// Invalid JSON should surface an error during reload.
    /// </summary>
    [Test]
    public void Reload_InvalidJson_ReturnsFalseAndRecordsError()
    {
        using var scope = new AppSettingsTestScope();
        File.WriteAllText(scope.SettingsPath, "{ invalid json");

        var settings = new AppSettings();

        Assert.That(settings.Reload(), Is.False);
        Assert.That(settings.LastPersistenceError, Is.Not.Empty);
    }

    /// <summary>
    /// Save failures should be reported instead of silently succeeding.
    /// </summary>
    [Test]
    public void Save_WhenStorageDirectoryCannotBeCreated_ReturnsFalse()
    {
        string blockingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        File.WriteAllText(blockingPath, "occupied");
        AppSettings.SetStorageDirectoryForTesting(blockingPath);

        try
        {
            var settings = new AppSettings
            {
                NeovimPath = @"C:\Tools\nvim.exe",
            };

            Assert.That(settings.Save(), Is.False);
            Assert.That(settings.LastPersistenceError, Is.Not.Empty);
        }
        finally
        {
            AppSettings.ResetForTesting();
            File.Delete(blockingPath);
        }
    }
}
