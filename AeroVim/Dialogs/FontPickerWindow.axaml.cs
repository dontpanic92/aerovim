// <copyright file="FontPickerWindow.axaml.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Dialogs;

using Avalonia.Controls;
using Avalonia.Interactivity;
using SkiaSharp;

/// <summary>
/// A dialog that lists all available system fonts and lets the user pick one.
/// </summary>
public partial class FontPickerWindow : Window
{
    private readonly string[] allFontNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="FontPickerWindow"/> class.
    /// </summary>
    public FontPickerWindow()
    {
        this.InitializeComponent();

        this.allFontNames = SKFontManager.Default.FontFamilies
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var fontListBox = this.FindControl<ListBox>("FontListBox")!;
        foreach (var name in this.allFontNames)
        {
            fontListBox.Items.Add(name);
        }

        var filterBox = this.FindControl<TextBox>("FilterTextBox")!;
        filterBox.TextChanged += this.OnFilterTextChanged;
    }

    /// <summary>
    /// Gets the font name selected by the user, or <c>null</c> if cancelled.
    /// </summary>
    public string? SelectedFontName { get; private set; }

    private void OnFilterTextChanged(object? sender, TextChangedEventArgs e)
    {
        var filterBox = this.FindControl<TextBox>("FilterTextBox")!;
        var fontListBox = this.FindControl<ListBox>("FontListBox")!;
        var filter = filterBox.Text ?? string.Empty;

        fontListBox.Items.Clear();
        foreach (var name in this.allFontNames)
        {
            if (name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                fontListBox.Items.Add(name);
            }
        }
    }

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        var fontListBox = this.FindControl<ListBox>("FontListBox")!;
        this.SelectedFontName = fontListBox.SelectedItem as string;
        this.Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        this.SelectedFontName = null;
        this.Close();
    }
}
