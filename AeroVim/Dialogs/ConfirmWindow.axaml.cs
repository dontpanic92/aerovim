// <copyright file="ConfirmWindow.axaml.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

#pragma warning disable SA1009 // StyleCop 1.1.118 false positive with null-forgiving operator after closing parenthesis

namespace AeroVim.Dialogs
{
    using Avalonia.Controls;
    using Avalonia.Interactivity;

    /// <summary>
    /// A confirmation dialog with Yes and No buttons.
    /// </summary>
    public partial class ConfirmWindow : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfirmWindow"/> class.
        /// </summary>
        public ConfirmWindow()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfirmWindow"/> class.
        /// </summary>
        /// <param name="message">Dialog message.</param>
        /// <param name="title">Dialog title.</param>
        public ConfirmWindow(string message, string title)
        {
            this.InitializeComponent();
            this.Title = title;
            this.FindControl<TextBlock>("MessageTextBlock")!.Text = message;
        }

        /// <summary>
        /// Gets a value indicating whether the user confirmed the action.
        /// </summary>
        public bool Confirmed { get; private set; }

        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            this.Confirmed = true;
            this.Close();
        }

        private void No_Click(object sender, RoutedEventArgs e)
        {
            this.Confirmed = false;
            this.Close();
        }
    }
}
