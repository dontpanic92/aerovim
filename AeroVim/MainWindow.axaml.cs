// <copyright file="MainWindow.axaml.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim;

using System.Diagnostics;
using System.Runtime.InteropServices;
using AeroVim.Controls;
using AeroVim.Editor;
using AeroVim.Settings;
using AeroVim.Utilities;
using AeroVim.VimClient;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

/// <summary>
/// The main window.
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppSettings settings = AppSettings.Default;
    private int currentBackgroundColor;
    private bool isMacFullScreen;
    private bool isSettingsDialogOpen;
    private IEditorClient? editorClient;
    private EditorControl? editorControl;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        this.currentBackgroundColor = this.settings.BackgroundColor;
        this.InitializeComponent();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            this.SetupMacOSTitleBar();
            this.Activated += this.OnWindowActivatedMacOS;
        }

        this.SetupBlurBehind();
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
        try
        {
            // Capture current editor settings before opening the dialog
            var previousEditorType = this.settings.EditorType;
            var previousNeovimPath = this.settings.NeovimPath;
            var previousVimPath = this.settings.VimPath;

            var dialog = new Dialogs.SettingsWindow(promptText);
            await dialog.ShowDialog(this);

            if (dialog.CloseReason == Dialogs.SettingsWindow.Result.Ok)
            {
                // When opened at runtime (not during startup), warn if editor config changed
                if (promptText is null &&
                    this.HasEditorConfigChanged(previousEditorType, previousNeovimPath, previousVimPath))
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
            this.isSettingsDialogOpen = false;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty && RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var newState = (WindowState)change.NewValue!;
            Dispatcher.UIThread.Post(
                () =>
                {
                    var nsWindow = this.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                    if (nsWindow == IntPtr.Zero)
                    {
                        Trace.WriteLine("AeroVim: macOS platform handle unavailable during WindowState change.");
                        return;
                    }

                    if (newState == WindowState.FullScreen)
                    {
                        this.isMacFullScreen = true;
                        this.FindControl<Grid>("TitleBar")!.IsVisible = false;
                        MacOSInterop.ConfigureForFullScreen(nsWindow);
                        this.UpdateBackgroundOpacity();
                    }
                    else
                    {
                        this.isMacFullScreen = false;
                        this.FindControl<Grid>("TitleBar")!.IsVisible = true;
                        MacOSInterop.SetTransparentTitlebar(nsWindow);
                        this.UpdateBackgroundOpacity();
                    }
                },
                DispatcherPriority.Background);
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

        if (this.editorClient is not null && KeyMapping.TryMap(e, out var text) && text is not null)
        {
            this.editorClient.Input(text);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (this.editorClient is not null && !string.IsNullOrEmpty(e.Text))
        {
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
                    this.editorClient.Input("<lt>");
                }
                else
                {
                    this.editorClient.Input(grapheme);
                }
            }

            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        WindowSettingsPersistence.Capture(this, this.settings);
        this.settings.Save();

        if (this.editorClient is not null)
        {
            this.editorClient.EditorExited -= this.OnEditorExited;
        }

        this.editorControl?.Dispose();
        this.editorClient?.Dispose();
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        this.Opened -= this.OnWindowOpened;

        this.DeferMacOSNativeTransparency();
        await this.InitializeEditorAsync();
    }

    private async Task InitializeEditorAsync()
    {
        EditorPathDetector.PopulateUnsetPaths(this.settings);
        await this.ShowSettingsPersistenceErrorIfNeededAsync();

        while (true)
        {
            try
            {
                this.editorClient = EditorClientFactory.Create(this.settings);
                break;
            }
            catch (Exception)
            {
                string editorName = this.settings.EditorType == EditorType.Vim ? "Vim" : "Neovim";
                if (await this.ShowSettingsDialogAsync($"Please specify the path to {editorName}") ==
                    Dialogs.SettingsWindow.Result.Cancel)
                {
                    this.Close();
                    return;
                }
            }
        }

        this.editorControl = new EditorControl(this.editorClient);
        var editorBorder = this.FindControl<Border>("NeovimBorder")!;
        editorBorder.Child = this.editorControl;
        this.UpdateBackgroundOpacity();

        this.editorClient.EditorExited += this.OnEditorExited;

        this.editorClient.Command("set mouse=a");

        this.editorClient.TitleChanged += (string title) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var effectiveTitle = string.IsNullOrEmpty(title) ? "AeroVim" : title;
                this.Title = effectiveTitle;
                var titleText = this.FindControl<TextBlock>("TitleText");
                if (titleText is not null)
                {
                    titleText.Text = effectiveTitle;
                }
            });
        };

        this.editorClient.ForegroundColorChanged += (int intColor) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var color = Helpers.GetAvaloniaColor(intColor);
                var brush = new Avalonia.Media.SolidColorBrush(color);
                var titleText = this.FindControl<TextBlock>("TitleText");
                if (titleText is not null)
                {
                    titleText.Foreground = brush;
                }

                this.FindControl<Button>("SettingsButton")!.Foreground = brush;
                this.FindControl<Button>("MinimizeButton")!.Foreground = brush;
                this.FindControl<Button>("MaximizeButton")!.Foreground = brush;
                this.FindControl<Button>("CloseButton")!.Foreground = brush;
            });
        };

        this.editorClient.BackgroundColorChanged += (int intColor) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                this.currentBackgroundColor = intColor;
                this.settings.BackgroundColor = intColor;
                this.settings.Save();
                this.UpdateBackgroundOpacity();
            });
        };

        this.settings.PropertyChanged += (sender, propChangedArgs) =>
        {
            switch (propChangedArgs.PropertyName)
            {
                case nameof(AppSettings.EnableBlurBehind):
                case nameof(AppSettings.BlurType):
                    Dispatcher.UIThread.Post(() =>
                    {
                        this.SetupBlurBehind();
                        this.DeferMacOSNativeTransparency();
                        Dispatcher.UIThread.InvokeAsync(async Task () =>
                            await this.TestAndShowTransparencyMismatchDialogAsync());
                    });
                    break;

                case nameof(AppSettings.BackgroundOpacity):
                    Dispatcher.UIThread.Post(() => this.UpdateBackgroundOpacity());
                    break;
                case nameof(AppSettings.EnableLigature):
                    this.editorControl.EnableLigature = this.settings.EnableLigature;
                    this.editorControl.InvalidateVisual();
                    break;
            }
        };
    }

    private void SetupBlurBehind()
    {
        this.TransparencyBackgroundFallback = Brushes.Transparent;
        this.Background = Brushes.Transparent;
        this.UpdateTransparencyLevelHint();
        this.UpdateBackgroundOpacity();
    }

    private void UpdateTransparencyLevelHint()
    {
        if (this.settings.EnableBlurBehind)
        {
            this.TransparencyLevelHint = [this.GetRequestedTransparencyLevel()];
        }
        else
        {
            this.TransparencyLevelHint = [WindowTransparencyLevel.None];
        }
    }

    /// <summary>
    /// Defers macOS native transparency setup (transparent titlebar, traffic light
    /// buttons) to <see cref="DispatcherPriority.Background"/> so that Avalonia
    /// finishes processing the transparency level hint before we override NSWindow
    /// properties. A no-op on non-macOS platforms.
    /// </summary>
    private void DeferMacOSNativeTransparency()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                var nsWindow = this.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (nsWindow == IntPtr.Zero)
                {
                    Trace.WriteLine("AeroVim: macOS platform handle unavailable, skipping native transparency setup.");
                    return;
                }

                MacOSInterop.SetTransparentTitlebar(nsWindow);
            },
            DispatcherPriority.Background);
    }

    private void UpdateBackgroundOpacity()
    {
        float opacity = this.isMacFullScreen ? 1f :
            this.settings.EnableBlurBehind ? (float)this.settings.BackgroundOpacity : 1f;
        IBrush backgroundBrush = new SolidColorBrush(Helpers.GetAvaloniaColor(this.currentBackgroundColor, opacity));

        this.FindControl<Grid>("TitleBar")!.Background = backgroundBrush;
        this.FindControl<Border>("NeovimBorder")!.Background = backgroundBrush;

        if (this.editorControl is not null)
        {
            this.editorControl.BackgroundAlpha = (byte)(opacity * 255);
            this.editorControl.InvalidateVisual();
        }
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

    private async void OnWindowActivatedMacOS(object? sender, EventArgs e)
    {
        try
        {
            // Yield to Background priority so Avalonia finishes any internal
            // NSWindow style mask resets before we reapply our overrides.
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            var nsWindow = this.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (nsWindow == IntPtr.Zero)
            {
                Trace.WriteLine("AeroVim: macOS platform handle unavailable in OnWindowActivatedMacOS.");
                return;
            }

            MacOSInterop.SetTransparentTitlebar(nsWindow);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"AeroVim: OnWindowActivatedMacOS failed: {ex.Message}");
        }
    }

    private async void OnEditorExited(int exitCode)
    {
        if (exitCode != 0)
        {
            string editorName = this.settings.EditorType == EditorType.Vim ? "Vim" : "Neovim";
            string message = exitCode == -1
                ? $"{editorName} failed to start. Please check the executable path in Settings."
                : $"{editorName} exited unexpectedly (exit code {exitCode}). Please verify the executable path in Settings.";

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await this.ShowSettingsDialogAsync(message);
                this.Close();
            });
        }
        else
        {
            Dispatcher.UIThread.Post(() => this.Close());
        }
    }

    private async void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        await this.ShowSettingsDialogAsync();
    }

    private bool HasEditorConfigChanged(EditorType previousType, string previousNeovimPath, string previousVimPath)
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

    private WindowTransparencyLevel GetRequestedTransparencyLevel()
    {
        return this.settings.BlurType switch
        {
            0 => WindowTransparencyLevel.Blur,
            1 => WindowTransparencyLevel.AcrylicBlur,
            2 => WindowTransparencyLevel.Mica,
            3 => WindowTransparencyLevel.Transparent,
            _ => WindowTransparencyLevel.None,
        };
    }

    private async Task TestAndShowTransparencyMismatchDialogAsync()
    {
        if (!this.settings.EnableBlurBehind)
        {
            return;
        }

        var requestedLevel = this.GetRequestedTransparencyLevel();

        // Yield to let Avalonia process the TransparencyLevelHint change.
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        // Poll briefly for the platform to finish applying the effect.
        // Some compositors need extra time beyond the dispatcher yield.
        for (int i = 0; i < 5; i++)
        {
            if (this.ActualTransparencyLevel == requestedLevel)
            {
                return;
            }

            await Task.Delay(100);
        }

        var actualLevel = this.ActualTransparencyLevel;
        if (actualLevel == requestedLevel)
        {
            return;
        }

        var dialog = new Dialogs.MessageWindow(
            $"The requested transparency level {requestedLevel} is not supported on your system. Falling back to {actualLevel}",
            "Transparency Level Fallback");
        await dialog.ShowDialog(this);
    }

    private async Task ShowSettingsPersistenceErrorIfNeededAsync()
    {
        if (string.IsNullOrEmpty(this.settings.LastPersistenceError))
        {
            return;
        }

        var dialog = new Dialogs.MessageWindow(
            $"Settings could not be fully loaded or saved:\n{this.settings.LastPersistenceError}",
            "Settings Warning");
        await dialog.ShowDialog(this);
        this.settings.ClearLastPersistenceError();
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