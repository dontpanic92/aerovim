// <copyright file="App.axaml.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim
{
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using Avalonia;
    using Avalonia.Controls.ApplicationLifetimes;
    using Avalonia.Markup.Xaml;
    using Avalonia.Styling;

    /// <summary>
    /// The Avalonia application.
    /// </summary>
    public class App : Application
    {
        /// <inheritdoc />
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            this.ApplyPlatformTheme();
        }

        /// <inheritdoc />
        public override void OnFrameworkInitializationCompleted()
        {
            if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// Applies the platform-specific Devolutions theme.
        /// </summary>
        private void ApplyPlatformTheme()
        {
            ISupportInitialize theme;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                theme = new Devolutions.AvaloniaTheme.DevExpress.DevolutionsDevExpressTheme();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                theme = new Devolutions.AvaloniaTheme.MacOS.DevolutionsMacOsTheme();
            }
            else
            {
                theme = new Devolutions.AvaloniaTheme.Linux.DevolutionsLinuxYaruTheme();
            }

            theme.BeginInit();
            theme.EndInit();
            this.Styles.Add((Styles)theme);
        }
    }
}
