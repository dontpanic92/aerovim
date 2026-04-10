// <copyright file="AppSettings.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Services;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AeroVim.Diagnostics;
using AeroVim.Editor.Utilities;
using AeroVim.Settings;

/// <summary>
/// Application settings with JSON persistence.
/// </summary>
public sealed class AppSettings : INotifyPropertyChanged
{
    private static readonly string DefaultSettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AeroVim");

    private static readonly object DefaultInstanceLock = new();

    private static string? settingsDirectoryOverride;

    private static Lazy<AppSettings> defaultInstance = CreateDefaultInstance();

    private string neovimPath = string.Empty;
    private string vimPath = string.Empty;
    private EditorType editorType = EditorType.Neovim;
    private double backgroundOpacity = 0.75;
    private bool enableLigature = true;
    private bool enableBlurBehind = true;
    private bool enableDragDrop = true;
    private List<string> fileAssociationExtensions = new List<string>();
    private BlurType blurType = BlurType.Acrylic;
    private bool isMaximized;
    private int windowWidth = 800;
    private int windowHeight = 600;
    private int backgroundColor = 0xFFFFFF;
    private List<string> fallbackFonts = new List<string> { FontPriorityList.GuiFontSentinel, FontPriorityList.SystemMonoSentinel };
    private bool enableExternalUI;
    private bool autoCheckForUpdates = true;
    private DateTime? lastUpdateCheckUtc;
    private string? skippedVersion;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the default settings instance. This exists primarily for XAML
    /// designer support and parameterless-constructor fallbacks. Runtime
    /// code should receive <see cref="AppSettings"/> through constructor
    /// injection rather than accessing this property directly.
    /// </summary>
    public static AppSettings Default
    {
        get
        {
            lock (DefaultInstanceLock)
            {
                return defaultInstance.Value;
            }
        }
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
    /// Gets or sets a value indicating whether drag-and-drop file opening is enabled.
    /// </summary>
    public bool EnableDragDrop
    {
        get => this.enableDragDrop;
        set => this.SetField(ref this.enableDragDrop, value);
    }

    /// <summary>
    /// Gets or sets the list of file extensions to associate with AeroVim.
    /// When empty, the default set from <see cref="ShellIntegrationService.DefaultExtensions"/>
    /// is used as the initial value in the Settings UI.
    /// </summary>
    public List<string> FileAssociationExtensions
    {
        get => this.fileAssociationExtensions;
        set => this.SetField(ref this.fileAssociationExtensions, value);
    }

    /// <summary>
    /// Gets or sets the blur type.
    /// </summary>
    public BlurType BlurType
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
    /// Gets or sets the user-configured fallback font list.
    /// These fonts are searched (in order) after the guifont fonts
    /// and before platform defaults when looking for glyph coverage.
    /// </summary>
    public List<string> FallbackFonts
    {
        get => this.fallbackFonts;
        set => this.SetField(ref this.fallbackFonts, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether external UI overlays (floating
    /// command line and completion popup) are enabled for Neovim. When enabled,
    /// Neovim's <c>ext_cmdline</c> and <c>ext_popupmenu</c> protocols are
    /// activated and the command line and completion menu render as elegant
    /// overlays instead of inside the terminal grid.
    /// </summary>
    public bool EnableExternalUI
    {
        get => this.enableExternalUI;
        set => this.SetField(ref this.enableExternalUI, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the application should
    /// automatically check for updates at startup.
    /// </summary>
    public bool AutoCheckForUpdates
    {
        get => this.autoCheckForUpdates;
        set => this.SetField(ref this.autoCheckForUpdates, value);
    }

    /// <summary>
    /// Gets or sets the UTC timestamp of the last successful update check.
    /// </summary>
    public DateTime? LastUpdateCheckUtc
    {
        get => this.lastUpdateCheckUtc;
        set => this.SetField(ref this.lastUpdateCheckUtc, value);
    }

    /// <summary>
    /// Gets or sets the version string the user chose to skip. When set,
    /// the update notification is suppressed for this exact version.
    /// </summary>
    public string? SkippedVersion
    {
        get => this.skippedVersion;
        set => this.SetField(ref this.skippedVersion, value);
    }

    /// <summary>
    /// Save settings to disk.
    /// </summary>
    /// <returns><c>true</c> if the settings were saved successfully; otherwise, <c>false</c>.</returns>
    public bool Save()
    {
        try
        {
            Directory.CreateDirectory(GetSettingsDirectory());
            var json = JsonSerializer.Serialize(this, AppSettingsJsonContext.Default.AppSettings);
            File.WriteAllText(GetSettingsPath(), json);
            this.LastPersistenceError = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or JsonException)
        {
            this.LastPersistenceError = ex.Message;
            AppLogger.For<AppSettings>().Error("Failed to save settings.", ex);
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
        this.EnableDragDrop = fresh.EnableDragDrop;
        this.FileAssociationExtensions = fresh.FileAssociationExtensions;
        this.BlurType = fresh.BlurType;
        this.IsMaximized = fresh.IsMaximized;
        this.WindowWidth = fresh.WindowWidth;
        this.WindowHeight = fresh.WindowHeight;
        this.BackgroundColor = fresh.BackgroundColor;
        this.FallbackFonts = fresh.FallbackFonts;
        this.EnableExternalUI = fresh.EnableExternalUI;
        this.AutoCheckForUpdates = fresh.AutoCheckForUpdates;
        this.LastUpdateCheckUtc = fresh.LastUpdateCheckUtc;
        this.SkippedVersion = fresh.SkippedVersion;
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

    /// <summary>
    /// Redirects settings persistence to a test-specific directory.
    /// </summary>
    /// <param name="settingsDirectory">The override directory, or <c>null</c> to use the default location.</param>
    internal static void SetStorageDirectoryForTesting(string? settingsDirectory)
    {
        lock (DefaultInstanceLock)
        {
            settingsDirectoryOverride = settingsDirectory;
            defaultInstance = CreateDefaultInstance();
        }
    }

    /// <summary>
    /// Clears any test overrides and recreates the default singleton.
    /// </summary>
    internal static void ResetForTesting()
    {
        lock (DefaultInstanceLock)
        {
            settingsDirectoryOverride = null;
            defaultInstance = CreateDefaultInstance();
        }
    }

    /// <summary>
    /// Gets the effective settings path used by the current test configuration.
    /// </summary>
    /// <returns>The effective settings file path.</returns>
    internal static string GetSettingsPathForTesting()
    {
        return GetSettingsPath();
    }

    private static AppSettings Load()
    {
        try
        {
            string settingsPath = GetSettingsPath();
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings) as AppSettings ?? new AppSettings();
                settings.fallbackFonts = FontPriorityList.Normalize(settings.fallbackFonts);
                return settings;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or JsonException)
        {
            AppLogger.For<AppSettings>().Error("Failed to load settings.", ex);
            return new AppSettings
            {
                LastPersistenceError = ex.Message,
            };
        }

        return new AppSettings();
    }

    private static Lazy<AppSettings> CreateDefaultInstance()
    {
        return new Lazy<AppSettings>(Load);
    }

    private static string GetSettingsDirectory()
    {
        return settingsDirectoryOverride ?? DefaultSettingsDirectory;
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(GetSettingsDirectory(), "settings.json");
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
