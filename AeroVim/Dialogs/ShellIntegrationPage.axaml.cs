// <copyright file="ShellIntegrationPage.axaml.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Dialogs;

using AeroVim.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

/// <summary>
/// Shell Integration settings page.
/// </summary>
public partial class ShellIntegrationPage : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShellIntegrationPage"/> class.
    /// </summary>
    public ShellIntegrationPage()
    {
        this.InitializeComponent();
    }

    private void FileAssocRemove_Click(object? sender, RoutedEventArgs e)
    {
        var listBox = this.FindControl<ListBox>("FileAssocListBox");
        if (listBox is null)
        {
            return;
        }

        var selectedItems = listBox.Selection.SelectedItems.Cast<object>().ToList();
        if (this.DataContext is ShellIntegrationPageViewModel vm)
        {
            vm.RemoveExtensionCommand.Execute(selectedItems);
        }
    }
}
