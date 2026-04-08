// <copyright file="AppearancePageViewModel.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.ViewModels;

using System.Collections.ObjectModel;
using AeroVim.Dialogs;
using AeroVim.Editor.Utilities;
using AeroVim.Services;
using AeroVim.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// View model for the Appearance settings page.
/// </summary>
internal sealed partial class AppearancePageViewModel : SettingsPageViewModel
{
    private readonly AppSettings settings;
    private readonly IDialogService dialogService;
    private readonly IReadOnlyList<string> currentGuiFontNames;

    [ObservableProperty]
    private bool enableLigature;

    [ObservableProperty]
    private bool enableBlurBehind;

    [ObservableProperty]
    private int blurType;

    [ObservableProperty]
    private double backgroundOpacity;

    [ObservableProperty]
    private int selectedFontIndex = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppearancePageViewModel"/> class.
    /// </summary>
    /// <param name="settings">The application settings.</param>
    /// <param name="dialogService">The dialog service for font picker.</param>
    /// <param name="guiFontNames">The currently resolved Neovim guifont names.</param>
    public AppearancePageViewModel(AppSettings settings, IDialogService dialogService, IReadOnlyList<string>? guiFontNames = null)
        : base("Appearance")
    {
        this.settings = settings;
        this.dialogService = dialogService;
        this.currentGuiFontNames = guiFontNames ?? Array.Empty<string>();

        this.enableLigature = settings.EnableLigature;
        this.enableBlurBehind = settings.EnableBlurBehind;
        this.blurType = settings.BlurType;
        this.backgroundOpacity = settings.BackgroundOpacity;

        foreach (var entry in settings.FallbackFonts)
        {
            this.FontItems.Add(this.CreateFontDisplayItem(entry));
        }
    }

    /// <summary>
    /// Gets the font priority list items.
    /// </summary>
    public ObservableCollection<object> FontItems { get; } = new();

    /// <summary>
    /// Gets the opacity label text.
    /// </summary>
    public string OpacityLabel => this.BackgroundOpacity.ToString("F2");

    /// <summary>
    /// Gets a value indicating whether the Transparent radio button is available.
    /// </summary>
    public bool IsTransparentAvailable => Helpers.TransparentAvailable();

    /// <summary>
    /// Gets a value indicating whether the Gaussian blur radio button is available.
    /// </summary>
    public bool IsGaussianAvailable => Helpers.GaussianBlurAvailable();

    /// <summary>
    /// Gets a value indicating whether the Acrylic blur radio button is available.
    /// </summary>
    public bool IsAcrylicAvailable => Helpers.AcrylicBlurAvailable();

    /// <summary>
    /// Gets a value indicating whether the Mica radio button is available.
    /// </summary>
    public bool IsMicaAvailable => Helpers.MicaAvailable();

    /// <summary>
    /// Gets a value indicating whether the Transparent radio option should be enabled.
    /// </summary>
    public bool IsTransparentEnabled => this.EnableBlurBehind && this.IsTransparentAvailable;

    /// <summary>
    /// Gets a value indicating whether the Gaussian radio option should be enabled.
    /// </summary>
    public bool IsGaussianEnabled => this.EnableBlurBehind && this.IsGaussianAvailable;

    /// <summary>
    /// Gets a value indicating whether the Acrylic radio option should be enabled.
    /// </summary>
    public bool IsAcrylicEnabled => this.EnableBlurBehind && this.IsAcrylicAvailable;

    /// <summary>
    /// Gets a value indicating whether the Mica radio option should be enabled.
    /// </summary>
    public bool IsMicaEnabled => this.EnableBlurBehind && this.IsMicaAvailable;

    /// <summary>
    /// Gets a value indicating whether the opacity slider should be enabled.
    /// </summary>
    public bool IsOpacityEnabled => this.EnableBlurBehind;

    /// <summary>
    /// Gets a value indicating whether the selected font can be removed (not a sentinel).
    /// </summary>
    public bool CanRemoveFont =>
        this.SelectedFontIndex >= 0 &&
        GetRawFontEntry(this.FontItems[this.SelectedFontIndex]) is string raw &&
        !FontPriorityList.IsSentinel(raw);

    /// <summary>
    /// Gets a value indicating whether the selected font can be moved up.
    /// </summary>
    public bool CanMoveUp => this.SelectedFontIndex > 0;

    /// <summary>
    /// Gets a value indicating whether the selected font can be moved down.
    /// </summary>
    public bool CanMoveDown => this.SelectedFontIndex >= 0 && this.SelectedFontIndex < this.FontItems.Count - 1;

