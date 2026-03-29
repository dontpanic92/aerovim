// <copyright file="MainWindow.axaml.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using AeroVim.Controls;
    using AeroVim.Editor;
    using AeroVim.Settings;
    using AeroVim.Utilities;
    using AeroVim.VimClient;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.Primitives;
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
        private IEditorClient editorClient;
        private EditorControl editorControl;

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

            this.Width = this.settings.WindowWidth;
            this.Height = this.settings.WindowHeight;
            if (this.settings.IsMaximized)
            {
                this.WindowState = WindowState.Maximized;
            }

            this.Opened += this.OnWindowOpened;
        }

        /// <summary>
        /// Opens the settings dialog and returns its result.
        /// </summary>
        /// <param name="promptText">Optional prompt text displayed in the dialog.</param>
        /// <returns>The dialog result indicating whether the user accepted or cancelled.</returns>
        internal async Task<Dialogs.SettingsWindow.Result> ShowSettingsDialogAsync(string promptText = null)
        {
            // Capture current editor settings before opening the dialog
            var previousEditorType = this.settings.EditorType;
            var previousNeovimPath = this.settings.NeovimPath;
            var previousVimPath = this.settings.VimPath;

            var dialog = new Dialogs.SettingsWindow(promptText);
            await dialog.ShowDialog(this);

            if (dialog.CloseReason == Dialogs.SettingsWindow.Result.Ok)
            {
                await this.ShowTransparencyMismatchDialogAsync();

                // When opened at runtime (not during startup), warn if editor config changed
                if (promptText == null && this.HasEditorConfigChanged(previousEditorType, previousNeovimPath, previousVimPath))
                {
                    var msg = new Dialogs.MessageWindow(
                        "Editor backend changes will take effect the next time AeroVim is started.",
                        "Restart Required");
                    await msg.ShowDialog(this);
                }
            }

            return dialog.CloseReason;
        }

        /// <inheritdoc />
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == WindowStateProperty && RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var newState = (WindowState)change.NewValue;
                Dispatcher.UIThread.Post(
                    () =>
                    {
                        var nsWindow = this.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                        if (newState == WindowState.FullScreen)
                        {
                            MacOSInterop.ConfigureForFullScreen(nsWindow);
                        }
                        else
                        {
                            MacOSInterop.SetTransparentTitlebar(nsWindow);
                        }
                    },
                    DispatcherPriority.Background);
            }
        }

        /// <inheritdoc />
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            // Hide the PART_TransparencyFallback border that Avalonia's Window
            // template shows when ActualTransparencyLevel falls back to None.
            // Its default brush (white) appears behind semi-transparent content.
            if (e.NameScope.Find("PART_TransparencyFallback") is Border fallback)
            {
                fallback.IsVisible = false;
            }
        }

        /// <inheritdoc />
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (this.editorClient != null && KeyMapping.TryMap(e, out var text))
            {
                this.editorClient.Input(text);
                e.Handled = true;
            }
        }

        /// <inheritdoc />
        protected override void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);
            if (this.editorClient != null && !string.IsNullOrEmpty(e.Text))
            {
                // Only forward printable characters that were not already handled by OnKeyDown
                foreach (var ch in e.Text)
                {
                    if (!char.IsControl(ch))
                    {
                        // Escape '<' so Neovim doesn't interpret it as a special key sequence
                        if (ch == '<')
                        {
                            this.editorClient.Input("<lt>");
                        }
                        else
                        {
                            this.editorClient.Input(ch.ToString());
                        }
                    }
                }

                e.Handled = true;
            }
        }

        /// <inheritdoc />
        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            if (this.WindowState == WindowState.Maximized)
            {
                this.settings.IsMaximized = true;
            }
            else
            {
                this.settings.IsMaximized = false;
                this.settings.WindowWidth = (int)this.Width;
                this.settings.WindowHeight = (int)this.Height;
            }

            this.settings.Save();

            if (this.editorClient != null)
            {
                this.editorClient.EditorExited -= this.OnEditorExited;
            }

            this.editorControl?.Dispose();
            this.editorClient?.Dispose();
        }

        private async void OnWindowOpened(object sender, EventArgs e)
        {
            this.Opened -= this.OnWindowOpened;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Defer so that Avalonia finishes applying TransparencyLevelHint
                // before we override NSWindow properties.
                Dispatcher.UIThread.Post(
                    () =>
                    {
                        var nsWindow = this.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                        MacOSInterop.SetTransparentTitlebar(nsWindow);
                    },
                    DispatcherPriority.Background);
            }

            await this.InitializeEditorAsync();
        }

        private async Task InitializeEditorAsync()
        {
            this.AutoDetectEditorPath();

            while (true)
            {
                try
                {
                    this.editorClient = this.CreateEditorClient();
                    break;
                }
                catch (Exception)
                {
                    string editorName = this.settings.EditorType == EditorType.Vim ? "Vim" : "Neovim";
                    if (await this.ShowSettingsDialogAsync($"Please specify the path to {editorName}") == Dialogs.SettingsWindow.Result.Cancel)
                    {
                        this.Close();
                        return;
                    }
                }
            }

            this.editorControl = new EditorControl(this.editorClient);
            var editorBorder = this.FindControl<Border>("NeovimBorder");
            editorBorder.Child = this.editorControl;

            this.editorClient.EditorExited += this.OnEditorExited;

            this.editorClient.Command("set mouse=a");

            this.editorClient.TitleChanged += (string title) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var effectiveTitle = string.IsNullOrEmpty(title) ? "AeroVim" : title;
                    this.Title = effectiveTitle;
                    var titleText = this.FindControl<TextBlock>("TitleText");
                    if (titleText != null)
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
                    if (titleText != null)
                    {
                        titleText.Foreground = brush;
                    }

                    this.FindControl<Button>("SettingsButton").Foreground = brush;
                    this.FindControl<Button>("MinimizeButton").Foreground = brush;
                    this.FindControl<Button>("MaximizeButton").Foreground = brush;
                    this.FindControl<Button>("CloseButton").Foreground = brush;
                });
             };

            this.editorClient.BackgroundColorChanged += (int intColor) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    this.currentBackgroundColor = intColor;
                    this.settings.BackgroundColor = intColor;
                    this.SetupBlurBehind();
                });
             };

            this.settings.PropertyChanged += (sender, propChangedArgs) =>
            {
                switch (propChangedArgs.PropertyName)
                {
                    case nameof(AppSettings.EnableBlurBehind):
                    case nameof(AppSettings.BlurType):
                    case nameof(AppSettings.BackgroundOpacity):
                        Dispatcher.UIThread.Post(() => this.SetupBlurBehind());
                        break;
                    case nameof(AppSettings.EnableLigature):
                        this.editorControl.EnableLigature = this.settings.EnableLigature;
                        this.editorControl.InvalidateVisual();
                        break;
                }
             };
        }

        private void AutoDetectEditorPath()
        {
            if (this.settings.EditorType == EditorType.Vim)
            {
                if (string.IsNullOrEmpty(this.settings.VimPath))
                {
                    var detected = EditorPathDetector.FindVimInPath();
                    if (detected != null)
                    {
                        this.settings.VimPath = detected;
                        this.settings.Save();
                    }
                }
            }
            else
            {
                if (string.IsNullOrEmpty(this.settings.NeovimPath))
                {
                    var detected = NeovimPathDetector.FindNeovimInPath();
                    if (detected != null)
                    {
                        this.settings.NeovimPath = detected;
                        this.settings.Save();
                    }
                }
            }
        }

        private IEditorClient CreateEditorClient()
        {
            if (this.settings.EditorType == EditorType.Vim)
            {
                return new VimClient.VimClient(this.settings.VimPath);
            }

            return new NeovimClient.NeovimClient(this.settings.NeovimPath);
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
                this.TransparencyLevelHint = new[] { this.GetRequestedTransparencyLevel() };
            }
            else
            {
                this.TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var nsWindow = this.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                MacOSInterop.SetTransparentTitlebar(nsWindow);
            }
        }

        private void UpdateBackgroundOpacity()
        {
            float opacity = this.settings.EnableBlurBehind ? (float)this.settings.BackgroundOpacity : 1f;
            IBrush backgroundBrush = new SolidColorBrush(Helpers.GetAvaloniaColor(this.currentBackgroundColor, opacity));

            this.FindControl<Grid>("TitleBar").Background = backgroundBrush;
            this.FindControl<Border>("NeovimBorder").Background = backgroundBrush;

            if (this.editorControl != null)
            {
                this.editorControl.BackgroundAlpha = (byte)(opacity * 255);
                this.editorControl.InvalidateVisual();
            }
        }

        private void SetupMacOSTitleBar()
        {
            this.FindControl<Button>("MinimizeButton").IsVisible = false;
            this.FindControl<Button>("MaximizeButton").IsVisible = false;
            this.FindControl<Button>("CloseButton").IsVisible = false;

            this.FindControl<Border>("TrafficLightSpacer").Width = 78;
        }

        private async void OnWindowActivatedMacOS(object sender, EventArgs e)
        {
            // Re-show native traffic light buttons after the window regains
            // focus (e.g. after a modal dialog is dismissed). A short delay
            // is required because Avalonia's internal window management may
            // reset the NSWindow style mask after the Activated event fires.
            await Task.Delay(100);
            var nsWindow = this.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            MacOSInterop.SetTransparentTitlebar(nsWindow);
        }

        private void OnEditorExited(int exitCode)
        {
            Dispatcher.UIThread.Post(() => this.Close());
        }

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
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

        private async Task ShowTransparencyMismatchDialogAsync()
        {
            if (!this.settings.EnableBlurBehind)
            {
                return;
            }

            var requestedLevel = this.GetRequestedTransparencyLevel();

            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(500);

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

        private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
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

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = this.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
