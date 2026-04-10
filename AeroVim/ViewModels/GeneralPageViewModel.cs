// <copyright file="GeneralPageViewModel.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.ViewModels;

using AeroVim.Services;
using AeroVim.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// View model for the General settings page.
/// </summary>
internal sealed partial class GeneralPageViewModel : SettingsPageViewModel
{
    private readonly AppSettings settings;
    private readonly IDialogService dialogService;

    [ObservableProperty]
    private int editorTypeIndex;

    [ObservableProperty]
    private string neovimPath = string.Empty;

    [ObservableProperty]
    private string vimPath = string.Empty;

    [ObservableProperty]
    private bool enableExternalUI;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneralPageViewModel"/> class.
    /// </summary>
    /// <param name="settings">The application settings.</param>
    /// <param name="dialogService">The dialog service for file pickers and messages.</param>
    public GeneralPageViewModel(AppSettings settings, IDialogService dialogService)
        : base("General")
    {
        this.settings = settings;
        this.dialogService = dialogService;
        this.editorTypeIndex = (int)settings.EditorType;
        this.neovimPath = settings.NeovimPath;
        this.vimPath = settings.VimPath;
        this.enableExternalUI = settings.EnableExternalUI;
    }

    /// <summary>
    /// Gets a value indicating whether the Vim backend is selected.
    /// </summary>
    public bool IsVimSelected => this.EditorTypeIndex == 1;

    /// <summary>
    /// Saves page state back to settings.
    /// </summary>
    public void SaveToSettings()
    {
        this.settings.EditorType = (EditorType)this.EditorTypeIndex;
        this.settings.NeovimPath = this.NeovimPath;
        this.settings.VimPath = this.VimPath;
        this.settings.EnableExternalUI = this.EnableExternalUI;
    }

    /// <inheritdoc/>
    partial void OnEditorTypeIndexChanged(int value)
    {
        this.OnPropertyChanged(nameof(this.IsVimSelected));
    }

    [RelayCommand]
    private async Task BrowseNeovimAsync()
    {
        var path = await this.dialogService.ShowFilePickerAsync("Select Neovim executable");
        if (path is not null)
        {
            this.NeovimPath = path;
        }
    }

    [RelayCommand]
    private async Task DetectNeovimAsync()
    {
        var detected = EditorPathDetector.FindNeovimInPath();
        if (detected is null)
        {
            await this.dialogService.ShowMessageAsync("Neovim was not found in PATH.", "Detect Neovim");
            return;
        }

        if (string.IsNullOrEmpty(this.NeovimPath))
        {
            this.NeovimPath = detected;
        }
        else if (!string.Equals(this.NeovimPath, detected, StringComparison.OrdinalIgnoreCase))
        {
            bool replace = await this.dialogService.ShowConfirmAsync(
                $"Detected Neovim at:\n{detected}\n\nReplace the current path?",
                "Detect Neovim");
            if (replace)
            {
                this.NeovimPath = detected;
            }
        }
    }

    [RelayCommand]
    private async Task BrowseVimAsync()
    {
        var path = await this.dialogService.ShowFilePickerAsync("Select Vim executable");
        if (path is not null)
        {
            this.VimPath = path;
        }
    }

    [RelayCommand]
    private async Task DetectVimAsync()
    {
        var detected = EditorPathDetector.FindVimInPath();
        if (detected is null)
        {
            await this.dialogService.ShowMessageAsync("Vim was not found in PATH.", "Detect Vim");
            return;
        }

        if (string.IsNullOrEmpty(this.VimPath))
        {
            this.VimPath = detected;
        }
        else if (!string.Equals(this.VimPath, detected, StringComparison.OrdinalIgnoreCase))
        {
            bool replace = await this.dialogService.ShowConfirmAsync(
                $"Detected Vim at:\n{detected}\n\nReplace the current path?",
                "Detect Vim");
            if (replace)
            {
                this.VimPath = detected;
            }
        }
    }
}
