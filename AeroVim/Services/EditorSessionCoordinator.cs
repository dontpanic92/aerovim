// <copyright file="EditorSessionCoordinator.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Services;

using AeroVim.Controls;
using AeroVim.Editor;
using AeroVim.Editor.Diagnostics;
using AeroVim.Utilities;
using Avalonia.Threading;

/// <summary>
/// Coordinates the editor backend lifecycle: client creation with retry,
/// <see cref="EditorControl"/> creation, event wiring, settings-change
/// routing, and shutdown.
/// </summary>
internal sealed class EditorSessionCoordinator
{
    private readonly AppSettings settings;
    private readonly IComponentLogger log;
    private readonly IAppLogger logger;
    private readonly Func<string?, Task<Dialogs.SettingsWindow.Result>> showSettingsPrompt;
    private IEditorClient? editorClient;
    private EditorControl? editorControl;

    /// <summary>
    /// Initializes a new instance of the <see cref="EditorSessionCoordinator"/> class.
    /// </summary>
    /// <param name="settings">Application settings.</param>
    /// <param name="logger">Application logger.</param>
    /// <param name="showSettingsPrompt">
    /// Callback invoked when the coordinator needs to prompt the user for an
    /// editor path. Receives an optional prompt message and returns the
    /// dialog result.
    /// </param>
    public EditorSessionCoordinator(
        AppSettings settings,
        IAppLogger logger,
        Func<string?, Task<Dialogs.SettingsWindow.Result>> showSettingsPrompt)
    {
        this.settings = settings;
        this.logger = logger;
        this.log = logger.For<EditorSessionCoordinator>();
        this.showSettingsPrompt = showSettingsPrompt;

        this.settings.PropertyChanged += this.OnSettingsPropertyChanged;
    }

    /// <summary>
    /// Raised when the <see cref="EditorControl"/> is created and ready to
    /// be placed in the visual tree.
    /// </summary>
    public event Action<EditorControl>? EditorReady;

    /// <summary>
    /// Raised when the editor client reports a title change.
    /// </summary>
    public event Action<string>? TitleChanged;

    /// <summary>
    /// Raised when the editor client reports a foreground color change.
    /// </summary>
    public event Action<int>? ForegroundColorChanged;

    /// <summary>
    /// Raised when the editor client reports a background color change.
    /// </summary>
    public event Action<int>? BackgroundColorChanged;

    /// <summary>
    /// Raised when the editor process exits with a non-zero exit code.
    /// The handler receives a user-friendly error message.
    /// </summary>
    public event Action<string>? EditorExitedAbnormally;

    /// <summary>
    /// Raised when the editor process exits cleanly (exit code 0).
    /// </summary>
    public event Action? EditorExitedNormally;

    /// <summary>
    /// Gets the active editor client, or <c>null</c> if not yet initialized.
    /// </summary>
    public IEditorClient? EditorClient => this.editorClient;

    /// <summary>
    /// Gets the active editor control, or <c>null</c> if not yet initialized.
    /// </summary>
    public EditorControl? EditorControl => this.editorControl;

    /// <summary>
    /// Attempts to create the editor client and control. If the configured
    /// editor executable is missing, prompts the user via the settings dialog
    /// callback until a valid path is provided or the user cancels.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the editor was initialized successfully; <c>false</c>
    /// if the user cancelled the settings dialog.
    /// </returns>
    public async Task<bool> InitializeAsync()
    {
        while (true)
        {
            try
            {
                this.log.Info($"Creating {this.settings.EditorType} backend...");
                this.editorClient = EditorClientFactory.Create(this.settings, this.logger);
                this.log.Info($"{this.settings.EditorType} backend created successfully.");
                break;
            }
            catch (Exception ex)
            {
                this.log.Error($"Failed to create {this.settings.EditorType} backend.", ex);
                string editorName = this.settings.EditorType == EditorType.Vim ? "Vim" : "Neovim";
                if (await this.showSettingsPrompt($"Please specify the path to {editorName}") ==
                    Dialogs.SettingsWindow.Result.Cancel)
                {
                    return false;
                }
            }
        }

        this.editorControl = new EditorControl(this.editorClient);
        this.editorControl.EnableLigature = this.settings.EnableLigature;
        if (this.settings.FallbackFonts.Count > 0)
        {
            this.editorControl.SetFallbackFonts(this.settings.FallbackFonts);
        }

        this.WireClientEvents();
        this.editorClient.Command("set mouse=a");
        this.EditorReady?.Invoke(this.editorControl);

        return true;
    }

