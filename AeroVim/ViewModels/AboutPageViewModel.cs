// <copyright file="AboutPageViewModel.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.ViewModels;

using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// View model for the About settings page.
/// </summary>
internal sealed partial class AboutPageViewModel : SettingsPageViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AboutPageViewModel"/> class.
    /// </summary>
    public AboutPageViewModel()
        : base("About")
    {
        var informationalVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var versionText = informationalVersion ?? "Version unknown";
        if (versionText.Split('+') is [var version, var build])
        {
            versionText = $"{version.Trim()} build {build[..7]}";
        }

        this.VersionText = versionText;
    }

    /// <summary>
    /// Gets the version text.
    /// </summary>
    public string VersionText { get; }

    [RelayCommand]
    private static void OpenGitHub()
    {
        Process.Start(new ProcessStartInfo("https://github.com/dontpanic92/aerovim") { UseShellExecute = true });
    }

    [RelayCommand]
    private static void OpenIssues()
    {
        Process.Start(new ProcessStartInfo("https://github.com/dontpanic92/aerovim/issues") { UseShellExecute = true });
    }
}
