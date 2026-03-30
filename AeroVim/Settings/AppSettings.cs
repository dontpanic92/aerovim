// <copyright file="AppSettings.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Settings;

using System.ComponentModel;
using System.Diagnostics;
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

    private static readonly Lazy<AppSettings> DefaultInstance = new Lazy<AppSettings>(Load);

    private string neovimPath = string.Empty;
    private string vimPath = string.Empty;
    private EditorType editorType = EditorType.Neovim;
    private double backgroundOpacity = 0.75;
    private bool enableLigature = true;
    private bool enableBlurBehind = true;
    private int blurType = 1;
    private bool isMaximized;
    private int windowWidth = 800;
    private int windowHeight = 600;
    private int backgroundColor = 0xFFFFFF;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the default settings instance.
    /// </summary>
    public static AppSettings Default
    {
        get => DefaultInstance.Value;
    }

    /// <summary>
    /// Gets the last persistence error, if any.
    /// </summary>
    public string LastPersistenceError { get; private set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Neovim executable path.
    /// </summary>
    public string NeovimPath
    {
        get => this.neovimPath;
        set => this.SetField(ref this.neovimPath, value);
    }

    /// <summary>
    /// Gets or sets the Vim executable path.
    /// </summary>
    public string VimPath
    {
        get => this.vimPath;
        set => this.SetField(ref this.vimPath, value);
    }

    /// <summary>
    /// Gets or sets the editor type (Neovim or Vim).
    /// </summary>
    public EditorType EditorType
    {
        get => this.editorType;
        set => this.SetField(ref this.editorType, value);
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
    /// <returns><c>true</c> if the settings were saved successfully; otherwise, <c>false</c>.</returns>
    public bool Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(this, AppSettingsJsonContext.Default.AppSettings);
            File.WriteAllText(SettingsPath, json);
            this.LastPersistenceError = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or JsonException)
        {
            this.LastPersistenceError = ex.Message;
            Trace.TraceError($"AeroVim: Failed to save settings: {ex}");
            return false;
        }
    }

    /// <summary>
    /// Reload settings from disk, discarding in-memory changes.
    /// </summary>
    /// <returns><c>true</c> if the settings were reloaded successfully; otherwise, <c>false</c>.</returns>
    public bool Reload()
    {
        var fresh = Load();
        this.NeovimPath = fresh.NeovimPath;
        this.VimPath = fresh.VimPath;
        this.EditorType = fresh.EditorType;
        this.BackgroundOpacity = fresh.BackgroundOpacity;
        this.EnableLigature = fresh.EnableLigature;
        this.EnableBlurBehind = fresh.EnableBlurBehind;
        this.BlurType = fresh.BlurType;
        this.IsMaximized = fresh.IsMaximized;
        this.WindowWidth = fresh.WindowWidth;
        this.WindowHeight = fresh.WindowHeight;
        this.BackgroundColor = fresh.BackgroundColor;
        this.LastPersistenceError = fresh.LastPersistenceError;
        return string.IsNullOrEmpty(this.LastPersistenceError);
    }

    /// <summary>
    /// Clears the last recorded persistence error after it has been shown to the user.
    /// </summary>
    public void ClearLastPersistenceError()
    {
        this.LastPersistenceError = string.Empty;
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or JsonException)
        {
            Trace.TraceError($"AeroVim: Failed to load settings: {ex}");
            return new AppSettings
            {
                LastPersistenceError = ex.Message,
            };
        }

        return new AppSettings();
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!Equals(field, value))
        {
            field = value;
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
