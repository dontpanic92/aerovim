// <copyright file="ShellIntegrationService.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Services;

using System.Runtime.InteropServices;
using AeroVim.Diagnostics;
using Microsoft.Win32;

/// <summary>
/// Manages OS shell integration such as Windows Explorer context menu
/// entries and file type associations. All Windows registry writes go
/// to <c>HKCU</c> (per-user, no elevation required).
/// </summary>
internal static class ShellIntegrationService
{
    /// <summary>
    /// The default set of file extensions offered for association.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultExtensions =
    [
        ".txt", ".md", ".json", ".yaml", ".yml", ".toml",
        ".xml", ".html", ".css", ".js", ".ts", ".jsx", ".tsx",
        ".py", ".cs", ".c", ".cpp", ".h", ".hpp",
        ".sh", ".bash", ".lua", ".vim", ".log",
        ".ini", ".cfg", ".conf", ".env",
        ".rs", ".go", ".java", ".rb", ".php",
        ".sql", ".csv", ".diff", ".patch",
    ];

    private const string ContextMenuVerb = "AeroVim";
    private const string ProgId = "AeroVim.Editor";

    /// <summary>
    /// Gets a value indicating whether the current platform supports
    /// shell integration (Windows only).
    /// </summary>
    public static bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>
    /// Checks whether the "Open with AeroVim" context menu entry is
    /// currently registered in the Windows registry.
    /// </summary>
    /// <returns><c>true</c> if the registry key exists.</returns>
    public static bool IsContextMenuRegistered()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return IsContextMenuRegisteredCore();
    }

    /// <summary>
    /// Checks whether a single file extension is associated with AeroVim.
    /// </summary>
    /// <param name="extension">The extension including the leading dot, e.g. ".txt".</param>
    /// <returns><c>true</c> if the extension has the AeroVim ProgId registered.</returns>
    public static bool IsExtensionRegistered(string extension)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return IsExtensionRegisteredCore(extension);
    }

    /// <summary>
    /// Registers or unregisters the "Open with AeroVim" Explorer context
    /// menu entry based on <paramref name="enabled"/>.
    /// </summary>
    /// <param name="enabled">Whether the context menu should be registered.</param>
    public static void SetContextMenuRegistration(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            if (enabled)
            {
                RegisterContextMenu();
            }
            else
            {
                UnregisterContextMenu();
            }
        }
        catch (Exception ex)
        {
            AppLogger.For(nameof(ShellIntegrationService)).Error(
                $"Failed to {(enabled ? "register" : "unregister")} context menu.", ex);
        }
    }

    /// <summary>
    /// Registers or unregisters a single file extension association.
    /// The shared ProgId is created on first register and removed when
    /// no extensions reference it.
    /// </summary>
    /// <param name="extension">The extension including the leading dot.</param>
    /// <param name="enabled">Whether the association should be registered.</param>
    public static void SetExtensionRegistration(string extension, bool enabled)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            if (enabled)
            {
                EnsureProgId();
                RegisterExtension(extension);
            }
            else
            {
                UnregisterExtension(extension);
            }

            NotifyShellAssociationsChanged();
        }
        catch (Exception ex)
        {
            AppLogger.For(nameof(ShellIntegrationService)).Error(
                $"Failed to {(enabled ? "register" : "unregister")} extension '{extension}'.", ex);
        }
    }

    /// <summary>
    /// Registers all specified extensions at once.
    /// </summary>
    /// <param name="extensions">The extensions to register.</param>
    public static void RegisterAllExtensions(IEnumerable<string> extensions)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            EnsureProgId();
            foreach (var ext in extensions)
            {
                RegisterExtension(ext);
            }

            NotifyShellAssociationsChanged();
        }
        catch (Exception ex)
        {
            AppLogger.For(nameof(ShellIntegrationService)).Error(
                "Failed to register file associations.", ex);
        }
    }

    /// <summary>
    /// Unregisters all specified extensions at once and removes the ProgId.
    /// </summary>
    /// <param name="extensions">The extensions to unregister.</param>
    public static void UnregisterAllExtensions(IEnumerable<string> extensions)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            foreach (var ext in extensions)
            {
                UnregisterExtension(ext);
            }

            RemoveProgIdIfUnused(extensions);
            NotifyShellAssociationsChanged();
        }
        catch (Exception ex)
        {
            AppLogger.For(nameof(ShellIntegrationService)).Error(
                "Failed to unregister file associations.", ex);
        }
    }

    /// <summary>
    /// Normalises an extension string: trims whitespace and ensures a
    /// leading dot. Returns <c>null</c> if the input is empty or invalid.
    /// </summary>
    /// <param name="input">Raw extension text, e.g. "txt" or ".txt".</param>
    /// <returns>Normalised extension like ".txt", or <c>null</c>.</returns>
    public static string? NormaliseExtension(string input)
    {
        var trimmed = input.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (!trimmed.StartsWith('.'))
        {
            trimmed = "." + trimmed;
        }

        // Reject strings that are just a dot or contain spaces/slashes.
        if (trimmed.Length < 2 || trimmed.Contains(' ') || trimmed.Contains('/') || trimmed.Contains('\\'))
        {
            return null;
        }

        return trimmed;
    }

    private static string GetExePath()
    {
        return Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "aerovim.exe");
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool IsContextMenuRegisteredCore()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @$"Software\Classes\*\shell\{ContextMenuVerb}\command");
        return key is not null;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool IsExtensionRegisteredCore(string extension)
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @$"Software\Classes\{extension}\OpenWithProgids");
        if (key is null)
        {
            return false;
        }

        return key.GetValue(ProgId) is not null;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void RegisterContextMenu()
    {
        string exePath = GetExePath();

        using var key = Registry.CurrentUser.CreateSubKey(
            @$"Software\Classes\*\shell\{ContextMenuVerb}");
        key.SetValue(string.Empty, "Open with AeroVim");
        key.SetValue("Icon", $"\"{exePath}\",0");

        using var cmdKey = key.CreateSubKey("command");
        cmdKey.SetValue(string.Empty, $"\"{exePath}\" \"%1\"");
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void UnregisterContextMenu()
    {
        Registry.CurrentUser.DeleteSubKeyTree(
            @$"Software\Classes\*\shell\{ContextMenuVerb}", throwOnMissingSubKey: false);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void EnsureProgId()
    {
        string exePath = GetExePath();

        using var progIdKey = Registry.CurrentUser.CreateSubKey(
            @$"Software\Classes\{ProgId}");
        progIdKey.SetValue(string.Empty, "AeroVim Editor File");

        using var iconKey = progIdKey.CreateSubKey("DefaultIcon");
        iconKey.SetValue(string.Empty, $"\"{exePath}\",0");

        using var cmdKey = progIdKey.CreateSubKey(@"shell\open\command");
        cmdKey.SetValue(string.Empty, $"\"{exePath}\" \"%1\"");
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void RegisterExtension(string extension)
    {
        using var extKey = Registry.CurrentUser.CreateSubKey(
            @$"Software\Classes\{extension}\OpenWithProgids");
        extKey.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void UnregisterExtension(string extension)
    {
        try
        {
            using var extKey = Registry.CurrentUser.OpenSubKey(
                @$"Software\Classes\{extension}\OpenWithProgids", writable: true);
            extKey?.DeleteValue(ProgId, throwOnMissingValue: false);
        }
        catch (Exception)
        {
            // Best effort.
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void RemoveProgIdIfUnused(IEnumerable<string> extensions)
    {
        // Only remove the ProgId if none of the known extensions reference it.
        bool anyStillRegistered = extensions.Any(ext => IsExtensionRegisteredCore(ext));
        if (!anyStillRegistered)
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                @$"Software\Classes\{ProgId}", throwOnMissingSubKey: false);
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void NotifyShellAssociationsChanged()
    {
        SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
