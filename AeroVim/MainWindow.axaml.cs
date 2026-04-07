// <copyright file="MainWindow.axaml.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim;

using System.Runtime.InteropServices;
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
    private bool isSettingsDialogOpen;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// Used by the XAML designer; runtime code should use
    /// <see cref="MainWindow(AppSettings)"/>.
    /// </summary>
    public MainWindow()
        : this(AppSettings.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="settings">Application settings.</param>
    public MainWindow(AppSettings settings)
    {
        this.settings = settings;
        this.InitializeComponent();

        this.effectsService = new WindowEffectsService(this, this.settings);
        this.effectsService.CurrentBackgroundColor = this.settings.BackgroundColor;
        this.coordinator = new EditorSessionCoordinator(this.settings, AeroVim.Diagnostics.AppLogger.Instance, this.ShowSettingsDialogAsync);

        this.effectsService.BackgroundBrushChanged += this.OnBackgroundBrushChanged;
        this.effectsService.MacOSFullScreenChanged += this.OnMacOSFullScreenChanged;
        this.coordinator.EditorReady += this.OnEditorReady;
        this.coordinator.TitleChanged += this.OnTitleChanged;
        this.coordinator.ForegroundColorChanged += this.OnForegroundColorChanged;
        this.coordinator.BackgroundColorChanged += this.OnBackgroundColorChanged;
        this.coordinator.EditorExitedAbnormally += this.OnEditorExitedAbnormally;
        this.coordinator.EditorExitedNormally += this.OnEditorExitedNormally;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            this.SetupMacOSTitleBar();
            this.Activated += (s, e) => this.effectsService.HandleMacOSActivation();
        }

        this.effectsService.SetupBlurBehind();
        WindowSettingsPersistence.Apply(this, this.settings);
        this.Opened += this.OnWindowOpened;
    }

    /// <summary>
    /// Opens the settings dialog and returns its result.
    /// </summary>
    /// <param name="promptText">Optional prompt text displayed in the dialog.</param>
    /// <returns>The dialog result indicating whether the user accepted or cancelled.</returns>
    internal async Task<Dialogs.SettingsWindow.Result> ShowSettingsDialogAsync(string? promptText = null)
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

            var dialog = new Dialogs.SettingsWindow(this.settings, promptText);
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

            if (char.IsControl((char)codepoint) && codepoint < 0x10000)
            {
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

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        this.Opened -= this.OnWindowOpened;

        this.effectsService.DeferMacOSNativeTransparency();
        EditorPathDetector.PopulateUnsetPaths(this.settings);
        await this.ShowSettingsPersistenceErrorIfNeededAsync();

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
        this.FindControl<Border>("NeovimBorder")!.Child = editorControl;
        this.effectsService.UpdateBackgroundOpacity();
    }

    private void OnTitleChanged(string title)
    {
        this.Title = title;
        var titleText = this.FindControl<TextBlock>("TitleText");
        if (titleText is not null)
        {
            titleText.Text = title;
        }
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
        this.FindControl<Grid>("TitleBar")!.Background = brush;
        this.FindControl<Border>("NeovimBorder")!.Background = brush;
    }

    private void OnMacOSFullScreenChanged(bool isFullScreen)
    {
        this.FindControl<Grid>("TitleBar")!.IsVisible = !isFullScreen;
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

        var titleText = this.FindControl<TextBlock>("TitleText")!;
        titleText.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        titleText.FontWeight = FontWeight.Bold;
    }

    private async void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        await this.ShowSettingsDialogAsync();
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
}