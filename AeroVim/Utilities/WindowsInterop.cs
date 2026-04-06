// <copyright file="WindowsInterop.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Utilities;

using System.Runtime.InteropServices;
using AeroVim.Diagnostics;

/// <summary>
/// Provides native Windows interop helpers for preserving blur/acrylic/mica
/// effects when the main window is deactivated by a child dialog.
/// </summary>
public static class WindowsInterop
{
    private const uint WmNcactivate = 0x0086;
    private const uint DwmwaSystembackdropType = 38;
    private static readonly IntPtr SubclassId = (IntPtr)1;

    private static SubclassCallbackDelegate? subclassProcInstance;
    private static bool isPreservingBlur;
    private static int storedBackdropType;

    /// <summary>
    /// Delegate matching the Win32 <c>SUBCLASSPROC</c> callback signature.
    /// </summary>
    /// <param name="hWnd">Window handle.</param>
    /// <param name="uMsg">Message identifier.</param>
    /// <param name="wParam">First message parameter.</param>
    /// <param name="lParam">Second message parameter.</param>
    /// <param name="uIdSubclass">Subclass identifier.</param>
    /// <param name="dwRefData">Reference data.</param>
    /// <returns>Message result.</returns>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr SubclassCallbackDelegate(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam,
        IntPtr uIdSubclass,
        IntPtr dwRefData);

    /// <summary>
    /// Installs a WndProc subclass on the specified window that intercepts
    /// <c>WM_NCACTIVATE</c> deactivation messages, preventing the non-client
    /// area from being drawn in the inactive state. This keeps the window's
    /// title bar and DWM backdrop looking active while a child dialog is open.
    /// </summary>
    /// <param name="hwnd">The main window's native HWND.</param>
    /// <param name="dwmBackdropType">
    /// The DWM <c>DWMWA_SYSTEMBACKDROP_TYPE</c> value to re-apply on
    /// deactivation (e.g., 2 for Mica, 3 for Acrylic). Pass 0 to skip
    /// DWM attribute re-application.
    /// </param>
    public static void EnableBlurPreservation(IntPtr hwnd, int dwmBackdropType)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || hwnd == IntPtr.Zero)
        {
            return;
        }

        if (isPreservingBlur)
        {
            return;
        }

        storedBackdropType = dwmBackdropType;
        subclassProcInstance = BlurPreservationSubclassProc;

        if (NativeMethods.SetWindowSubclass(hwnd, subclassProcInstance, SubclassId, IntPtr.Zero))
        {
            isPreservingBlur = true;
        }
        else
        {
            AppLogger.Instance.Warning("WindowsInterop", "Failed to install WM_NCACTIVATE subclass for blur preservation.");
            subclassProcInstance = null;
        }
    }

    /// <summary>
    /// Updates the DWM backdrop type used by the blur preservation subclass
    /// and immediately applies the new value via <c>DwmSetWindowAttribute</c>.
    /// Call this when the user changes the blur mode while the settings dialog
    /// is open so that (a) the subclass re-applies the correct backdrop on
    /// future <c>WM_NCACTIVATE</c> messages and (b) the window's current
    /// backdrop effect updates in real time for live preview.
    /// </summary>
    /// <param name="hwnd">The main window's native HWND.</param>
    /// <param name="dwmBackdropType">
    /// The new DWM <c>DWMWA_SYSTEMBACKDROP_TYPE</c> value (e.g., 2 for Mica,
    /// 3 for Acrylic, 0 for auto/none).
    /// </param>
    public static void UpdateStoredBackdropType(IntPtr hwnd, int dwmBackdropType)
    {
        if (!isPreservingBlur)
        {
            return;
        }

        storedBackdropType = dwmBackdropType;

        // Apply the new DWM backdrop type immediately so the effect
        // updates in real time during live preview. Without this, the
        // previously-applied attribute (e.g. Mica) stays in effect
        // until the next WM_NCACTIVATE.
        if (hwnd != IntPtr.Zero)
        {
            int backdropType = dwmBackdropType;
            NativeMethods.DwmSetWindowAttribute(
                hwnd,
                DwmwaSystembackdropType,
                ref backdropType,
                sizeof(int));
        }
    }

    /// <summary>
    /// Removes the WndProc subclass installed by <see cref="EnableBlurPreservation"/>,
    /// restoring default <c>WM_NCACTIVATE</c> handling. Also resets the DWM
    /// <c>DWMWA_SYSTEMBACKDROP_TYPE</c> attribute to <c>DWMSBT_AUTO</c> so
    /// Avalonia can regain control of the backdrop effect.
    /// </summary>
    /// <param name="hwnd">The main window's native HWND.</param>
    public static void DisableBlurPreservation(IntPtr hwnd)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || hwnd == IntPtr.Zero)
        {
            return;
        }

        if (!isPreservingBlur || subclassProcInstance is null)
        {
            return;
        }

        NativeMethods.RemoveWindowSubclass(hwnd, subclassProcInstance, SubclassId);
        isPreservingBlur = false;
        subclassProcInstance = null;

        // Reset the DWM backdrop attribute to DWMSBT_AUTO (0) so the
        // manually-applied value from the subclass proc doesn't persist
        // and conflict with whatever Avalonia sets next.
        int auto = 0;
        NativeMethods.DwmSetWindowAttribute(
            hwnd,
            DwmwaSystembackdropType,
            ref auto,
            sizeof(int));

        storedBackdropType = 0;
    }

    /// <summary>
    /// Subclass callback that intercepts <c>WM_NCACTIVATE</c> deactivation.
    /// When <c>wParam</c> is zero (inactive), returns <c>TRUE</c> to prevent
    /// the window from painting its non-client area in the inactive style.
    /// </summary>
    private static IntPtr BlurPreservationSubclassProc(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam,
        IntPtr uIdSubclass,
        IntPtr dwRefData)
    {
        if (uMsg == WmNcactivate && wParam == IntPtr.Zero)
        {
            // Re-apply DWM backdrop type to help maintain the effect.
            if (storedBackdropType > 0)
            {
                int backdropType = storedBackdropType;
                NativeMethods.DwmSetWindowAttribute(
                    hWnd,
                    DwmwaSystembackdropType,
                    ref backdropType,
                    sizeof(int));
            }

            // Return TRUE to tell Windows we handled the deactivation
            // without repainting the non-client area in inactive style.
            return (IntPtr)1;
        }

        return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    /// <summary>
    /// Contains P/Invoke declarations for Windows APIs used by blur preservation.
    /// </summary>
    private static class NativeMethods
    {
        /// <summary>
        /// Installs or updates a window subclass callback.
        /// </summary>
        /// <param name="hWnd">The window handle.</param>
        /// <param name="pfnSubclass">The subclass callback function.</param>
        /// <param name="uIdSubclass">The subclass identifier.</param>
        /// <param name="dwRefData">Reference data passed to the callback.</param>
        /// <returns><c>true</c> if the subclass was successfully installed.</returns>
        [DllImport("comctl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowSubclass(
            IntPtr hWnd,
            SubclassCallbackDelegate pfnSubclass,
            IntPtr uIdSubclass,
            IntPtr dwRefData);

        /// <summary>
        /// Removes a window subclass callback.
        /// </summary>
        /// <param name="hWnd">The window handle.</param>
        /// <param name="pfnSubclass">The subclass callback to remove.</param>
        /// <param name="uIdSubclass">The subclass identifier.</param>
        /// <returns><c>true</c> if the subclass was successfully removed.</returns>
        [DllImport("comctl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RemoveWindowSubclass(
            IntPtr hWnd,
            SubclassCallbackDelegate pfnSubclass,
            IntPtr uIdSubclass);

        /// <summary>
        /// Calls the next handler in the subclass chain.
        /// </summary>
        /// <param name="hWnd">The window handle.</param>
        /// <param name="uMsg">The message identifier.</param>
        /// <param name="wParam">First message parameter.</param>
        /// <param name="lParam">Second message parameter.</param>
        /// <returns>The message result from the next handler.</returns>
        [DllImport("comctl32.dll")]
        public static extern IntPtr DefSubclassProc(
            IntPtr hWnd,
            uint uMsg,
            IntPtr wParam,
            IntPtr lParam);

        /// <summary>
        /// Sets the value of Desktop Window Manager (DWM) attributes.
        /// </summary>
        /// <param name="hwnd">The window handle.</param>
        /// <param name="dwAttribute">The DWM attribute to set.</param>
        /// <param name="pvAttribute">Reference to the attribute value.</param>
        /// <param name="cbAttribute">Size of the attribute value in bytes.</param>
        /// <returns>Zero on success, or an HRESULT error code.</returns>
        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            uint dwAttribute,
            ref int pvAttribute,
            int cbAttribute);
    }
}
