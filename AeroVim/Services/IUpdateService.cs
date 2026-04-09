// <copyright file="IUpdateService.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Services;

/// <summary>
/// Checks for and manages application updates.
/// </summary>
internal interface IUpdateService
{
    /// <summary>
    /// Raised when update availability changes (a new update is found, or the
    /// current notification is dismissed).
    /// </summary>
    event EventHandler<UpdateInfo?>? UpdateAvailableChanged;

    /// <summary>
    /// Gets the currently available update, or <c>null</c> if the app is up to date
    /// or no check has been performed.
    /// </summary>
    UpdateInfo? AvailableUpdate { get; }

    /// <summary>
    /// Gets a value indicating whether the application was installed via Velopack
    /// and is able to receive updates. Returns <c>false</c> for local dev builds.
    /// </summary>
    bool IsInstalled { get; }

    /// <summary>
    /// Gets a value indicating whether a check is currently in progress.
    /// </summary>
    bool IsChecking { get; }

    /// <summary>
    /// Gets a value indicating whether an update download is in progress.
    /// </summary>
    bool IsDownloading { get; }

    /// <summary>
    /// Gets the download progress percentage (0–100).
    /// </summary>
    int DownloadProgress { get; }

    /// <summary>
    /// Gets a value indicating whether a downloaded update is ready to be applied.
    /// </summary>
    bool IsReadyToApply { get; }

    /// <summary>
    /// Gets the last error message from a failed check or download, or <c>null</c>
    /// if the most recent operation succeeded or no operation has been performed.
    /// </summary>
    string? LastError { get; }

    /// <summary>
    /// Checks the configured update channel for a newer version.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the check finishes.</returns>
    Task CheckForUpdateAsync(CancellationToken ct = default);

    /// <summary>
    /// Downloads the available update with progress reporting.
    /// </summary>
    /// <param name="progress">Optional progress callback (0–100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the download finishes.</returns>
    Task DownloadUpdateAsync(IProgress<int>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Applies the downloaded update and restarts the application.
    /// </summary>
    void ApplyUpdateAndRestart();

    /// <summary>
    /// Dismisses the current update notification (e.g. the user chose to skip
    /// this version). The <see cref="AvailableUpdate"/> is cleared and
    /// <see cref="UpdateAvailableChanged"/> fires with <c>null</c>.
    /// </summary>
    /// <param name="skipVersion">
    /// When <c>true</c>, records the skipped version so it is not surfaced
    /// again until a newer version is available.
    /// </param>
    void DismissUpdate(bool skipVersion);
}
