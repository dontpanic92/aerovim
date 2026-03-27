// <copyright file="SettingsWindow.axaml.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Dialogs
{
    using System;
    using System.Runtime.InteropServices;
    using AeroVim.Settings;
    using AeroVim.Utilities;
    using Avalonia.Controls;
    using Avalonia.Interactivity;
    using Avalonia.Platform.Storage;

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
            this.FindControl<TextBox>("VimPathBox").Text = this.settings.VimPath;

            var editorTypeCombo = this.FindControl<ComboBox>("EditorTypeCombo");
            editorTypeCombo.SelectedIndex = (int)this.settings.EditorType;
            this.UpdatePathPanelVisibility();

            editorTypeCombo.SelectionChanged += (s, e) => this.UpdatePathPanelVisibility();

            this.FindControl<CheckBox>("LigatureCheckBox").IsChecked = this.settings.EnableLigature;

            var blurBehindCheckBox = this.FindControl<CheckBox>("BlurBehindCheckBox");
            blurBehindCheckBox.IsChecked = this.settings.EnableBlurBehind;

            var transparentRadio = this.FindControl<RadioButton>("TransparentRadio");
            transparentRadio.IsEnabled = this.settings.EnableBlurBehind && Helpers.TransparentAvailable();
            transparentRadio.IsChecked = this.settings.BlurType == 3 && this.settings.EnableBlurBehind;

            var gaussianRadio = this.FindControl<RadioButton>("GaussianRadio");
            gaussianRadio.IsEnabled = this.settings.EnableBlurBehind && Helpers.GaussianBlurAvailable();
            gaussianRadio.IsChecked = this.settings.BlurType == 0 && this.settings.EnableBlurBehind;

            var acrylicRadio = this.FindControl<RadioButton>("AcrylicRadio");
            acrylicRadio.IsEnabled = this.settings.EnableBlurBehind && Helpers.AcrylicBlurAvailable();
            acrylicRadio.IsChecked = this.settings.BlurType == 1 && this.settings.EnableBlurBehind;

            var micaRadio = this.FindControl<RadioButton>("MicaRadio");
            micaRadio.IsEnabled = this.settings.EnableBlurBehind && Helpers.MicaAvailable();
            micaRadio.IsChecked = this.settings.BlurType == 2 && this.settings.EnableBlurBehind;

            var opacitySlider = this.FindControl<Slider>("OpacitySlider");
            opacitySlider.Value = this.settings.BackgroundOpacity;
            opacitySlider.IsEnabled = this.settings.EnableBlurBehind;

            this.FindControl<TextBlock>("OpacityLabel").Text = this.settings.BackgroundOpacity.ToString("F2");

            blurBehindCheckBox.IsCheckedChanged += (s, e) =>
            {
                bool isChecked = blurBehindCheckBox.IsChecked == true;
                transparentRadio.IsEnabled = isChecked && Helpers.TransparentAvailable();
                gaussianRadio.IsEnabled = isChecked && Helpers.GaussianBlurAvailable();
                acrylicRadio.IsEnabled = isChecked && Helpers.AcrylicBlurAvailable();
                micaRadio.IsEnabled = isChecked && Helpers.MicaAvailable();
                opacitySlider.IsEnabled = isChecked;
            };
        }

        private void SaveUiToSettings()
        {
            this.settings.NeovimPath = this.FindControl<TextBox>("NeovimPathBox").Text ?? string.Empty;
            this.settings.VimPath = this.FindControl<TextBox>("VimPathBox").Text ?? string.Empty;
            this.settings.EditorType = (Settings.EditorType)this.FindControl<ComboBox>("EditorTypeCombo").SelectedIndex;
            this.settings.EnableLigature = this.FindControl<CheckBox>("LigatureCheckBox").IsChecked == true;
            this.settings.EnableBlurBehind = this.FindControl<CheckBox>("BlurBehindCheckBox").IsChecked == true;
            this.settings.BackgroundOpacity = this.FindControl<Slider>("OpacitySlider").Value;

            if (this.FindControl<RadioButton>("TransparentRadio").IsChecked == true)
            {
                this.settings.BlurType = 3;
            }
            else if (this.FindControl<RadioButton>("GaussianRadio").IsChecked == true)
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
            var fileTypeFilters = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new[]
                {
                    new FilePickerFileType("Executable Files") { Patterns = new[] { "*.exe" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } },
                }
                : new[]
                {
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } },
                };

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Neovim executable",
                AllowMultiple = false,
                FileTypeFilter = fileTypeFilters,
            });

            if (files.Count > 0)
            {
                this.FindControl<TextBox>("NeovimPathBox").Text = files[0].Path.LocalPath;
            }
        }

        private async void Detect_Click(object sender, RoutedEventArgs e)
        {
            var detected = NeovimPathDetector.FindNeovimInPath();
            if (detected == null)
            {
                var msg = new MessageWindow("Neovim was not found in PATH.", "Detect Neovim");
                await msg.ShowDialog(this);
                return;
            }

            var pathBox = this.FindControl<TextBox>("NeovimPathBox");
            var currentPath = pathBox.Text ?? string.Empty;

            if (string.IsNullOrEmpty(currentPath))
            {
                pathBox.Text = detected;
            }
            else if (!string.Equals(currentPath, detected, StringComparison.OrdinalIgnoreCase))
            {
                var confirm = new ConfirmWindow(
                    $"Detected Neovim at:\n{detected}\n\nReplace the current path?",
                    "Detect Neovim");
                await confirm.ShowDialog(this);

                if (confirm.Confirmed)
                {
                    pathBox.Text = detected;
                }
            }
        }

        private async void VimBrowse_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            var fileTypeFilters = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new[]
                {
                    new FilePickerFileType("Executable Files") { Patterns = new[] { "*.exe" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } },
                }
                : new[]
                {
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } },
                };

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Vim executable",
                AllowMultiple = false,
                FileTypeFilter = fileTypeFilters,
            });

            if (files.Count > 0)
            {
                this.FindControl<TextBox>("VimPathBox").Text = files[0].Path.LocalPath;
            }
        }

        private async void VimDetect_Click(object sender, RoutedEventArgs e)
        {
            var detected = EditorPathDetector.FindVimInPath();
            if (detected == null)
            {
                var msg = new MessageWindow("Vim was not found in PATH.", "Detect Vim");
                await msg.ShowDialog(this);
                return;
            }

            var pathBox = this.FindControl<TextBox>("VimPathBox");
            var currentPath = pathBox.Text ?? string.Empty;

            if (string.IsNullOrEmpty(currentPath))
            {
                pathBox.Text = detected;
            }
            else if (!string.Equals(currentPath, detected, StringComparison.OrdinalIgnoreCase))
            {
                var confirm = new ConfirmWindow(
                    $"Detected Vim at:\n{detected}\n\nReplace the current path?",
                    "Detect Vim");
                await confirm.ShowDialog(this);

                if (confirm.Confirmed)
                {
                    pathBox.Text = detected;
                }
            }
        }

        private void UpdatePathPanelVisibility()
        {
            var editorTypeCombo = this.FindControl<ComboBox>("EditorTypeCombo");
            bool isVim = editorTypeCombo.SelectedIndex == 1;
            this.FindControl<StackPanel>("NeovimPathPanel").IsVisible = !isVim;
            this.FindControl<StackPanel>("VimPathPanel").IsVisible = isVim;
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
