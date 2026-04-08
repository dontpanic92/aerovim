// <copyright file="IDialogService.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Services;

/// <summary>
/// Abstraction for opening dialogs from view models without depending on the view layer.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a message dialog.
    /// </summary>
    /// <param name="message">The message text.</param>
    /// <param name="title">The dialog title.</param>
    /// <returns>A task that completes when the dialog is closed.</returns>
    Task ShowMessageAsync(string message, string title);

    /// <summary>
    /// Shows a confirmation dialog with Yes/No buttons.
    /// </summary>
    /// <param name="message">The message text.</param>
    /// <param name="title">The dialog title.</param>
    /// <returns><c>true</c> if the user chose Yes; otherwise <c>false</c>.</returns>
    Task<bool> ShowConfirmAsync(string message, string title);

    /// <summary>
    /// Shows an input dialog.
    /// </summary>
    /// <param name="prompt">Prompt text displayed above the input field.</param>
    /// <param name="title">The dialog title.</param>
    /// <param name="watermark">Optional watermark for the input field.</param>
    /// <returns>The text entered by the user, or <c>null</c> if cancelled.</returns>
    Task<string?> ShowInputAsync(string prompt, string title, string? watermark = null);

    /// <summary>
    /// Shows a font picker dialog.
    /// </summary>
    /// <returns>The selected font name, or <c>null</c> if cancelled.</returns>
    Task<string?> ShowFontPickerAsync();

    /// <summary>
    /// Shows a file picker dialog.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <returns>The selected file path, or <c>null</c> if cancelled.</returns>
    Task<string?> ShowFilePickerAsync(string title);
}
