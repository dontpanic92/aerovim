// <copyright file="FontPickerViewModel.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;

/// <summary>
/// View model for the font picker dialog.
/// </summary>
public sealed partial class FontPickerViewModel : ViewModelBase
{
    private readonly string[] allFontNames;

    [ObservableProperty]
    private string filterText = string.Empty;

    [ObservableProperty]
    private string? selectedFontName;

    /// <summary>
    /// Initializes a new instance of the <see cref="FontPickerViewModel"/> class.
    /// </summary>
    public FontPickerViewModel()
    {
        this.allFontNames = SKFontManager.Default.FontFamilies
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var name in this.allFontNames)
        {
            this.FilteredFonts.Add(name);
        }
    }

    /// <summary>
    /// Gets the filtered list of font names.
    /// </summary>
    public ObservableCollection<string> FilteredFonts { get; } = new();

    /// <inheritdoc/>
    partial void OnFilterTextChanged(string value)
    {
        this.FilteredFonts.Clear();
        foreach (var name in this.allFontNames)
        {
            if (name.Contains(value, StringComparison.OrdinalIgnoreCase))
            {
                this.FilteredFonts.Add(name);
            }
        }
    }
}
