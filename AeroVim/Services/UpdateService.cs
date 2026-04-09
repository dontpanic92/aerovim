// <copyright file="UpdateService.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Services;

using System.Runtime.InteropServices;
using AeroVim.Diagnostics;
using AeroVim.Editor.Diagnostics;
using Velopack;
using Velopack.Sources;

/// <summary>
/// Checks for, downloads, and applies application updates using Velopack.
/// </summary>
internal sealed class UpdateService : IUpdateService
{
    /// <summary>
    /// Base URL for the GitHub Pages nightly update feed.
    /// </summary>
    internal const string NightlyFeedUrl = "https://dontpanic92.github.io/dotnvim/updates";

    /// <summary>
    /// GitHub repository URL used by Velopack's <see cref="GithubSource"/>
    /// to fetch stable releases from GitHub Releases.
    /// </summary>
    internal const string GitHubRepoUrl = "https://github.com/dontpanic92/dotnvim";

    private static readonly IComponentLogger Log = AppLogger.For<UpdateService>();

    private readonly AppSettings settings;
    private readonly Func<UpdateChannel, UpdateManager>? managerFactory;

    private UpdateManager? currentManager;
    private UpdateChannel? currentManagerChannel;
    private Velopack.UpdateInfo? velopackUpdateInfo;
    private UpdateInfo? availableUpdate;
    private bool isChecking;
    private bool isDownloading;
    private int downloadProgress;
    private bool isReadyToApply;
    private string? lastError;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateService"/> class.
    /// </summary>
    /// <param name="settings">Application settings.</param>
    /// <param name="managerFactory">Optional factory for testing.</param>
    public UpdateService(AppSettings settings, Func<UpdateChannel, UpdateManager>? managerFactory = null)
    {
        this.settings = settings;
        this.managerFactory = managerFactory;
    }

    /// <inheritdoc/>
    public event EventHandler<UpdateInfo?>? UpdateAvailableChanged;

    /// <inheritdoc/>
    public UpdateInfo? AvailableUpdate => this.availableUpdate;

