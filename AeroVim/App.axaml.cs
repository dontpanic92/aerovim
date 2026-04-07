// <copyright file="App.axaml.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim;

using AeroVim.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

/// <summary>
/// The Avalonia application.
/// </summary>
public class App : Application
{
    /// <inheritdoc />
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Filter out Avalonia framework args; keep only file paths.
            var fileArgs = desktop.Args?
                .Where(a => !string.IsNullOrWhiteSpace(a) && !a.StartsWith('-'))
                .ToList();

            desktop.MainWindow = new MainWindow(AppSettings.Default, fileArgs);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Handles the Preferences menu item click by opening the settings dialog.
    /// </summary>
    private async void OnPreferencesClicked(object? sender, EventArgs e)
    {
        if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is MainWindow mainWindow)
        {
            await mainWindow.ShowSettingsDialogAsync();
        }
    }
}
