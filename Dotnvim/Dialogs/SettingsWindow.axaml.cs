// <copyright file="SettingsWindow.axaml.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Dialogs
{
    using System;
    using Avalonia.Controls;
    using Avalonia.Interactivity;
    using Avalonia.Platform.Storage;
    using Dotnvim.Settings;
    using Dotnvim.Utilities;

    /// <summary>
    /// Interaction logic for SettingsWindow.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings settings = AppSettings.Default;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsWindow"/> class.
        /// </summary>
        public SettingsWindow()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsWindow"/> class.
        /// </summary>
        /// <param name="promptText">Text for prompt.</param>
        public SettingsWindow(string promptText)
        {
            this.InitializeComponent();

            if (!string.IsNullOrWhiteSpace(promptText))
            {
                var promptLabel = this.FindControl<TextBlock>("PromptLabel");
                promptLabel.Text = promptText;
                promptLabel.IsVisible = true;
            }

            this.LoadSettingsToUi();

            var opacitySlider = this.FindControl<Slider>("OpacitySlider");
            opacitySlider.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty)
                {
                    this.FindControl<TextBlock>("OpacityLabel").Text = opacitySlider.Value.ToString("F2");
                }
            };

            this.Closing += this.OnWindowClosing;
        }

        /// <summary>
        /// Reason of closing the window.
        /// </summary>
        public enum Result
        {
            /// <summary>
            /// Window is not closed yet.
            /// </summary>
            NotClosed,

            /// <summary>
            /// Window closed due to Ok button clicked.
            /// </summary>
            Ok,

            /// <summary>
            /// Window closed due to Cancel button clicked.
            /// </summary>
            Cancel,
        }

        /// <summary>
        /// Gets the reason of closing the window.
        /// </summary>
        public Result CloseReason { get; private set; } = Result.NotClosed;

        private void LoadSettingsToUi()
        {
            this.FindControl<TextBox>("NeovimPathBox").Text = this.settings.NeovimPath;
            this.FindControl<CheckBox>("LigatureCheckBox").IsChecked = this.settings.EnableLigature;

            var blurBehindCheckBox = this.FindControl<CheckBox>("BlurBehindCheckBox");
            blurBehindCheckBox.IsChecked = Helpers.BlurBehindEnabled();
            blurBehindCheckBox.IsEnabled = Helpers.BlurBehindAvailable();

            var gaussianRadio = this.FindControl<RadioButton>("GaussianRadio");
            gaussianRadio.IsEnabled = Helpers.GaussianBlurAvailable() && this.settings.EnableBlurBehind;
            gaussianRadio.IsChecked = this.settings.BlurType == 0 && this.settings.EnableBlurBehind;

            var acrylicRadio = this.FindControl<RadioButton>("AcrylicRadio");
            acrylicRadio.IsEnabled = Helpers.AcrylicBlurAvailable() && this.settings.EnableBlurBehind;
            acrylicRadio.IsChecked = this.settings.BlurType == 1 && this.settings.EnableBlurBehind;

            var micaRadio = this.FindControl<RadioButton>("MicaRadio");
            micaRadio.IsEnabled = Helpers.MicaAvailable() && this.settings.EnableBlurBehind;
            micaRadio.IsChecked = this.settings.BlurType == 2 && this.settings.EnableBlurBehind;

            var opacitySlider = this.FindControl<Slider>("OpacitySlider");
            opacitySlider.Value = this.settings.BackgroundOpacity;
            opacitySlider.IsEnabled = this.settings.EnableBlurBehind;

            this.FindControl<TextBlock>("OpacityLabel").Text = this.settings.BackgroundOpacity.ToString("F2");

            blurBehindCheckBox.IsCheckedChanged += (s, e) =>
            {
                bool isChecked = blurBehindCheckBox.IsChecked == true;
                gaussianRadio.IsEnabled = Helpers.GaussianBlurAvailable() && isChecked;
                acrylicRadio.IsEnabled = Helpers.AcrylicBlurAvailable() && isChecked;
                micaRadio.IsEnabled = Helpers.MicaAvailable() && isChecked;
                opacitySlider.IsEnabled = isChecked;
            };
        }

        private void SaveUiToSettings()
        {
            this.settings.NeovimPath = this.FindControl<TextBox>("NeovimPathBox").Text ?? string.Empty;
            this.settings.EnableLigature = this.FindControl<CheckBox>("LigatureCheckBox").IsChecked == true;
            this.settings.EnableBlurBehind = this.FindControl<CheckBox>("BlurBehindCheckBox").IsChecked == true;
            this.settings.BackgroundOpacity = this.FindControl<Slider>("OpacitySlider").Value;

            if (this.FindControl<RadioButton>("GaussianRadio").IsChecked == true)
            {
                this.settings.BlurType = 0;
            }
            else if (this.FindControl<RadioButton>("AcrylicRadio").IsChecked == true)
            {
                this.settings.BlurType = 1;
            }
            else if (this.FindControl<RadioButton>("MicaRadio").IsChecked == true)
            {
                this.settings.BlurType = 2;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            this.CloseReason = Result.Ok;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.CloseReason = Result.Cancel;
            this.Close();
        }

        private async void Browse_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Neovim executable",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Executable Files") { Patterns = new[] { "*.exe" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } },
                },
            });

            if (files.Count > 0)
            {
                this.FindControl<TextBox>("NeovimPathBox").Text = files[0].Path.LocalPath;
            }
        }

        private void OnWindowClosing(object sender, WindowClosingEventArgs e)
        {
            switch (this.CloseReason)
            {
                case Result.Ok:
                    this.SaveUiToSettings();
                    this.settings.Save();
                    break;
                case Result.Cancel:
                    this.settings.Reload();
                    break;
                case Result.NotClosed:
                    this.CloseReason = Result.Cancel;
                    this.settings.Reload();
                    break;
            }
        }
    }
}
