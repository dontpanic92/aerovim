// <copyright file="MainWindow.axaml.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim
{
    using System;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Input;
    using Avalonia.Interactivity;
    using Avalonia.Media;
    using Avalonia.Threading;
    using Dotnvim.Controls;
    using Dotnvim.Settings;
    using Dotnvim.Utilities;

    /// <summary>
    /// The main window.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly AppSettings settings = AppSettings.Default;
        private int currentBackgroundColor;
        private NeovimClient.NeovimClient neovimClient;
        private NeovimControl neovimControl;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            this.currentBackgroundColor = this.settings.BackgroundColor;
            this.InitializeComponent();

            this.SetupBlurBehind();

            this.Width = this.settings.WindowWidth;
            this.Height = this.settings.WindowHeight;
            if (this.settings.IsMaximized)
            {
                this.WindowState = WindowState.Maximized;
            }

            this.Opened += this.OnWindowOpened;
        }

        /// <inheritdoc />
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (this.neovimClient != null && KeyMapping.TryMap(e, out var text))
            {
                this.neovimClient.Input(text);
                e.Handled = true;
            }
        }

        /// <inheritdoc />
        protected override void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);
            if (this.neovimClient != null && !string.IsNullOrEmpty(e.Text))
            {
                // Only forward printable characters that were not already handled by OnKeyDown
                foreach (var ch in e.Text)
                {
                    if (!char.IsControl(ch))
                    {
                        // Escape '<' so Neovim doesn't interpret it as a special key sequence
                        if (ch == '<')
                        {
                            this.neovimClient.Input("<lt>");
                        }
                        else
                        {
                            this.neovimClient.Input(ch.ToString());
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

            if (this.neovimClient != null)
            {
                this.neovimClient.NeovimExited -= this.OnNeovimExited;
            }

            this.neovimControl?.Dispose();
            this.neovimClient?.Dispose();
        }

        private async void OnWindowOpened(object sender, EventArgs e)
        {
            this.Opened -= this.OnWindowOpened;
            await this.InitializeNeovimAsync();
        }

        private async Task InitializeNeovimAsync()
        {
            while (true)
            {
                try
                {
                    this.neovimClient = new NeovimClient.NeovimClient(this.settings.NeovimPath);
                    break;
                }
                catch (Exception)
                {
                    if (await this.ShowSettingsDialogAsync("Please specify the path to Neovim") == Dialogs.SettingsWindow.Result.Cancel)
                    {
                        this.Close();
                        return;
                    }
                }
            }

            this.neovimControl = new NeovimControl(this.neovimClient);
            var neovimBorder = this.FindControl<Border>("NeovimBorder");
            neovimBorder.Child = this.neovimControl;

            this.neovimClient.NeovimExited += this.OnNeovimExited;

            this.neovimClient.TitleChanged += (string title) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var effectiveTitle = string.IsNullOrEmpty(title) ? "dotnvim" : title;
                    this.Title = effectiveTitle;
                    var titleText = this.FindControl<TextBlock>("TitleText");
                    if (titleText != null)
                    {
                        titleText.Text = effectiveTitle;
                    }
                });
             };

            this.neovimClient.ForegroundColorChanged += (int intColor) =>
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

            this.neovimClient.BackgroundColorChanged += (int intColor) =>
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
                        this.neovimControl.EnableLigature = this.settings.EnableLigature;
                        this.neovimControl.InvalidateVisual();
                        break;
                }
             };
        }

        private void SetupBlurBehind()
        {
            if (this.settings.EnableBlurBehind)
            {
                this.TransparencyLevelHint = new[] { this.GetRequestedTransparencyLevel() };
            }
            else
            {
                this.TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
            }

            float opacity = this.settings.EnableBlurBehind ? (float)this.settings.BackgroundOpacity : 1f;
            IBrush backgroundBrush = new SolidColorBrush(Helpers.GetAvaloniaColor(this.currentBackgroundColor, opacity));

            this.FindControl<Grid>("TitleBar").Background = backgroundBrush;
            this.FindControl<Border>("NeovimBorder").Background = backgroundBrush;

            if (this.neovimControl != null)
            {
                this.neovimControl.BackgroundAlpha = (byte)(opacity * 255);
                this.neovimControl.InvalidateVisual();
            }
        }

        private void OnNeovimExited(int exitCode)
        {
            Dispatcher.UIThread.Post(() => this.Close());
        }

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            await this.ShowSettingsDialogAsync();
        }

        private async Task<Dialogs.SettingsWindow.Result> ShowSettingsDialogAsync(string promptText = null)
        {
            var dialog = new Dialogs.SettingsWindow(promptText);
            await dialog.ShowDialog(this);

            if (dialog.CloseReason == Dialogs.SettingsWindow.Result.Ok)
            {
                await this.ShowTransparencyMismatchDialogAsync();
            }

            return dialog.CloseReason;
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
            await Task.Delay(100);

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
