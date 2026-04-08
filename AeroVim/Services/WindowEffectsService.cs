// <copyright file="WindowEffectsService.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Services;

using System.Runtime.InteropServices;
using AeroVim.Controls;
using AeroVim.Diagnostics;
using AeroVim.Settings;
using AeroVim.Utilities;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

/// <summary>
/// Manages all blur, transparency, and opacity behavior for the main window.
/// Subscribes to <see cref="AppSettings.PropertyChanged"/> for live preview
/// of blur-related settings changes.
/// </summary>
internal sealed class WindowEffectsService
{
    private readonly Window window;
    private readonly AppSettings settings;
    private bool isMacFullScreen;
    private bool isDialogOpen;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowEffectsService"/> class.
    /// </summary>
    /// <param name="window">The main window to manage effects for.</param>
    /// <param name="settings">Application settings.</param>
    public WindowEffectsService(Window window, AppSettings settings)
    {
        this.window = window;
        this.settings = settings;

        this.settings.PropertyChanged += this.OnSettingsPropertyChanged;
    }

    /// <summary>
    /// Raised when the background brush needs to be applied to the window
    /// chrome and editor border. The handler receives the computed brush.
    /// </summary>
    public event Action<IBrush>? BackgroundBrushChanged;

    /// <summary>
    /// Raised when the macOS full-screen state changes. The handler receives
    /// <c>true</c> when entering full screen, <c>false</c> when leaving.
    /// </summary>
    public event Action<bool>? MacOSFullScreenChanged;

    /// <summary>
    /// Gets or sets the current background color (RGB integer).
    /// </summary>
    public int CurrentBackgroundColor { get; set; }

    /// <summary>
    /// Gets or sets the editor control reference for opacity updates.
    /// </summary>
    public EditorControl? EditorControl { get; set; }

    /// <summary>
    /// Gets a value indicating whether macOS full-screen mode is active.
    /// </summary>
    public bool IsMacFullScreen => this.isMacFullScreen;

    /// <summary>
    /// Configures initial blur and transparency settings on the window.
    /// </summary>
    public void SetupBlurBehind()
    {
        this.window.TransparencyBackgroundFallback = Brushes.Transparent;
        this.window.Background = Brushes.Transparent;
        this.UpdateTransparencyLevelHint();
        this.UpdateBackgroundOpacity();
    }

    /// <summary>
    /// Defers macOS native transparency setup to
    /// <see cref="DispatcherPriority.Background"/> so that Avalonia finishes
    /// processing the transparency level hint before we override NSWindow
    /// properties. A no-op on non-macOS platforms.
    /// </summary>
    public void DeferMacOSNativeTransparency()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                var nsWindow = this.window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (nsWindow == IntPtr.Zero)
                {
                    AppLogger.For<WindowEffectsService>().Info("macOS platform handle unavailable, skipping native transparency setup.");
                    return;
                }

