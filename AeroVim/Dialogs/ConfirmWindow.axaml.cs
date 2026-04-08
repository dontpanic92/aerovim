// <copyright file="ConfirmWindow.axaml.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Dialogs;

using AeroVim.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

/// <summary>
/// A confirmation dialog with Yes and No buttons.
/// </summary>
public partial class ConfirmWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmWindow"/> class.
    /// </summary>
    public ConfirmWindow()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmWindow"/> class.
    /// </summary>
    /// <param name="message">Dialog message.</param>
    /// <param name="title">Dialog title.</param>
    public ConfirmWindow(string message, string title)
    {
        this.DataContext = new ConfirmViewModel(message, title);
        this.InitializeComponent();
    }

    /// <summary>
    /// Gets a value indicating whether the user confirmed the action.
    /// </summary>
    public bool Confirmed { get; private set; }

    private void Yes_Click(object? sender, RoutedEventArgs e)
    {
        this.Confirmed = true;
        this.Close();
    }

    private void No_Click(object? sender, RoutedEventArgs e)
    {
        this.Confirmed = false;
        this.Close();
    }
}
