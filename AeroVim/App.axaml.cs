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
    private NativeMenuItem? updateMenuItem;

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

            var mainWindow = new MainWindow(AppSettings.Default, fileArgs);
            desktop.MainWindow = mainWindow;

            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                    System.Runtime.InteropServices.OSPlatform.OSX))
            {
                mainWindow.UpdateService.UpdateAvailableChanged += this.OnUpdateAvailableChanged;
            }
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

    private void OnUpdateAvailableChanged(object? sender, UpdateInfo? info)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var menu = NativeMenu.GetMenu(this);
            if (info is not null && this.updateMenuItem is null)
            {
                this.updateMenuItem = new NativeMenuItem("Update Available\u2026");
                this.updateMenuItem.Click += this.OnUpdateMenuItemClicked;
                menu?.Items.Insert(0, this.updateMenuItem);
            }
            else if (info is null && this.updateMenuItem is not null)
            {
                menu?.Items.Remove(this.updateMenuItem);
                this.updateMenuItem.Click -= this.OnUpdateMenuItemClicked;
                this.updateMenuItem = null;
            }
        });
    }

    private async void OnUpdateMenuItemClicked(object? sender, EventArgs e)
    {
        if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is MainWindow mainWindow)
        {
            await mainWindow.ShowSettingsDialogAsync(
                initialPage: typeof(ViewModels.UpdatesPageViewModel));
        }
    }
}