    /// <inheritdoc/>
    public bool IsInstalled
    {
        get
        {
            try
            {
                var mgr = this.GetOrCreateManager(this.settings.UpdateChannel);
                return mgr.IsInstalled;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <inheritdoc/>
    public bool IsChecking => this.isChecking;

    /// <inheritdoc/>
    public bool IsDownloading => this.isDownloading;

    /// <inheritdoc/>
    public int DownloadProgress => this.downloadProgress;

    /// <inheritdoc/>
    public bool IsReadyToApply => this.isReadyToApply;

    /// <inheritdoc/>
    public string? LastError => this.lastError;

    /// <inheritdoc/>
    public async Task CheckForUpdateAsync(CancellationToken ct = default)
    {
        if (this.isChecking)
        {
            return;
        }

        this.isChecking = true;
        this.lastError = null;

        try
        {
            var channel = this.settings.UpdateChannel;

            // When the user switches channels, clear stale state so the
            // previous channel's skipped version and pending download don't
            // interfere with the new channel's update check.
            if (this.currentManagerChannel is not null && this.currentManagerChannel != channel)
            {
                Log.Info($"Channel changed from {this.currentManagerChannel} to {channel} — clearing skipped version.");
                this.settings.SkippedVersion = null;
                this.velopackUpdateInfo = null;
                this.isReadyToApply = false;
                this.SetAvailableUpdate(null);
            }

            var mgr = this.GetOrCreateManager(channel);

            if (!mgr.IsInstalled)
            {
                Log.Info("Skipping update check — not a Velopack-installed build (local dev build).");
                return;
            }

            Log.Info($"Checking for updates on {channel} channel via Velopack.");

            var info = await mgr.CheckForUpdatesAsync().ConfigureAwait(false);

            this.settings.LastUpdateCheckUtc = DateTime.UtcNow;

            if (info is null)
            {
                Log.Info("Already up to date.");
                this.velopackUpdateInfo = null;
                this.SetAvailableUpdate(null);
                return;
            }

            var version = info.TargetFullRelease.Version.ToString();

            // If the user previously skipped this exact version, don't notify again.
            if (!string.IsNullOrEmpty(this.settings.SkippedVersion) &&
                string.Equals(this.settings.SkippedVersion, version, StringComparison.OrdinalIgnoreCase))
            {
                Log.Info($"Skipping previously dismissed version {version}.");
                return;
            }

            this.velopackUpdateInfo = info;

            var update = new UpdateInfo(
                version,
                channel,
                string.Empty,
                GetReleaseNotesUrl(channel),
                null);

            Log.Info($"Update available: {version}.");
            this.SetAvailableUpdate(update);
        }
        catch (OperationCanceledException)
        {
            // Caller cancelled.
        }
        catch (Exception ex)
        {
            this.lastError = ex.Message;
            Log.Warning($"Update check failed: {ex.Message}", ex);
        }
        finally
        {
            this.isChecking = false;
        }
    }

    /// <inheritdoc/>
    public async Task DownloadUpdateAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        if (this.isDownloading || this.velopackUpdateInfo is null)
        {
            return;
        }

        this.isDownloading = true;
        this.downloadProgress = 0;
        this.lastError = null;

        try
        {
            var mgr = this.GetOrCreateManager(this.settings.UpdateChannel);

            if (!mgr.IsInstalled)
            {
                Log.Warning("Cannot download update — not a Velopack-installed build.");
                this.lastError = "Updates are not available in local dev builds.";
                return;
            }

            await mgr.DownloadUpdatesAsync(
                this.velopackUpdateInfo,
                p =>
                {
                    this.downloadProgress = p;
                    progress?.Report(p);
                }).ConfigureAwait(false);

            this.isReadyToApply = true;
            Log.Info("Update downloaded and ready to apply.");
        }
        catch (OperationCanceledException)
        {
            // Caller cancelled.
        }
        catch (Exception ex)
        {
            this.lastError = ex.Message;
            Log.Warning($"Update download failed: {ex.Message}", ex);
        }
        finally
        {
            this.isDownloading = false;
        }
    }

    /// <inheritdoc/>
    public void ApplyUpdateAndRestart()
    {
        if (this.velopackUpdateInfo is null)
        {
            return;
        }

        try
        {
            var mgr = this.GetOrCreateManager(this.settings.UpdateChannel);

            if (!mgr.IsInstalled)
            {
                Log.Warning("Cannot apply update — not a Velopack-installed build.");
                return;
            }

            Log.Info("Applying update and restarting…");
            mgr.ApplyUpdatesAndRestart(this.velopackUpdateInfo);
        }
        catch (Exception ex)
        {
            this.lastError = ex.Message;
            Log.Error($"Failed to apply update: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public void DismissUpdate(bool skipVersion)
    {
        if (skipVersion && this.availableUpdate is not null)
        {
            this.settings.SkippedVersion = this.availableUpdate.Version;
        }

        this.velopackUpdateInfo = null;
        this.isReadyToApply = false;
        this.SetAvailableUpdate(null);
    }

    private static string GetReleaseNotesUrl(UpdateChannel channel) => channel switch
    {
        UpdateChannel.Nightly => "https://github.com/dontpanic92/aerovim/commits/master",
        _ => "https://github.com/dontpanic92/aerovim/releases/latest",
    };

    private static string GetChannelName(UpdateChannel channel)
    {
        var rid = GetCurrentRuntimeIdentifier() ?? "unknown";
        var suffix = channel == UpdateChannel.Nightly ? "nightly" : "stable";
        return $"{rid}-{suffix}";
    }

    private static string? GetCurrentRuntimeIdentifier()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "win-x64";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? "osx-arm64"
                : "osx-x64";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "linux-x64";
        }

        return null;
    }

    private UpdateManager GetOrCreateManager(UpdateChannel channel)
    {
        if (this.managerFactory is not null)
        {
            return this.managerFactory(channel);
        }

        if (this.currentManager is not null && this.currentManagerChannel == channel)
        {
            return this.currentManager;
        }

        var channelName = GetChannelName(channel);
        var options = new UpdateOptions
        {
            ExplicitChannel = channelName,
            AllowVersionDowngrade = true,
        };

        if (channel == UpdateChannel.Stable)
        {
            var source = new GithubSource(GitHubRepoUrl, accessToken: null, prerelease: false);
            this.currentManager = new UpdateManager(source, options);
        }
        else
        {
            this.currentManager = new UpdateManager(NightlyFeedUrl, options);
        }

        this.currentManagerChannel = channel;
        return this.currentManager;
    }

    private void SetAvailableUpdate(UpdateInfo? update)
    {
        this.availableUpdate = update;
        this.UpdateAvailableChanged?.Invoke(this, update);
    }
}
