// <copyright file="MessageViewModel.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.ViewModels;

/// <summary>
/// View model for the message dialog.
/// </summary>
/// <param name="message">The message to display.</param>
/// <param name="title">The dialog title.</param>
public sealed class MessageViewModel(string message, string title) : ViewModelBase
{
    /// <summary>
    /// Gets the message to display.
    /// </summary>
    public string Message { get; } = message;

    /// <summary>
    /// Gets the dialog title.
    /// </summary>
    public string Title { get; } = title;
}
