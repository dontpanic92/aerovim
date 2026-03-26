// <copyright file="MainWindow.axaml.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim
{
    using System;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Input;
    using Avalonia.Interactivity;
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
        private NeovimClient.NeovimClient neovimClient;
        private NeovimControl neovimControl;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            this.InitializeComponent();

            this.SetupBlurBehind();

            while (true)
            {
                try
                {
                    this.neovimClient = new NeovimClient.NeovimClient(this.settings.NeovimPath);
                    break;
                }
                catch (Exception)
                {
                    var dialog = new Dialogs.SettingsWindow("Please specify the path to Neovim");
                    dialog.ShowDialog(this).GetAwaiter().GetResult();
                    if (dialog.CloseReason == Dialogs.SettingsWindow.Result.Cancel)
                    {
                        Environment.Exit(0);
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
                    this.Title = title;
                    var titleText = this.FindControl<TextBlock>("TitleText");
                    if (titleText != null)
                    {
                        titleText.Text = title;
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
                    this.SetupBlurBehind();
                });
             };

            this.settings.PropertyChanged += (sender, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(AppSettings.EnableBlurBehind):
                    case nameof(AppSettings.BlurType):
                        Dispatcher.UIThread.Post(() => this.SetupBlurBehind());
                        break;
                    case nameof(AppSettings.EnableLigature):
                        this.neovimControl.EnableLigature = this.settings.EnableLigature;
                        this.neovimControl.InvalidateVisual();
                        break;
                }
             };

            this.Width = this.settings.WindowWidth;
            this.Height = this.settings.WindowHeight;
            if (this.settings.IsMaximized)
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        /// <inheritdoc />
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (KeyMapping.TryMap(e, out var text))
            {
                this.neovimClient.Input(text);
                e.Handled = true;
            }
        }

        /// <inheritdoc />
        protected override void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);
            if (!string.IsNullOrEmpty(e.Text))
            {
                // Only forward printable characters that were not already handled by OnKeyDown
                foreach (var ch in e.Text)
                {
                    if (!char.IsControl(ch))
                    {
                        this.neovimClient.Input(ch.ToString());
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

            this.neovimClient.NeovimExited -= this.OnNeovimExited;
            this.neovimControl?.Dispose();
            this.neovimClient?.Dispose();
        }

        private void SetupBlurBehind()
        {
            if (Helpers.BlurBehindAvailable() && this.settings.EnableBlurBehind)
            {
                switch (this.settings.BlurType)
                {
                    case 0: // Gaussian Blur
                        this.TransparencyLevelHint = new[] { WindowTransparencyLevel.Blur };
                        break;
                    case 1: // Acrylic Blur
                        this.TransparencyLevelHint = new[] { WindowTransparencyLevel.AcrylicBlur };
                        break;
                    case 2: // Mica
                        this.TransparencyLevelHint = new[] { WindowTransparencyLevel.Mica };
                        break;
                    default:
                        this.TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
                        break;
                }
            }
            else
            {
                this.TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
            }
        }

        private void OnNeovimExited(int exitCode)
        {
            Dispatcher.UIThread.Post(() => this.Close());
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Dialogs.SettingsWindow();
            dialog.ShowDialog(this);
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