    /// <summary>
    /// Saves page state back to settings.
    /// </summary>
    public void SaveToSettings()
    {
        this.settings.EnableLigature = this.EnableLigature;
        this.settings.EnableBlurBehind = this.EnableBlurBehind;
        this.settings.BlurType = this.BlurType;
        this.settings.BackgroundOpacity = this.BackgroundOpacity;
        this.settings.FallbackFonts = this.GetRawFontList();
    }

    private static string? GetRawFontEntry(object? item)
    {
        if (item is FontPriorityItem sentinel)
        {
            return sentinel.Sentinel;
        }

        if (item is string fontName)
        {
            return fontName;
        }

        return null;
    }

    /// <inheritdoc/>
    partial void OnEnableBlurBehindChanged(bool value)
    {
        this.settings.EnableBlurBehind = value;
        this.OnPropertyChanged(nameof(this.IsTransparentEnabled));
        this.OnPropertyChanged(nameof(this.IsGaussianEnabled));
        this.OnPropertyChanged(nameof(this.IsAcrylicEnabled));
        this.OnPropertyChanged(nameof(this.IsMicaEnabled));
        this.OnPropertyChanged(nameof(this.IsOpacityEnabled));
    }

    /// <inheritdoc/>
    partial void OnBlurTypeChanged(int value)
    {
        this.settings.BlurType = value;
    }

    /// <inheritdoc/>
    partial void OnBackgroundOpacityChanged(double value)
    {
        this.settings.BackgroundOpacity = value;
        this.OnPropertyChanged(nameof(this.OpacityLabel));
    }

    /// <inheritdoc/>
    partial void OnEnableLigatureChanged(bool value)
    {
        this.settings.EnableLigature = value;
    }

    /// <inheritdoc/>
    partial void OnSelectedFontIndexChanged(int value)
    {
        this.OnPropertyChanged(nameof(this.CanRemoveFont));
        this.OnPropertyChanged(nameof(this.CanMoveUp));
        this.OnPropertyChanged(nameof(this.CanMoveDown));
    }

    [RelayCommand]
    private async Task FontAddAsync()
    {
        var fontName = await this.dialogService.ShowFontPickerAsync();
        if (!string.IsNullOrWhiteSpace(fontName))
        {
            int insertIndex = this.SelectedFontIndex >= 0
                ? this.SelectedFontIndex
                : this.FontItems.Count;
            this.FontItems.Insert(insertIndex, fontName);
            this.SelectedFontIndex = insertIndex;
            this.UpdateFontPriorityLive();
        }
    }

    [RelayCommand]
    private void FontRemove()
    {
        if (this.SelectedFontIndex < 0)
        {
            return;
        }

        string? raw = GetRawFontEntry(this.FontItems[this.SelectedFontIndex]);
        if (raw is not null && FontPriorityList.IsSentinel(raw))
        {
            return;
        }

        this.FontItems.RemoveAt(this.SelectedFontIndex);
        this.UpdateFontPriorityLive();
    }

    [RelayCommand]
    private void FontMoveUp()
    {
        int index = this.SelectedFontIndex;
        if (index > 0)
        {
            var item = this.FontItems[index];
            this.FontItems.RemoveAt(index);
            this.FontItems.Insert(index - 1, item);
            this.SelectedFontIndex = index - 1;
            this.UpdateFontPriorityLive();
        }
    }

    [RelayCommand]
    private void FontMoveDown()
    {
        int index = this.SelectedFontIndex;
        if (index >= 0 && index < this.FontItems.Count - 1)
        {
            var item = this.FontItems[index];
            this.FontItems.RemoveAt(index);
            this.FontItems.Insert(index + 1, item);
            this.SelectedFontIndex = index + 1;
            this.UpdateFontPriorityLive();
        }
    }

    private object CreateFontDisplayItem(string entry)
    {
        if (FontPriorityList.IsGuiFontSentinel(entry))
        {
            string resolved = string.Join(", ", this.currentGuiFontNames);
            string label = string.IsNullOrEmpty(resolved)
                ? "[Neovim guifont]"
                : $"[Neovim guifont]  ({resolved})";
            return new FontPriorityItem(FontPriorityList.GuiFontSentinel, label);
        }

        if (FontPriorityList.IsSystemMonoSentinel(entry))
        {
            string resolved = string.Join(", ", Helpers.GetDefaultFallbackFontNames());
            return new FontPriorityItem(FontPriorityList.SystemMonoSentinel, $"[System Monospace]  ({resolved})");
        }

        return entry;
    }

    private List<string> GetRawFontList()
    {
        var fonts = new List<string>();
        foreach (var item in this.FontItems)
        {
            string? raw = GetRawFontEntry(item);
            if (raw is not null)
            {
                fonts.Add(raw);
            }
        }

        return fonts;
    }

    private void UpdateFontPriorityLive()
    {
        this.settings.FallbackFonts = this.GetRawFontList();
    }
}
