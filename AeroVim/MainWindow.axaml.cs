// <copyright file="MainWindow.axaml.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim;

using System.Runtime.InteropServices;
using AeroVim.Editor.Capabilities;
using AeroVim.Services;
using AeroVim.Utilities;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

/// <summary>
/// The main window. Acts as a thin composition root that wires
/// <see cref="WindowEffectsService"/> and <see cref="EditorSessionCoordinator"/>
/// together with the visual tree.
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppSettings settings;
    private readonly WindowEffectsService effectsService;
    private readonly EditorSessionCoordinator coordinator;
    private readonly IUpdateService updateService;
    private readonly Grid titleBar;
    private readonly TextBlock titleText;
    private readonly Border neovimBorder;
    private bool isSettingsDialogOpen;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// Used by the XAML designer; runtime code should use
    /// <see cref="MainWindow(AppSettings, IReadOnlyList{string}?)"/>.
    /// </summary>
    public MainWindow()
        : this(AppSettings.Default, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="settings">Application settings.</param>
    /// <param name="fileArgs">Optional file paths from command-line arguments.</param>
    public MainWindow(AppSettings settings, IReadOnlyList<string>? fileArgs = null)
    {
        this.settings = settings;
        this.InitializeComponent();

        this.titleBar = this.FindControl<Grid>("TitleBar")!;
        this.titleText = this.FindControl<TextBlock>("TitleText")!;
        this.neovimBorder = this.FindControl<Border>("NeovimBorder")!;

        this.effectsService = new WindowEffectsService(this, this.settings);
        this.effectsService.CurrentBackgroundColor = this.settings.BackgroundColor;
        this.updateService = new UpdateService(this.settings);
        this.coordinator = new EditorSessionCoordinator(this.settings, AeroVim.Diagnostics.AppLogger.Instance, prompt => this.ShowSettingsDialogAsync(prompt), fileArgs);

        this.effectsService.BackgroundBrushChanged += this.OnBackgroundBrushChanged;
        this.effectsService.MacOSFullScreenChanged += this.OnMacOSFullScreenChanged;
        this.coordinator.EditorReady += this.OnEditorReady;
        this.coordinator.TitleChanged += this.OnTitleChanged;
        this.coordinator.ForegroundColorChanged += this.OnForegroundColorChanged;
        this.coordinator.BackgroundColorChanged += this.OnBackgroundColorChanged;
        this.coordinator.EditorExitedAbnormally += this.OnEditorExitedAbnormally;
        this.coordinator.EditorExitedNormally += this.OnEditorExitedNormally;
        this.updateService.UpdateAvailableChanged += this.OnUpdateAvailableChanged;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            this.SetupMacOSTitleBar();
            this.Activated += (s, e) => this.effectsService.HandleMacOSActivation();
        }

        this.effectsService.SetupBlurBehind();
        WindowSettingsPersistence.Apply(this, this.settings);

        this.AddHandler(DragDrop.DragOverEvent, this.OnDragOver);
        this.AddHandler(DragDrop.DropEvent, this.OnDrop);

        this.Activated += this.OnWindowActivated;
        this.Deactivated += this.OnWindowDeactivated;
        this.Opened += this.OnWindowOpened;
    }

    /// <summary>
    /// Gets the update service for platform-specific notification wiring
    /// (e.g. macOS menu item).
    /// </summary>
    internal IUpdateService UpdateService => this.updateService;

    /// <summary>
    /// Opens the specified files in the editor. Used by drag-and-drop
    /// and platform file-open events (e.g. macOS Finder).
    /// </summary>
    /// <param name="paths">The file paths to open.</param>
    public void OpenFiles(IEnumerable<string> paths)
    {
        this.coordinator.OpenFiles(paths);
    }

    /// <summary>
    /// Opens the settings dialog and returns its result.
    /// </summary>
    /// <param name="promptText">Optional prompt text displayed in the dialog.</param>
    /// <param name="initialPage">Optional page type to navigate to on open.</param>
    /// <returns>The dialog result indicating whether the user accepted or cancelled.</returns>
    internal async Task<Dialogs.SettingsWindow.Result> ShowSettingsDialogAsync(string? promptText = null, Type? initialPage = null)
    {
        if (this.isSettingsDialogOpen)
        {
            return Dialogs.SettingsWindow.Result.Cancel;
        }

        this.isSettingsDialogOpen = true;
        IntPtr blurHandle = IntPtr.Zero;
        try
        {
            var previousEditorType = this.settings.EditorType;
            var previousNeovimPath = this.settings.NeovimPath;
            var previousVimPath = this.settings.VimPath;

            blurHandle = this.effectsService.BeginDialogBlurPreservation();

            var dialog = new Dialogs.SettingsWindow(
                this.settings,
                this.updateService,
                promptText,
                this.coordinator.EditorClient?.FontSettings.FontNames,
                initialPage);
            await dialog.ShowDialog(this);

            if (dialog.CloseReason == Dialogs.SettingsWindow.Result.Ok)
            {
                if (promptText is null &&
                    this.coordinator.HasEditorConfigChanged(previousEditorType, previousNeovimPath, previousVimPath))
                {
                    var msg = new Dialogs.MessageWindow(
                        "Editor backend changes will take effect the next time AeroVim is started.",
                        "Restart Required");
                    await msg.ShowDialog(this);
                }
            }

            return dialog.CloseReason;
        }
        finally
        {
            this.effectsService.EndDialogBlurPreservation(blurHandle);
            this.isSettingsDialogOpen = false;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty)
        {
            var newState = (WindowState)change.NewValue!;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Dispatcher.UIThread.Post(
                    () => this.effectsService.HandleMacOSFullScreenTransition(newState == WindowState.FullScreen),
                    DispatcherPriority.Background);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                this.UpdateMaximizedPadding(newState);
            }
        }
        else if (change.Property == OffScreenMarginProperty && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            this.UpdateMaximizedPadding(this.WindowState);
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // During IME composition Avalonia raises KeyDown with Key.ImeProcessed (or Key.None).
        // These must not be forwarded to the editor; let the IME handle them.
        if (e.Key is Key.ImeProcessed or Key.None)
        {
            return;
        }

        // Intercept system paste when bracketed paste mode is active.
        if (this.IsPasteShortcut(e) &&
            this.coordinator.EditorClient is ITerminalCapabilities tc &&
            tc.BracketedPasteEnabled)
        {
            _ = this.HandleBracketedPasteAsync();
            e.Handled = true;
            return;
        }

        if (this.coordinator.EditorClient is not null && KeyMapping.TryMap(e, out var text) && text is not null)
        {
            this.coordinator.Input(text);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (this.coordinator.EditorClient is null || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        // Iterate over Unicode codepoints (not UTF-16 code units) so that
        // surrogate pairs such as emoji are sent as a single character.
        var text = e.Text;
        int i = 0;
        while (i < text.Length)
        {
            int codepoint;
            string grapheme;
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                codepoint = char.ConvertToUtf32(text[i], text[i + 1]);
                grapheme = text.Substring(i, 2);
                i += 2;
            }
            else
            {
                codepoint = text[i];
                grapheme = text[i].ToString();
                i++;
            }

            // Allow newline, carriage return, and tab through (needed for
            // paste), but filter all other control characters (ESC, BEL, etc.).
            if (codepoint < 0x10000 && char.IsControl((char)codepoint))
            {
                if (codepoint == '\n' || codepoint == '\r')
                {
                    this.coordinator.Input("<CR>");
                    continue;
                }

                if (codepoint == '\t')
                {
                    this.coordinator.Input("<Tab>");
                    continue;
                }

                continue;
            }

            // Escape '<' so Neovim doesn't interpret it as a special key sequence
            if (codepoint == '<')
            {
                this.coordinator.Input("<lt>");
            }
            else
            {
                this.coordinator.Input(grapheme);
            }
        }

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        WindowSettingsPersistence.Capture(this, this.settings);
        this.settings.Save();
        this.coordinator.Shutdown();
    }

    private void UpdateMaximizedPadding(WindowState state)
    {
        this.Padding = state == WindowState.Maximized
            ? this.OffScreenMargin
            : default;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!this.settings.EnableDragDrop)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (!this.settings.EnableDragDrop || !e.DataTransfer.Contains(DataFormat.File))
        {
            return;
        }

        var files = e.DataTransfer.TryGetFiles();
        if (files is null)
        {
            return;
        }

        var paths = files
            .Select(f => f.Path.LocalPath)
            .Where(p => !string.IsNullOrEmpty(p));

        this.coordinator.OpenFiles(paths);
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        this.Opened -= this.OnWindowOpened;

        this.effectsService.DeferMacOSNativeTransparency();
        EditorPathDetector.PopulateUnsetPaths(this.settings);
        await this.ShowSettingsPersistenceErrorIfNeededAsync();
        this.StartUpdateCheckIfEnabled();

        if (!await this.coordinator.InitializeAsync())
        {
            this.Close();
        }
    }

    private async Task ShowSettingsPersistenceErrorIfNeededAsync()
    {
        if (string.IsNullOrEmpty(this.settings.LastPersistenceError))
        {
            return;
        }

        string message = $"Settings could not be fully loaded or saved:\n{this.settings.LastPersistenceError}";
        string? logPath = AeroVim.Diagnostics.AppLogger.LogFilePath;
        if (logPath is not null)
        {
            message += $"\n\nSee log file for details:\n{logPath}";
        }

        var dialog = new Dialogs.MessageWindow(message, "Settings Warning");
        await dialog.ShowDialog(this);
        this.settings.ClearLastPersistenceError();
    }

    private void OnEditorReady(Controls.EditorControl editorControl)
    {
        this.effectsService.EditorControl = editorControl;
        this.neovimBorder.Child = editorControl;
        this.effectsService.UpdateBackgroundOpacity();
    }

    private void OnTitleChanged(string title)
    {
        this.Title = title;
        this.titleText.Text = title;
    }

    private void OnForegroundColorChanged(int intColor)
    {
        var color = Helpers.GetAvaloniaColor(intColor);

        this.Resources["TitleBarForegroundBrush"] = new SolidColorBrush(color);
        this.Resources["TitleBarButtonHoverBrush"] = new SolidColorBrush(
            Color.FromArgb(0x20, color.R, color.G, color.B));
        this.Resources["TitleBarButtonPressedBrush"] = new SolidColorBrush(
            Color.FromArgb(0x40, color.R, color.G, color.B));
    }

    private void OnBackgroundColorChanged(int intColor)
    {
        this.effectsService.CurrentBackgroundColor = intColor;
        this.settings.BackgroundColor = intColor;
        this.settings.Save();
        this.effectsService.UpdateBackgroundOpacity();
    }

    private void OnBackgroundBrushChanged(IBrush brush)
    {
        this.titleBar.Background = brush;
        this.neovimBorder.Background = brush;
    }

    private void OnMacOSFullScreenChanged(bool isFullScreen)
    {
        this.titleBar.IsVisible = !isFullScreen;
    }

    private async void OnEditorExitedAbnormally(string message)
    {
        await this.ShowSettingsDialogAsync(message);
        this.Close();
    }

    private void OnEditorExitedNormally()
    {
        this.Close();
    }

    private void SetupMacOSTitleBar()
    {
        this.FindControl<Button>("MinimizeButton")!.IsVisible = false;
        this.FindControl<Button>("MaximizeButton")!.IsVisible = false;
        this.FindControl<Button>("CloseButton")!.IsVisible = false;
        this.FindControl<Button>("SettingsButton")!.IsVisible = false;
        this.FindControl<TextBlock>("LogoText")!.IsVisible = false;

        this.titleText.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        this.titleText.FontWeight = FontWeight.Bold;
    }

    private async void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        var initialPage = this.updateService.AvailableUpdate is not null
            ? typeof(ViewModels.UpdatesPageViewModel)
            : (Type?)null;
        await this.ShowSettingsDialogAsync(initialPage: initialPage);
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Button)
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            if (e.ClickCount == 2)
            {
                this.WindowState = this.WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
            else
            {
                this.BeginMoveDrag(e);
            }
        }
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object? sender, RoutedEventArgs e)
    {
        this.WindowState = this.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private bool IsPasteShortcut(KeyEventArgs e)
    {
        if (e.Key != Key.V)
        {
            return false;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        }

        return e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
               e.KeyModifiers.HasFlag(KeyModifiers.Shift);
    }

    private async Task HandleBracketedPasteAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        var data = await clipboard.TryGetDataAsync();
        string? text = data is not null ? await data.TryGetTextAsync() : null;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        this.coordinator.Input("\x1B[200~");
        this.coordinator.Input(text);
        this.coordinator.Input("\x1B[201~");
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        if (this.coordinator.EditorClient is ITerminalCapabilities tc && tc.FocusEventsEnabled)
        {
            this.coordinator.Input("\x1B[I");
        }
    }

    private void OnUpdateAvailableChanged(object? sender, UpdateInfo? info)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var badge = this.FindControl<Avalonia.Controls.Shapes.Ellipse>("UpdateBadge");
            if (badge is not null)
            {
                badge.IsVisible = info is not null;
            }
        });
    }

    private void StartUpdateCheckIfEnabled()
    {
        if (this.settings.AutoCheckForUpdates)
        {
            _ = this.updateService.CheckForUpdateAsync();
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (this.coordinator.EditorClient is ITerminalCapabilities tc && tc.FocusEventsEnabled)
        {
            this.coordinator.Input("\x1B[O");
        }
    }
}