// <copyright file="SettingsViewModel.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.ViewModels;

using System.Collections.ObjectModel;
using AeroVim.Services;
using CommunityToolkit.Mvvm.ComponentModel;

/// <summary>
/// Orchestrator view model for the Settings dialog. Manages page navigation
/// and the OK/Cancel lifecycle.
/// </summary>
internal sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly AppSettings settings;

    [ObservableProperty]
    private SettingsPageViewModel? selectedPage;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
    /// </summary>
    /// <param name="settings">Application settings.</param>
    /// <param name="dialogService">The dialog service.</param>
    /// <param name="updateService">The update service.</param>
    /// <param name="promptText">Optional prompt text to display.</param>
    /// <param name="guiFontNames">The currently resolved Neovim guifont names.</param>
    /// <param name="initialPage">Optional page type to navigate to on open.</param>
    public SettingsViewModel(
        AppSettings settings,
        IDialogService dialogService,
        IUpdateService updateService,
        string? promptText = null,
        IReadOnlyList<string>? guiFontNames = null,
        Type? initialPage = null)
    {
        this.settings = settings;

        this.GeneralPage = new GeneralPageViewModel(settings, dialogService);
        this.AppearancePage = new AppearancePageViewModel(settings, dialogService, guiFontNames);
        this.ShellIntegrationPage = new ShellIntegrationPageViewModel(settings, dialogService);
        this.UpdatesPage = new UpdatesPageViewModel(settings, updateService);
        this.AboutPage = new AboutPageViewModel();

        this.Pages.Add(this.GeneralPage);
        this.Pages.Add(this.AppearancePage);
        this.Pages.Add(this.ShellIntegrationPage);
        this.Pages.Add(this.UpdatesPage);
        this.Pages.Add(this.AboutPage);

        this.SelectedPage = initialPage is not null
            ? this.Pages.FirstOrDefault(p => p.GetType() == initialPage) ?? this.GeneralPage
            : this.GeneralPage;

        if (!string.IsNullOrWhiteSpace(promptText))
        {
            this.PromptText = promptText;
            this.IsPromptVisible = true;
        }
    }

    /// <summary>
    /// Gets the list of available settings pages.
    /// </summary>
    public ObservableCollection<SettingsPageViewModel> Pages { get; } = new();

    /// <summary>
    /// Gets the General page view model.
    /// </summary>
    public GeneralPageViewModel GeneralPage { get; }

    /// <summary>
    /// Gets the Appearance page view model.
    /// </summary>
    public AppearancePageViewModel AppearancePage { get; }

    /// <summary>
    /// Gets the Shell Integration page view model.
    /// </summary>
    public ShellIntegrationPageViewModel ShellIntegrationPage { get; }

    /// <summary>
    /// Gets the Updates page view model.
    /// </summary>
    public UpdatesPageViewModel UpdatesPage { get; }

    /// <summary>
    /// Gets the About page view model.
    /// </summary>
    public AboutPageViewModel AboutPage { get; }

    /// <summary>
    /// Gets the prompt text displayed when the settings dialog is opened with a message.
    /// </summary>
    public string PromptText { get; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the prompt label should be visible.
    /// </summary>
    public bool IsPromptVisible { get; }

    /// <summary>
    /// Saves all page states back to settings and persists to disk.
    /// </summary>
    public void SaveAllToSettings()
    {
        this.GeneralPage.SaveToSettings();
        this.AppearancePage.SaveToSettings();
        this.ShellIntegrationPage.SaveToSettings();
        this.UpdatesPage.SaveToSettings();
        this.settings.Save();
    }
}
