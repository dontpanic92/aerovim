// <copyright file="AppSettings.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Settings
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text.Json;

    /// <summary>
    /// Application settings with JSON persistence.
    /// </summary>
    public sealed class AppSettings : INotifyPropertyChanged
    {
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AeroVim");

        private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

        private static AppSettings defaultInstance;

        private string neovimPath = string.Empty;
        private double backgroundOpacity = 0.75;
        private bool enableLigature = true;
        private bool enableBlurBehind = true;
        private int blurType = 1;
        private bool isMaximized;
        private int windowWidth = 800;
        private int windowHeight = 600;
        private int backgroundColor = 0xFFFFFF;

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the default settings instance.
        /// </summary>
        public static AppSettings Default
        {
            get
            {
                if (defaultInstance == null)
                {
                    defaultInstance = Load();
                }

                return defaultInstance;
            }
        }

        /// <summary>
        /// Gets or sets the Neovim executable path.
        /// </summary>
        public string NeovimPath
        {
            get => this.neovimPath;
            set => this.SetField(ref this.neovimPath, value);
        }

        /// <summary>
        /// Gets or sets the background opacity.
        /// </summary>
        public double BackgroundOpacity
        {
            get => this.backgroundOpacity;
            set => this.SetField(ref this.backgroundOpacity, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether font ligature is enabled.
        /// </summary>
        public bool EnableLigature
        {
            get => this.enableLigature;
            set => this.SetField(ref this.enableLigature, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether blur behind is enabled.
        /// </summary>
        public bool EnableBlurBehind
        {
            get => this.enableBlurBehind;
            set => this.SetField(ref this.enableBlurBehind, value);
        }

        /// <summary>
        /// Gets or sets the blur type.
        /// </summary>
        public int BlurType
        {
            get => this.blurType;
            set => this.SetField(ref this.blurType, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the window is maximized.
        /// </summary>
        public bool IsMaximized
        {
            get => this.isMaximized;
            set => this.SetField(ref this.isMaximized, value);
        }

        /// <summary>
        /// Gets or sets the window width.
        /// </summary>
        public int WindowWidth
        {
            get => this.windowWidth;
            set => this.SetField(ref this.windowWidth, value);
        }

        /// <summary>
        /// Gets or sets the window height.
        /// </summary>
        public int WindowHeight
        {
            get => this.windowHeight;
            set => this.SetField(ref this.windowHeight, value);
        }

        /// <summary>
        /// Gets or sets the last background color from Neovim.
        /// </summary>
        public int BackgroundColor
        {
            get => this.backgroundColor;
            set => this.SetField(ref this.backgroundColor, value);
        }

        /// <summary>
        /// Save settings to disk.
        /// </summary>
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                var json = JsonSerializer.Serialize(this, AppSettingsJsonContext.Default.AppSettings);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception)
            {
                // Silently ignore save errors
            }
        }

        /// <summary>
        /// Reload settings from disk, discarding in-memory changes.
        /// </summary>
        public void Reload()
        {
            var fresh = Load();
            this.NeovimPath = fresh.NeovimPath;
            this.BackgroundOpacity = fresh.BackgroundOpacity;
            this.EnableLigature = fresh.EnableLigature;
            this.EnableBlurBehind = fresh.EnableBlurBehind;
            this.BlurType = fresh.BlurType;
            this.IsMaximized = fresh.IsMaximized;
            this.WindowWidth = fresh.WindowWidth;
            this.WindowHeight = fresh.WindowHeight;
            this.BackgroundColor = fresh.BackgroundColor;
        }

        private static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings) as AppSettings ?? new AppSettings();
                }
            }
            catch (Exception)
            {
                // Fall through to default
            }

            return new AppSettings();
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (!Equals(field, value))
            {
                field = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
