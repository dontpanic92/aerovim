// <copyright file="SettingsPageViewModel.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.ViewModels;

/// <summary>
/// Base class for settings page view models. Provides a display name for
/// the page list.
/// </summary>
/// <param name="displayName">The display name shown in the page list.</param>
internal abstract class SettingsPageViewModel(string displayName) : ViewModelBase
{
    /// <summary>
    /// Gets the display name of this page.
    /// </summary>
    public string DisplayName { get; } = displayName;

    /// <inheritdoc/>
    public override string ToString() => this.DisplayName;
}
