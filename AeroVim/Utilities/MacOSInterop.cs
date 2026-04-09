// <copyright file="MacOSInterop.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Utilities;

using System.Runtime.InteropServices;

/// <summary>
/// Provides native macOS interop helpers for configuring NSWindow properties
/// that Avalonia does not expose through its public API.
/// </summary>
public static class MacOSInterop
{
    /// <summary>
    /// Configures the NSWindow for a fully transparent background while
    /// preserving native traffic light buttons. Sets the window as non-opaque
    /// with a clear background color, makes the titlebar transparent with a
    /// hidden title, removes the titlebar separator, and ensures the window
    /// shadow is preserved.
    /// </summary>
    /// <param name="nsWindow">The NSWindow handle.</param>
    public static void SetTransparentTitlebar(IntPtr nsWindow)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || nsWindow == IntPtr.Zero)
        {
            return;
        }

        // Make the window non-opaque so macOS composites transparency.
        // [window setOpaque:NO]
        NativeMethods.ObjCMsgSendBool(nsWindow, NativeMethods.SelRegisterName("setOpaque:"), false);

        // Set the native background to clear so the titlebar area
        // participates in the window's transparency level.
        // [window setBackgroundColor:[NSColor clearColor]]
        IntPtr nsColorClass = NativeMethods.ObjCGetClass("NSColor");
        IntPtr clearColor = NativeMethods.ObjCMsgSend(nsColorClass, NativeMethods.SelRegisterName("clearColor"));
        NativeMethods.ObjCMsgSendIntPtr(nsWindow, NativeMethods.SelRegisterName("setBackgroundColor:"), clearColor);

        // [window setTitlebarSeparatorStyle:NSTitlebarSeparatorStyleNone] (0)
        NativeMethods.ObjCMsgSendLong(nsWindow, NativeMethods.SelRegisterName("setTitlebarSeparatorStyle:"), 0);

        // [window setTitlebarAppearsTransparent:YES]
        NativeMethods.ObjCMsgSendBool(nsWindow, NativeMethods.SelRegisterName("setTitlebarAppearsTransparent:"), true);

        // [window setTitleVisibility:NSWindowTitleHidden] (1)
        NativeMethods.ObjCMsgSendLong(nsWindow, NativeMethods.SelRegisterName("setTitleVisibility:"), 1);

        // Ensure native traffic light buttons are visible since we use
        // a custom window template (Avalonia won't manage them for us).
        ShowTrafficLightButtons(nsWindow);
    }

    /// <summary>
    /// Configures the NSWindow for macOS full screen mode so the native
    /// titlebar auto-shows when the user moves the mouse to the top of the
    /// screen. Restores the window to opaque, makes the titlebar visible
    /// and non-transparent so that the system reveals a usable titlebar
    /// alongside the menu bar.
    /// </summary>
    /// <param name="nsWindow">The NSWindow handle.</param>
    public static void ConfigureForFullScreen(IntPtr nsWindow)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || nsWindow == IntPtr.Zero)
        {
            return;
        }

        // Restore opaque window for full screen (content is fully opaque).
        // [window setOpaque:YES]
        NativeMethods.ObjCMsgSendBool(nsWindow, NativeMethods.SelRegisterName("setOpaque:"), true);

        // [window setTitlebarAppearsTransparent:NO]
        NativeMethods.ObjCMsgSendBool(nsWindow, NativeMethods.SelRegisterName("setTitlebarAppearsTransparent:"), false);

        // [window setTitleVisibility:NSWindowTitleVisible] (0)
        NativeMethods.ObjCMsgSendLong(nsWindow, NativeMethods.SelRegisterName("setTitleVisibility:"), 0);

        // [window setTitlebarSeparatorStyle:NSTitlebarSeparatorStyleAutomatic] (1)
        NativeMethods.ObjCMsgSendLong(nsWindow, NativeMethods.SelRegisterName("setTitlebarSeparatorStyle:"), 1);

        ShowTrafficLightButtons(nsWindow);
    }

    /// <summary>
    /// Controls the visibility of Avalonia's internal titlebar material view
    /// and the <c>NSWindowStyleMaskTexturedBackground</c> style mask flag.
    /// <para>
    /// In Avalonia 12 with <c>ExtendClientAreaToDecorationsHint="True"</c>
    /// and <c>WindowDecorations="Full"</c>, the native layer adds
    /// <c>NSWindowStyleMaskTexturedBackground</c> to the style mask and calls
    /// <c>ShowTitleBar:YES</c> on the <c>AutoFitContentView</c>, inserting an
    /// <c>NSVisualEffectView</c> with <c>NSVisualEffectMaterialTitlebar</c>.
    /// Both of these contribute an opaque titlebar background that breaks
    /// <c>WindowTransparencyLevel.Transparent</c>.
    /// </para>
    /// <para>
    /// Calling this with <paramref name="hidden"/> = <c>true</c> replicates
    /// the Avalonia 11 <c>NoChrome</c> behavior: the textured-background
    /// style flag is stripped and the titlebar material view is hidden,
    /// resulting in a fully transparent titlebar while keeping native
    /// traffic light buttons.
    /// </para>
    /// </summary>
    /// <param name="nsWindow">The NSWindow handle.</param>
    /// <param name="hidden">
    /// <c>true</c> to hide the titlebar material (Transparent mode);
    /// <c>false</c> to restore it (Acrylic / no blur).
    /// </param>
    public static void SetTitleBarMaterialHidden(IntPtr nsWindow, bool hidden)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || nsWindow == IntPtr.Zero)
        {
            return;
        }

        // NSWindowStyleMaskTexturedBackground = 1 << 8
        const long texturedBackgroundMask = 1 << 8;
        long currentMask = (long)(nint)NativeMethods.ObjCMsgSend(
            nsWindow,
            NativeMethods.SelRegisterName("styleMask"));

        if (hidden)
        {
            NativeMethods.ObjCMsgSendLong(
                nsWindow,
                NativeMethods.SelRegisterName("setStyleMask:"),
                currentMask & ~texturedBackgroundMask);
        }
        else if ((currentMask & texturedBackgroundMask) == 0)
        {
            NativeMethods.ObjCMsgSendLong(
                nsWindow,
                NativeMethods.SelRegisterName("setStyleMask:"),
                currentMask | texturedBackgroundMask);
        }

        // Call Avalonia's ShowTitleBar: on AutoFitContentView (the
        // window's contentView) to hide/show the _titleBarMaterial
        // NSVisualEffectView and _titleBarUnderline NSBox.
        IntPtr contentView = NativeMethods.ObjCMsgSend(
            nsWindow,
            NativeMethods.SelRegisterName("contentView"));
        if (contentView != IntPtr.Zero)
        {
            NativeMethods.ObjCMsgSendBool(
                contentView,
                NativeMethods.SelRegisterName("ShowTitleBar:"),
                !hidden);
        }

        ShowTrafficLightButtons(nsWindow);
    }

    /// <summary>
    /// Forces the window's blur effect to render in its active (vibrant)
    /// state regardless of window activation. Call before showing a child
    /// dialog so that the main window's blur stays fully active while
    /// focus is on the dialog.
    /// </summary>
    /// <param name="nsWindow">The NSWindow handle.</param>
    public static void ForceBlurActive(IntPtr nsWindow)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || nsWindow == IntPtr.Zero)
        {
            return;
        }

        // NSVisualEffectStateActive = 1
        SetVisualEffectViewState(nsWindow, 1);
    }

    /// <summary>
    /// Resets the window's blur effect to follow the window's active state,
    /// restoring the default macOS behavior. Call after a child dialog closes.
    /// </summary>
    /// <param name="nsWindow">The NSWindow handle.</param>
    public static void ResetBlurState(IntPtr nsWindow)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || nsWindow == IntPtr.Zero)
        {
            return;
        }

        // NSVisualEffectStateFollowsActiveState = 0
        SetVisualEffectViewState(nsWindow, 0);
    }

    /// <summary>
    /// Walks the NSView hierarchy from the window's content view and sets
    /// the <c>state</c> property on every <c>NSVisualEffectView</c> found.
    /// </summary>
    /// <param name="nsWindow">The NSWindow handle.</param>
    /// <param name="state">
    /// 0 = followsWindowActiveState, 1 = active, 2 = inactive.
    /// </param>
    private static void SetVisualEffectViewState(IntPtr nsWindow, long state)
    {
        IntPtr contentView = NativeMethods.ObjCMsgSend(
            nsWindow,
            NativeMethods.SelRegisterName("contentView"));
        if (contentView == IntPtr.Zero)
        {
            return;
        }

        IntPtr effectViewClass = NativeMethods.ObjCGetClass("NSVisualEffectView");
        if (effectViewClass == IntPtr.Zero)
        {
            return;
        }

        // Cache selectors used during the recursive walk.
        IntPtr isKindOfClassSel = NativeMethods.SelRegisterName("isKindOfClass:");
        IntPtr setStateSel = NativeMethods.SelRegisterName("setState:");
        IntPtr subviewsSel = NativeMethods.SelRegisterName("subviews");
        IntPtr countSel = NativeMethods.SelRegisterName("count");
        IntPtr objectAtIndexSel = NativeMethods.SelRegisterName("objectAtIndex:");

        ApplyVisualEffectState(
            contentView,
            effectViewClass,
            isKindOfClassSel,
            setStateSel,
            subviewsSel,
            countSel,
            objectAtIndexSel,
            state);
    }

    /// <summary>
    /// Recursively walks the NSView tree starting from <paramref name="view"/>
    /// and sets the <c>state</c> property on any <c>NSVisualEffectView</c>.
    /// </summary>
    /// <param name="view">The current NSView to inspect.</param>
    /// <param name="effectViewClass">The NSVisualEffectView class pointer.</param>
    /// <param name="isKindOfClassSel">Cached <c>isKindOfClass:</c> selector.</param>
    /// <param name="setStateSel">Cached <c>setState:</c> selector.</param>
    /// <param name="subviewsSel">Cached <c>subviews</c> selector.</param>
    /// <param name="countSel">Cached <c>count</c> selector.</param>
    /// <param name="objectAtIndexSel">Cached <c>objectAtIndex:</c> selector.</param>
    /// <param name="state">The visual effect state value to apply.</param>
    private static void ApplyVisualEffectState(
        IntPtr view,
        IntPtr effectViewClass,
        IntPtr isKindOfClassSel,
        IntPtr setStateSel,
        IntPtr subviewsSel,
        IntPtr countSel,
        IntPtr objectAtIndexSel,
        long state)
    {
        if (NativeMethods.ObjCMsgSendPtrRetBool(view, isKindOfClassSel, effectViewClass))
        {
            NativeMethods.ObjCMsgSendLong(view, setStateSel, state);
        }

        IntPtr subviews = NativeMethods.ObjCMsgSend(view, subviewsSel);
        if (subviews == IntPtr.Zero)
        {
            return;
        }

        long count = (long)(nint)NativeMethods.ObjCMsgSend(subviews, countSel);
        for (long i = 0; i < count; i++)
        {
            IntPtr subview = NativeMethods.ObjCMsgSendLongRetPtr(subviews, objectAtIndexSel, i);
            if (subview != IntPtr.Zero)
            {
                ApplyVisualEffectState(
                    subview,
                    effectViewClass,
                    isKindOfClassSel,
                    setStateSel,
                    subviewsSel,
                    countSel,
                    objectAtIndexSel,
                    state);
            }
        }
    }

    /// <summary>
    /// Ensures the native macOS traffic light buttons (close, miniaturize,
    /// zoom) are visible. Called when using NoChrome so Avalonia does not
    /// manage them, but the NSWindow still owns the standard button instances.
    /// </summary>
    /// <param name="nsWindow">The NSWindow handle.</param>
    private static void ShowTrafficLightButtons(IntPtr nsWindow)
    {
        IntPtr standardWindowButtonSel = NativeMethods.SelRegisterName("standardWindowButton:");
        IntPtr setHiddenSel = NativeMethods.SelRegisterName("setHidden:");

        // NSWindowCloseButton = 0, NSWindowMiniaturizeButton = 1, NSWindowZoomButton = 2
        for (long buttonType = 0; buttonType <= 2; buttonType++)
        {
            IntPtr button = NativeMethods.ObjCMsgSendLongRetPtr(nsWindow, standardWindowButtonSel, buttonType);
            if (button != IntPtr.Zero)
            {
                NativeMethods.ObjCMsgSendBool(button, setHiddenSel, false);
            }
        }
    }

    /// <summary>
    /// Contains P/Invoke declarations for the Objective-C runtime.
    /// </summary>
    private static class NativeMethods
    {
        /// <summary>
        /// Returns a pointer to the class definition identified by name.
        /// </summary>
        /// <param name="name">The class name.</param>
        /// <returns>The class pointer.</returns>
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass")]
        public static extern IntPtr ObjCGetClass(string name);

        /// <summary>
        /// Registers a selector with the Objective-C runtime.
        /// </summary>
        /// <param name="name">The selector name.</param>
        /// <returns>The selector pointer.</returns>
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
        public static extern IntPtr SelRegisterName(string name);

        /// <summary>
        /// Sends a message with no arguments to an Objective-C object and returns a pointer.
        /// </summary>
        /// <param name="receiver">The target object.</param>
        /// <param name="selector">The selector to invoke.</param>
        /// <returns>The return value as a pointer.</returns>
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        public static extern IntPtr ObjCMsgSend(IntPtr receiver, IntPtr selector);

        /// <summary>
        /// Sends a message with a pointer argument to an Objective-C object.
        /// </summary>
        /// <param name="receiver">The target object.</param>
        /// <param name="selector">The selector to invoke.</param>
        /// <param name="arg">The pointer argument.</param>
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        public static extern void ObjCMsgSendIntPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

        /// <summary>
        /// Sends a message with a boolean argument to an Objective-C object.
        /// </summary>
        /// <param name="receiver">The target object.</param>
        /// <param name="selector">The selector to invoke.</param>
        /// <param name="arg">The boolean argument.</param>
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        public static extern void ObjCMsgSendBool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool arg);

        /// <summary>
        /// Sends a message with a long integer argument to an Objective-C object.
        /// </summary>
        /// <param name="receiver">The target object.</param>
        /// <param name="selector">The selector to invoke.</param>
        /// <param name="arg">The long integer argument.</param>
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        public static extern void ObjCMsgSendLong(IntPtr receiver, IntPtr selector, long arg);

        /// <summary>
        /// Sends a message with a long integer argument to an Objective-C object
        /// and returns a pointer result.
        /// </summary>
        /// <param name="receiver">The target object.</param>
        /// <param name="selector">The selector to invoke.</param>
        /// <param name="arg">The long integer argument.</param>
        /// <returns>The return value as a pointer.</returns>
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        public static extern IntPtr ObjCMsgSendLongRetPtr(IntPtr receiver, IntPtr selector, long arg);

        /// <summary>
        /// Sends a message with a pointer argument to an Objective-C object
        /// and returns a pointer result.
        /// </summary>
        /// <param name="receiver">The target object.</param>
        /// <param name="selector">The selector to invoke.</param>
        /// <param name="arg">The pointer argument.</param>
        /// <returns>The return value as a pointer.</returns>
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        public static extern IntPtr ObjCMsgSendPtrRetPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

        /// <summary>
        /// Sends a message with a pointer argument to an Objective-C object
        /// and returns a boolean result.
        /// </summary>
        /// <param name="receiver">The target object.</param>
        /// <param name="selector">The selector to invoke.</param>
        /// <param name="arg">The pointer argument.</param>
        /// <returns>The boolean return value.</returns>
        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool ObjCMsgSendPtrRetBool(IntPtr receiver, IntPtr selector, IntPtr arg);
    }
}
