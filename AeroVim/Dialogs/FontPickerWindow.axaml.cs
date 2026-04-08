// <copyright file="FontPickerWindow.axaml.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Dialogs;

using AeroVim.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

/// <summary>
/// A dialog that lists all available system fonts and lets the user pick one.
/// </summary>
public partial class FontPickerWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FontPickerWindow"/> class.
    /// </summary>
    public FontPickerWindow()
    {
        this.DataContext = new FontPickerViewModel();
        this.InitializeComponent();
    }

    /// <summary>
    /// Gets the font name selected by the user, or <c>null</c> if cancelled.
    /// </summary>
    public string? SelectedFontName { get; private set; }

    private FontPickerViewModel? ViewModel => this.DataContext as FontPickerViewModel;

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        this.SelectedFontName = this.ViewModel?.SelectedFontName;
        this.Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        this.SelectedFontName = null;
        this.Close();
    }
}
