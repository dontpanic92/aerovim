// <copyright file="UpdatesPage.axaml.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Dialogs;

using AeroVim.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;

/// <summary>
/// Updates settings page.
/// </summary>
public partial class UpdatesPage : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatesPage"/> class.
    /// </summary>
    public UpdatesPage()
    {
        this.InitializeComponent();
    }

    private void ReleaseNotesLink_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (this.DataContext is UpdatesPageViewModel vm)
        {
            vm.ViewReleaseNotesCommand.Execute(null);
        }
    }
}
