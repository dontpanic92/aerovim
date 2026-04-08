// <copyright file="SettingsWindow.axaml.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Dialogs;

using AeroVim.Services;
using AeroVim.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

/// <summary>
/// Interaction logic for SettingsWindow.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsWindow"/> class.
    /// Used by the XAML designer; runtime code should use
    /// <see cref="SettingsWindow(AppSettings, string?, IReadOnlyList{string}?)"/>.
    /// </summary>
    public SettingsWindow()
        : this(AppSettings.Default, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsWindow"/> class.
    /// </summary>
    /// <param name="settings">Application settings.</param>
    /// <param name="promptText">Text for prompt.</param>
    /// <param name="guiFontNames">The currently resolved Neovim guifont names, if available.</param>
    public SettingsWindow(AppSettings settings, string? promptText, IReadOnlyList<string>? guiFontNames = null)
    {
        this.settings = settings;
        var dialogService = new DialogService(this);
        this.DataContext = new SettingsViewModel(settings, dialogService, promptText, guiFontNames);
        this.InitializeComponent();
        this.Closing += this.OnWindowClosing;
    }

    /// <summary>
    /// Reason of closing the window.
    /// </summary>
    public enum Result
    {
        /// <summary>
        /// Window is not closed yet.
        /// </summary>
        NotClosed,

        /// <summary>
        /// Window closed due to Ok button clicked.
        /// </summary>
        Ok,

        /// <summary>
        /// Window closed due to Cancel button clicked.
        /// </summary>
        Cancel,
    }

    /// <summary>
    /// Gets the reason of closing the window.
    /// </summary>
    public Result CloseReason { get; private set; } = Result.NotClosed;

    private SettingsViewModel? ViewModel => this.DataContext as SettingsViewModel;

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        this.CloseReason = Result.Ok;
        this.Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        this.CloseReason = Result.Cancel;
        this.Close();
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        switch (this.CloseReason)
        {
            case Result.Ok:
                this.ViewModel?.SaveAllToSettings();
                break;
            case Result.Cancel:
                this.settings.Reload();
                break;
            case Result.NotClosed:
                this.CloseReason = Result.Cancel;
                this.settings.Reload();
                break;
        }
    }
}