                MacOSInterop.SetTransparentTitlebar(nsWindow);
            },
            DispatcherPriority.Background);
    }

    /// <summary>
    /// Handles macOS window activation by reapplying transparent titlebar
    /// settings after Avalonia finishes any internal NSWindow style resets.
    /// A no-op on non-macOS platforms.
    /// </summary>
    public async void HandleMacOSActivation()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return;
        }

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            var nsWindow = this.window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (nsWindow == IntPtr.Zero)
            {
                AppLogger.For<WindowEffectsService>().Info("macOS platform handle unavailable in HandleMacOSActivation.");
                return;
            }

            MacOSInterop.SetTransparentTitlebar(nsWindow);
        }
        catch (Exception ex)
        {
            AppLogger.For<WindowEffectsService>().Warning($"HandleMacOSActivation failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Handles macOS full-screen state transitions by configuring native
    /// window properties and updating background opacity.
    /// </summary>
    /// <param name="isFullScreen"><c>true</c> when entering full screen.</param>
    public void HandleMacOSFullScreenTransition(bool isFullScreen)
    {
        var nsWindow = this.window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (nsWindow == IntPtr.Zero)
        {
            AppLogger.For<WindowEffectsService>().Info("macOS platform handle unavailable during WindowState change.");
            return;
        }

        this.isMacFullScreen = isFullScreen;

        if (isFullScreen)
        {
            MacOSInterop.ConfigureForFullScreen(nsWindow);
        }
        else
        {
            MacOSInterop.SetTransparentTitlebar(nsWindow);
        }

        this.UpdateBackgroundOpacity();
        this.MacOSFullScreenChanged?.Invoke(isFullScreen);
    }

    /// <summary>
    /// Activates platform-specific blur preservation so the window's
    /// acrylic/mica/blur effect stays fully active while a child dialog
    /// has focus.
    /// </summary>
    /// <returns>The native handle used for preservation, for passing to <see cref="EndDialogBlurPreservation"/>.</returns>
    public IntPtr BeginDialogBlurPreservation()
    {
        this.isDialogOpen = true;

        if (!this.settings.EnableBlurBehind)
        {
            return IntPtr.Zero;
        }

        IntPtr nativeHandle = this.window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (nativeHandle == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            MacOSInterop.ForceBlurActive(nativeHandle);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            int dwmBackdropType = this.MapBlurTypeToDwmBackdrop();
            WindowsInterop.EnableBlurPreservation(nativeHandle, dwmBackdropType);
        }

        return nativeHandle;
    }

    /// <summary>
    /// Deactivates platform-specific blur preservation, restoring normal
    /// window activation behavior. Then forces a transparency re-negotiation
    /// with the compositor.
    /// </summary>
    /// <param name="nativeHandle">
    /// The native handle returned by <see cref="BeginDialogBlurPreservation"/>.
    /// </param>
    public void EndDialogBlurPreservation(IntPtr nativeHandle)
    {
        this.isDialogOpen = false;

        if (nativeHandle != IntPtr.Zero)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                MacOSInterop.ResetBlurState(nativeHandle);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                WindowsInterop.DisableBlurPreservation(nativeHandle);
            }
        }

        // Force Avalonia to re-negotiate the transparency level with the compositor.
        this.window.TransparencyLevelHint = [WindowTransparencyLevel.None];
        this.SetupBlurBehind();
    }

    /// <summary>
    /// Checks whether the actual transparency level matches the requested
    /// level and shows a mismatch warning dialog if needed.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CheckTransparencyMismatchAsync()
    {
        if (!this.settings.EnableBlurBehind)
        {
            return;
        }

        var requestedLevel = this.GetRequestedTransparencyLevel();

        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        for (int i = 0; i < 5; i++)
        {
            if (this.window.ActualTransparencyLevel == requestedLevel)
            {
                return;
            }

            await Task.Delay(100);
        }

        var actualLevel = this.window.ActualTransparencyLevel;
        if (actualLevel == requestedLevel)
        {
            return;
        }

        var dialog = new Dialogs.MessageWindow(
            $"The requested transparency level {requestedLevel} is not supported on your system. Falling back to {actualLevel}",
            "Transparency Level Fallback");
        await dialog.ShowDialog(this.window);
    }

    /// <summary>
    /// Recalculates and applies background opacity to the editor control
    /// and raises <see cref="BackgroundBrushChanged"/>.
    /// </summary>
    public void UpdateBackgroundOpacity()
    {
        float opacity = this.isMacFullScreen ? 1f :
            this.settings.EnableBlurBehind ? (float)this.settings.BackgroundOpacity : 1f;
        IBrush backgroundBrush = new SolidColorBrush(Helpers.GetAvaloniaColor(this.CurrentBackgroundColor, opacity));

        this.BackgroundBrushChanged?.Invoke(backgroundBrush);

        if (this.EditorControl is not null)
        {
            this.EditorControl.BackgroundAlpha = (byte)(opacity * 255);
            this.EditorControl.InvalidateVisual();
        }
    }

    private WindowTransparencyLevel GetRequestedTransparencyLevel()
    {
        return this.settings.BlurType switch
        {
            BlurType.Gaussian => WindowTransparencyLevel.Blur,
            BlurType.Acrylic => WindowTransparencyLevel.AcrylicBlur,
            BlurType.Mica => WindowTransparencyLevel.Mica,
            BlurType.Transparent => WindowTransparencyLevel.Transparent,
            _ => WindowTransparencyLevel.None,
        };
    }

    private void UpdateTransparencyLevelHint()
    {
        if (this.settings.EnableBlurBehind)
        {
            this.window.TransparencyLevelHint = [this.GetRequestedTransparencyLevel()];
        }
        else
        {
            this.window.TransparencyLevelHint = [WindowTransparencyLevel.None];
        }
    }

    private int MapBlurTypeToDwmBackdrop()
    {
        return this.settings.BlurType switch
        {
            BlurType.Gaussian => 0,
            BlurType.Acrylic => 3, // DWMSBT_TRANSIENTWINDOW
            BlurType.Mica => 2, // DWMSBT_MAINWINDOW
            BlurType.Transparent => 0,
            _ => 0,
        };
    }

    private void UpdateBlurPreservationForCurrentSettings()
    {
        if (!this.isDialogOpen || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        int dwmBackdropType = this.MapBlurTypeToDwmBackdrop();
        IntPtr hwnd = this.window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        WindowsInterop.UpdateStoredBackdropType(hwnd, dwmBackdropType);
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppSettings.EnableBlurBehind):
            case nameof(AppSettings.BlurType):
                Dispatcher.UIThread.Post(() =>
                {
                    this.UpdateBlurPreservationForCurrentSettings();
                    this.SetupBlurBehind();
                    this.DeferMacOSNativeTransparency();
                    if (!this.isDialogOpen)
                    {
                        Dispatcher.UIThread.InvokeAsync(async Task () =>
                            await this.CheckTransparencyMismatchAsync());
                    }
                });
                break;

            case nameof(AppSettings.BackgroundOpacity):
                Dispatcher.UIThread.Post(() => this.UpdateBackgroundOpacity());
                break;
        }
    }
}
