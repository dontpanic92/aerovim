// <copyright file="UpdatesPageViewModel.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.ViewModels;

using System.Reflection;
using AeroVim.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// View model for the Updates settings page. Displays the current version,
/// channel selection, update check status, and available update actions.
/// </summary>
internal sealed partial class UpdatesPageViewModel : SettingsPageViewModel
{
    private readonly AppSettings settings;
    private readonly IUpdateService updateService;

    [ObservableProperty]
    private int selectedChannelIndex;

    [ObservableProperty]
    private bool autoCheckForUpdates;

    [ObservableProperty]
    private string statusText = "Not checked yet";

    [ObservableProperty]
    private bool isUpdateAvailable;

    [ObservableProperty]
    private bool isChecking;

    [ObservableProperty]
    private string? releaseNotesUrl;

    [ObservableProperty]
    private bool isDownloading;

    [ObservableProperty]
    private int downloadProgress;

    [ObservableProperty]
    private bool isReadyToRestart;

    [ObservableProperty]
    private string downloadStatusText = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatesPageViewModel"/> class.
    /// </summary>
    /// <param name="settings">Application settings.</param>
    /// <param name="updateService">The update service.</param>
    public UpdatesPageViewModel(AppSettings settings, IUpdateService updateService)
        : base("Updates")
    {
        this.settings = settings;
        this.updateService = updateService;
        this.IsInstalled = updateService.IsInstalled;

        this.selectedChannelIndex = (int)updateService.InstalledChannel;
        this.autoCheckForUpdates = settings.AutoCheckForUpdates;

        this.updateService.UpdateAvailableChanged += this.OnUpdateAvailableChanged;
        this.RefreshStatus();
    }

    /// <summary>
    /// Gets the current version text.
    /// </summary>
    public string VersionText { get; } = GetVersionText();

    /// <summary>
    /// Gets a value indicating whether the app is a Velopack-installed build
    /// that can receive updates.
    /// </summary>
    public bool IsInstalled { get; }

    /// <summary>
    /// Gets the last checked text.
    /// </summary>
    public string LastCheckedText => FormatLastChecked(this.settings.LastUpdateCheckUtc);

    /// <summary>
    /// Saves update-related settings when the dialog is accepted.
    /// </summary>
    public void SaveToSettings()
    {
        this.settings.AutoCheckForUpdates = this.AutoCheckForUpdates;
    }

    private static string GetVersionText()
    {
        var informationalVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var versionText = informationalVersion ?? "Unknown";
        if (versionText.Split('+') is [var version, var build])
        {
            versionText = $"{version.Trim()} build {build[..Math.Min(7, build.Length)]}";
        }

        return versionText;
    }

    private static string FormatLastChecked(DateTime? utc)
    {
        if (utc is null)
        {
            return "Never";
        }

        var elapsed = DateTime.UtcNow - utc.Value;
        if (elapsed.TotalMinutes < 1)
        {
            return "Just now";
        }

        if (elapsed.TotalHours < 1)
        {
            return $"{(int)elapsed.TotalMinutes} minute(s) ago";
        }

        if (elapsed.TotalDays < 1)
        {
            return $"{(int)elapsed.TotalHours} hour(s) ago";
        }

        return utc.Value.ToLocalTime().ToString("g");
    }

    /// <summary>
    /// Performs a manual update check on the installed channel.
    /// </summary>
    /// <returns>A task that completes when the check finishes.</returns>
    [RelayCommand]
    private async Task CheckForUpdateAsync()
    {
        this.IsChecking = true;
        this.StatusText = "Checking…";

        await this.updateService.CheckForUpdateAsync().ConfigureAwait(true);

        this.IsChecking = false;
        this.RefreshStatus();
        this.OnPropertyChanged(nameof(this.LastCheckedText));
    }

    /// <summary>
    /// Called by the source generator when <see cref="SelectedChannelIndex"/>
    /// changes. If the user picks a different channel, immediately start a
    /// check against that channel so the download-and-restart flow can begin.
    /// </summary>
    /// <param name="value">The new index.</param>
    partial void OnSelectedChannelIndexChanged(int value)
    {
        var target = (UpdateChannel)value;
        if (target != this.updateService.InstalledChannel)
        {
            _ = this.SwitchChannelAsync(target);
        }
    }

    private async Task SwitchChannelAsync(UpdateChannel target)
    {
        this.IsChecking = true;
        this.StatusText = $"Checking {target} channel…";

        await this.updateService.CheckForUpdateAsync(target).ConfigureAwait(true);

        this.IsChecking = false;
        this.RefreshStatus();
        this.OnPropertyChanged(nameof(this.LastCheckedText));
    }

    /// <summary>
    /// Downloads the update and prepares it for installation.
    /// </summary>
    [RelayCommand]
    private async Task DownloadAndInstallAsync()
    {
        this.IsDownloading = true;
        this.DownloadProgress = 0;
        this.DownloadStatusText = "Downloading…";

        var progress = new Progress<int>(p =>
        {
            this.DownloadProgress = p;
            this.DownloadStatusText = $"Downloading… {p}%";
        });

        await this.updateService.DownloadUpdateAsync(progress).ConfigureAwait(true);

        this.IsDownloading = false;

        if (this.updateService.IsReadyToApply)
        {
            this.IsReadyToRestart = true;
            this.DownloadStatusText = "Ready to restart";
        }
        else if (this.updateService.LastError is not null)
        {
            this.DownloadStatusText = $"Download failed: {this.updateService.LastError}";
        }
    }

    /// <summary>
    /// Applies the downloaded update and restarts the application.
    /// </summary>
    [RelayCommand]
    private void RestartToUpdate()
    {
        this.updateService.ApplyUpdateAndRestart();
    }

    /// <summary>
    /// Opens the release notes URL in the default browser.
    /// </summary>
    [RelayCommand]
    private void ViewReleaseNotes()
    {
        if (!string.IsNullOrEmpty(this.ReleaseNotesUrl))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(this.ReleaseNotesUrl) { UseShellExecute = true });
        }
    }

    /// <summary>
    /// Skips the currently available update version.
    /// </summary>
    [RelayCommand]
    private void SkipThisVersion()
    {
        this.updateService.DismissUpdate(skipVersion: true);
    }

    private void OnUpdateAvailableChanged(object? sender, UpdateInfo? info)
    {
        this.RefreshStatus();
    }

    private void RefreshStatus()
    {
        var update = this.updateService.AvailableUpdate;
        if (update is not null)
        {
            this.IsUpdateAvailable = true;
            this.StatusText = $"Update available: {update.Version}";
            this.ReleaseNotesUrl = update.ReleaseNotesUrl;
        }
        else if (this.updateService.LastError is not null)
        {
            this.IsUpdateAvailable = false;
            this.StatusText = $"Check failed: {this.updateService.LastError}";
            this.ReleaseNotesUrl = null;
        }
        else
        {
            this.IsUpdateAvailable = false;
            this.StatusText = "You're up to date";
            this.ReleaseNotesUrl = null;
        }
    }
}
