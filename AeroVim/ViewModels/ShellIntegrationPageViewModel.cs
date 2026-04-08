// <copyright file="ShellIntegrationPageViewModel.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.ViewModels;

using System.Collections.ObjectModel;
using AeroVim.Dialogs;
using AeroVim.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// View model for the Shell Integration settings page.
/// </summary>
internal sealed partial class ShellIntegrationPageViewModel : SettingsPageViewModel
{
    private readonly AppSettings settings;
    private readonly IDialogService dialogService;

    [ObservableProperty]
    private bool enableDragDrop;

    [ObservableProperty]
    private string contextMenuStatusText = "Checking...";

    [ObservableProperty]
    private string contextMenuButtonText = "Integrate";

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellIntegrationPageViewModel"/> class.
    /// </summary>
    /// <param name="settings">The application settings.</param>
    /// <param name="dialogService">The dialog service for confirm/input dialogs.</param>
    public ShellIntegrationPageViewModel(AppSettings settings, IDialogService dialogService)
        : base("Shell Integration")
    {
        this.settings = settings;
        this.dialogService = dialogService;
        this.enableDragDrop = settings.EnableDragDrop;

        this.PopulateFileAssociationList();
        this.RefreshContextMenuStatus();
    }

    /// <summary>
    /// Gets a value indicating whether shell integration is supported on this platform.
    /// </summary>
    public bool IsShellIntegrationSupported => ShellIntegrationService.IsSupported;

    /// <summary>
    /// Gets the file association items.
    /// </summary>
    public ObservableCollection<FileAssocItem> FileAssociations { get; } = new();

    /// <summary>
    /// Saves page state back to settings.
    /// </summary>
    public void SaveToSettings()
    {
        this.settings.EnableDragDrop = this.EnableDragDrop;
        this.settings.FileAssociationExtensions = this.GetExtensionList();
    }

    [RelayCommand]
    private void ToggleContextMenu()
    {
        bool isRegistered = ShellIntegrationService.IsContextMenuRegistered();
        ShellIntegrationService.SetContextMenuRegistration(!isRegistered);
        this.RefreshContextMenuStatus();
    }

    [RelayCommand]
    private void RegisterAllExtensions()
    {
        var extensions = this.GetExtensionList();
        ShellIntegrationService.RegisterAllExtensions(extensions);
        this.PopulateFileAssociationList();
    }

    [RelayCommand]
    private void UnregisterAllExtensions()
    {
        var extensions = this.GetExtensionList();
        ShellIntegrationService.UnregisterAllExtensions(extensions);
        this.PopulateFileAssociationList();
    }

    [RelayCommand]
    private async Task AddExtensionAsync()
    {
        var input = await this.dialogService.ShowInputAsync(
            "Enter file extensions separated by comma or semicolon:",
            "Add Extensions",
            "e.g. .txt, .log, .cfg");

        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        var existing = this.GetExtensionList();
        var parts = input.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var ext = ShellIntegrationService.NormaliseExtension(part);
            if (ext is not null && !existing.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                existing.Add(ext);
                this.FileAssociations.Add(FileAssocItem.Create(ext));
            }
        }
    }

    [RelayCommand]
    private void RemoveExtension(IList<object> selectedItems)
    {
        var toRemove = new List<FileAssocItem>();
        foreach (var item in selectedItems)
        {
            if (item is FileAssocItem row)
            {
                toRemove.Add(row);
            }
        }

        foreach (var row in toRemove)
        {
            ShellIntegrationService.SetExtensionRegistration(row.Extension, false);
            this.FileAssociations.Remove(row);
        }
    }

    [RelayCommand]
    private async Task ClearAllExtensionsAsync()
    {
        bool confirmed = await this.dialogService.ShowConfirmAsync(
            "This will unregister all file associations and clear the list. Continue?",
            "Clear All Extensions");

        if (!confirmed)
        {
            return;
        }

        var extensions = this.GetExtensionList();
        ShellIntegrationService.UnregisterAllExtensions(extensions);
        this.FileAssociations.Clear();
    }

    private void PopulateFileAssociationList()
    {
        if (!ShellIntegrationService.IsSupported)
        {
            return;
        }

        this.FileAssociations.Clear();

        var extensions = this.settings.FileAssociationExtensions.Count > 0
            ? this.settings.FileAssociationExtensions
            : ShellIntegrationService.DefaultExtensions.ToList();

        foreach (var ext in extensions.OrderBy(e => e, StringComparer.OrdinalIgnoreCase))
        {
            this.FileAssociations.Add(FileAssocItem.Create(ext));
        }
    }

    private List<string> GetExtensionList()
    {
        return this.FileAssociations.Select(fa => fa.Extension).ToList();
    }

    private void RefreshContextMenuStatus()
    {
        if (!ShellIntegrationService.IsSupported)
        {
            return;
        }

        bool isRegistered = ShellIntegrationService.IsContextMenuRegistered();
        this.ContextMenuStatusText = isRegistered
            ? "Integrated — \"Open with AeroVim\" is in the Explorer right-click menu."
            : "Not integrated.";
        this.ContextMenuButtonText = isRegistered ? "Remove" : "Integrate";

        this.PopulateFileAssociationList();
    }
}
