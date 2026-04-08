// <copyright file="AboutPage.axaml.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Dialogs;

using AeroVim.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;

/// <summary>
/// About settings page.
/// </summary>
public partial class AboutPage : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AboutPage"/> class.
    /// </summary>
    public AboutPage()
    {
        this.InitializeComponent();
    }

    private void GitHubLink_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (this.DataContext is AboutPageViewModel vm)
        {
            vm.OpenGitHubCommand.Execute(null);
        }
    }

    private void IssuesLink_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (this.DataContext is AboutPageViewModel vm)
        {
            vm.OpenIssuesCommand.Execute(null);
        }
    }
}
