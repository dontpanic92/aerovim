// <copyright file="WindowSettingsPersistence.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Utilities
{
    using AeroVim.Settings;
    using Avalonia.Controls;

    /// <summary>
    /// Persists and restores window geometry from application settings.
    /// </summary>
    public static class WindowSettingsPersistence
    {
        /// <summary>
        /// Apply stored geometry to a window.
        /// </summary>
        /// <param name="window">The target window.</param>
        /// <param name="settings">Application settings.</param>
        public static void Apply(Window window, AppSettings settings)
        {
            window.Width = settings.WindowWidth;
            window.Height = settings.WindowHeight;
            if (settings.IsMaximized)
            {
                window.WindowState = WindowState.Maximized;
            }
        }

        /// <summary>
        /// Capture current geometry from a window into settings.
        /// </summary>
        /// <param name="window">The source window.</param>
        /// <param name="settings">Application settings.</param>
        public static void Capture(Window window, AppSettings settings)
        {
            if (window.WindowState == WindowState.Maximized)
            {
                settings.IsMaximized = true;
                return;
            }

            settings.IsMaximized = false;
            settings.WindowWidth = (int)window.Width;
            settings.WindowHeight = (int)window.Height;
        }
    }
}
