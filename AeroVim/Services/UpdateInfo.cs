// <copyright file="UpdateInfo.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Services;

/// <summary>
/// Describes an available update discovered by the update service.
/// </summary>
/// <param name="Version">The version string of the available update (e.g. "0.9.0" or "0.9.0-nightly.20260409").</param>
/// <param name="Channel">The channel from which this update was discovered.</param>
/// <param name="DownloadUrl">Direct download URL for the current platform's package.</param>
/// <param name="ReleaseNotesUrl">URL to the release notes or commit log.</param>
/// <param name="PublishedAt">When this version was published, if known.</param>
public sealed record UpdateInfo(
    string Version,
    UpdateChannel Channel,
    string DownloadUrl,
    string? ReleaseNotesUrl,
    DateTimeOffset? PublishedAt);
