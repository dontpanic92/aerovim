// <copyright file="MessageWindow.axaml.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

#pragma warning disable SA1009 // StyleCop 1.1.118 false positive with null-forgiving operator after closing parenthesis

namespace AeroVim.Dialogs;

using Avalonia.Controls;
using Avalonia.Interactivity;

/// <summary>
/// A simple message dialog.
/// </summary>
public partial class MessageWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageWindow"/> class.
    /// </summary>
    public MessageWindow()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageWindow"/> class.
    /// </summary>
    /// <param name="message">Dialog message.</param>
    /// <param name="title">Dialog title.</param>
    public MessageWindow(string message, string title)
    {
        this.InitializeComponent();
        this.Title = title;
        this.FindControl<TextBlock>("MessageTextBlock")!.Text = message;
    }

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        this.Close();
    }
}
