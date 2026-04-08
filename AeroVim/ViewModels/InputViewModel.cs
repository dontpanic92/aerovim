// <copyright file="InputViewModel.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

/// <summary>
/// View model for the input dialog.
/// </summary>
public sealed partial class InputViewModel : ViewModelBase
{
    [ObservableProperty]
    private string inputText = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="InputViewModel"/> class.
    /// </summary>
    /// <param name="prompt">Prompt text displayed above the input field.</param>
    /// <param name="title">The dialog title.</param>
    /// <param name="watermark">Optional watermark for the input field.</param>
    public InputViewModel(string prompt, string title, string? watermark = null)
    {
        this.Prompt = prompt;
        this.Title = title;
        this.Watermark = watermark ?? string.Empty;
    }

    /// <summary>
    /// Gets the prompt text.
    /// </summary>
    public string Prompt { get; }

    /// <summary>
    /// Gets the dialog title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the watermark text for the input field.
    /// </summary>
    public string Watermark { get; }
}
