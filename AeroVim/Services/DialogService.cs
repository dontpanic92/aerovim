// <copyright file="DialogService.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Services;

using System.Runtime.InteropServices;
using AeroVim.Dialogs;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

/// <summary>
/// Concrete dialog service that opens Avalonia windows as modal dialogs.
/// </summary>
internal sealed class DialogService : IDialogService
{
    private readonly Window owner;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialogService"/> class.
    /// </summary>
    /// <param name="owner">The parent window used for dialog ownership.</param>
    public DialogService(Window owner)
    {
        this.owner = owner;
    }

    /// <inheritdoc/>
    public async Task ShowMessageAsync(string message, string title)
    {
        var dialog = new MessageWindow(message, title);
        await dialog.ShowDialog(this.owner);
    }

    /// <inheritdoc/>
    public async Task<bool> ShowConfirmAsync(string message, string title)
    {
        var dialog = new ConfirmWindow(message, title);
        await dialog.ShowDialog(this.owner);
        return dialog.Confirmed;
    }

    /// <inheritdoc/>
    public async Task<string?> ShowInputAsync(string prompt, string title, string? watermark = null)
    {
        var dialog = new InputWindow(prompt, title, watermark);
        await dialog.ShowDialog(this.owner);
        return dialog.InputText;
    }

    /// <inheritdoc/>
    public async Task<string?> ShowFontPickerAsync()
    {
        var dialog = new FontPickerWindow();
        await dialog.ShowDialog(this.owner);
        return dialog.SelectedFontName;
    }

    /// <inheritdoc/>
    public async Task<string?> ShowFilePickerAsync(string title)
    {
        var topLevel = TopLevel.GetTopLevel(this.owner);
        var fileTypeFilters = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[]
            {
                new FilePickerFileType("Executable Files") { Patterns = ["*.exe"] },
                new FilePickerFileType("All Files") { Patterns = ["*.*"] },
            }
            : new[]
            {
                new FilePickerFileType("All Files") { Patterns = ["*"] },
            };

        var files = await topLevel!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypeFilters,
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
}
