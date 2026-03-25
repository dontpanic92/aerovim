// <copyright file="SettingsDialog.xaml.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Dialogs
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;
    using Dotnvim.Utilities;

    /// <summary>
    /// Interaction logic for SettingsDialog.xaml.
    /// </summary>
    public partial class SettingsDialog : Window
    {
        /*private static readonly DependencyProperty BlurBehindEnabledProperty
            = DependencyProperty.Register(
                "BlurBehindEnabled",
                typeof(bool),
                typeof(SettingsDialog),
                new PropertyMetadata(
                    false,
                    (DependencyObject d, DependencyPropertyChangedEventArgs e) =>
                    {
                    }));*/

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsDialog"/> class.
        /// </summary>
        /// <param name="promptText">Text for prompt.</param>
        public SettingsDialog(string promptText = null)
        {
            this.InitializeComponent();

            if (!string.IsNullOrWhiteSpace(promptText))
            {
                this.PromptLabel.Content = promptText;
                this.PromptLabel.Visibility = Visibility.Visible;
            }

            // this.SetValue(BlurBehindEnabledProperty, Helpers.BlurBehindEnabled());
            this.Closing += this.SettingsDialog_Closing;
        }

        /// <summary>
        /// Reason of closing the window.
        /// </summary>
        public enum Result
        {
            /// <summary>
            /// Window is not closed yet
            /// </summary>
            NotClosed,

            /// <summary>
            /// Window closed due to Ok button clicked
            /// </summary>
            Ok,

            /// <summary>
            /// Window closed due to Cancel button clicked
            /// </summary>
            Cancel,
        }

        /// <summary>
        /// Gets or sets a value indicating whether the blur behind feature is enabled.
        /// </summary>
        public bool BlurBehindEnabled
        {
            get
            {
                // return (bool)this.GetValue(BlurBehindEnabledProperty);
                return Helpers.BlurBehindEnabled();
            }

            set
            {
                if (Properties.Settings.Default.EnableBlurBehind != value)
                {
                    Properties.Settings.Default.EnableBlurBehind = value;
                }

                // this.SetValue(BlurBehindEnabledProperty, value);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the blur behind feature available.
        /// </summary>
        public bool BlurBehindAvailable
        {
            get
            {
                return Helpers.BlurBehindAvailable();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the acrylic blur feature available.
        /// </summary>
        public bool AcrylicBlurAvailable
        {
            get
            {
                return Helpers.AcrylicBlurAvailable();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the Gaussian blur feature available.
        /// </summary>
        public bool GaussianBlurAvailable
        {
            get
            {
                return Helpers.GaussianBlurAvailable();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the Mica effect is available.
        /// </summary>
        public bool MicaAvailable
        {
            get
            {
                return Helpers.MicaAvailable();
            }
        }

        /// <summary>
        /// Gets the reason of closing the window.
        /// </summary>
        public Result CloseReason { get; private set; } = Result.NotClosed;

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

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe|All Files(*.*)|*.*",
                RestoreDirectory = true,
                Multiselect = false,
            };

            if (openFileDialog.ShowDialog() == true)
            {
                this.NeovimPath.Text = Dotnvim.Properties.Settings.Default.NeovimPath = openFileDialog.FileName;
            }
        }

        private void SettingsDialog_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            switch (this.CloseReason)
            {
                case Result.Ok:
                    Dotnvim.Properties.Settings.Default.Save();
                    break;
                case Result.Cancel:
                    Dotnvim.Properties.Settings.Default.Reload();
                    break;
                case Result.NotClosed:
                    this.CloseReason = Result.Cancel;
                    Dotnvim.Properties.Settings.Default.Reload();
                    break;
            }
        }
    }
}