    /// <summary>
    /// Forwards text input to the editor client.
    /// </summary>
    /// <param name="text">The text to send.</param>
    public void Input(string text)
    {
        this.editorClient?.Input(text);
    }

    /// <summary>
    /// Detects whether the editor type or path changed between
    /// <paramref name="previousType"/>/<paramref name="previousNeovimPath"/>/<paramref name="previousVimPath"/>
    /// and the current settings.
    /// </summary>
    /// <param name="previousType">The editor type before the dialog.</param>
    /// <param name="previousNeovimPath">The Neovim path before the dialog.</param>
    /// <param name="previousVimPath">The Vim path before the dialog.</param>
    /// <returns><c>true</c> if the effective editor configuration changed.</returns>
    public bool HasEditorConfigChanged(EditorType previousType, string previousNeovimPath, string previousVimPath)
    {
        if (this.settings.EditorType != previousType)
        {
            return true;
        }

        if (this.settings.EditorType == EditorType.Neovim
            && !string.Equals(this.settings.NeovimPath, previousNeovimPath, StringComparison.Ordinal))
        {
            return true;
        }

        if (this.settings.EditorType == EditorType.Vim
            && !string.Equals(this.settings.VimPath, previousVimPath, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Disposes the editor client and control, unsubscribing from events.
    /// </summary>
    public void Shutdown()
    {
        if (this.editorClient is not null)
        {
            this.editorClient.EditorExited -= this.OnEditorExited;
        }

        this.settings.PropertyChanged -= this.OnSettingsPropertyChanged;
        this.editorControl?.Dispose();
        this.editorClient?.Dispose();
    }

    private void WireClientEvents()
    {
        if (this.editorClient is null)
        {
            return;
        }

        this.editorClient.EditorExited += this.OnEditorExited;

        this.editorClient.TitleChanged += (string title) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var effectiveTitle = string.IsNullOrEmpty(title) ? "AeroVim" : title;
                this.TitleChanged?.Invoke(effectiveTitle);
            });
        };

        this.editorClient.ForegroundColorChanged += (int intColor) =>
        {
            Dispatcher.UIThread.Post(() => this.ForegroundColorChanged?.Invoke(intColor));
        };

        this.editorClient.BackgroundColorChanged += (int intColor) =>
        {
            Dispatcher.UIThread.Post(() => this.BackgroundColorChanged?.Invoke(intColor));
        };
    }

    private async void OnEditorExited(int exitCode)
    {
        if (exitCode != 0)
        {
            string editorName = this.settings.EditorType == EditorType.Vim ? "Vim" : "Neovim";
            string message = exitCode == -1
                ? $"{editorName} failed to start. Please check the executable path in Settings."
                : $"{editorName} exited unexpectedly (exit code {exitCode}). Please verify the executable path in Settings.";

            string? logPath = AeroVim.Diagnostics.AppLogger.LogFilePath;
            if (logPath is not null)
            {
                message += $"\n\nSee log file for details:\n{logPath}";
            }

            this.log.Error(message);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.EditorExitedAbnormally?.Invoke(message);
            });
        }
        else
        {
            Dispatcher.UIThread.Post(() => this.EditorExitedNormally?.Invoke());
        }
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppSettings.EnableLigature):
                if (this.editorControl is not null)
                {
                    this.editorControl.EnableLigature = this.settings.EnableLigature;
                    this.editorControl.InvalidateVisual();
                }

                break;

            case nameof(AppSettings.FallbackFonts):
                this.editorControl?.SetFallbackFonts(this.settings.FallbackFonts);
                break;
        }
    }
}
