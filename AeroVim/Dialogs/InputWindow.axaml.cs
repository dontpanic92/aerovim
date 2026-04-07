// <copyright file="InputWindow.axaml.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Dialogs;

using Avalonia.Controls;
using Avalonia.Interactivity;

/// <summary>
/// A simple input dialog with a text box, OK, and Cancel buttons.
/// </summary>
public partial class InputWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InputWindow"/> class.
    /// </summary>
    public InputWindow()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InputWindow"/> class.
    /// </summary>
    /// <param name="prompt">Prompt text displayed above the input field.</param>
    /// <param name="title">Dialog title.</param>
    /// <param name="watermark">Optional watermark text for the input field.</param>
    public InputWindow(string prompt, string title, string? watermark = null)
    {
        this.InitializeComponent();
        this.Title = title;
        this.FindControl<TextBlock>("PromptTextBlock")!.Text = prompt;

        var inputBox = this.FindControl<TextBox>("InputTextBox")!;
        if (watermark is not null)
        {
            inputBox.Watermark = watermark;
        }

        inputBox.AttachedToVisualTree += (s, e) => inputBox.Focus();
    }

    /// <summary>
    /// Gets the text entered by the user, or <c>null</c> if cancelled.
    /// </summary>
    public string? InputText { get; private set; }

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        this.InputText = this.FindControl<TextBox>("InputTextBox")!.Text;
        this.Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        this.InputText = null;
        this.Close();
    }
}
