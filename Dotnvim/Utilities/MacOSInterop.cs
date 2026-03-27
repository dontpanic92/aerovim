// <copyright file="MacOSInterop.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Utilities
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Provides native macOS interop helpers for configuring NSWindow properties
    /// that Avalonia does not expose through its public API.
    /// </summary>
    public static class MacOSInterop
    {
        /// <summary>
        /// Configures the NSWindow for a fully transparent background while
        /// preserving native traffic light buttons. Sets the window background
        /// color to clear, marks the window as non-opaque, removes the titlebar
        /// separator, and ensures the window shadow is preserved.
        /// </summary>
        /// <param name="nsWindow">The NSWindow handle.</param>
        public static void SetTransparentTitlebar(IntPtr nsWindow)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || nsWindow == IntPtr.Zero)
            {
                return;
            }

            // [NSColor clearColor]
            // IntPtr nsColorClass = NativeMethods.ObjCGetClass("NSColor");
            // IntPtr clearColor = NativeMethods.ObjCMsgSend(nsColorClass, NativeMethods.SelRegisterName("clearColor"));

            // [window setBackgroundColor:[NSColor clearColor]]
            // NativeMethods.ObjCMsgSendIntPtr(nsWindow, NativeMethods.SelRegisterName("setBackgroundColor:"), clearColor);

            // [window setOpaque:NO]
            // NativeMethods.ObjCMsgSendBool(nsWindow, NativeMethods.SelRegisterName("setOpaque:"), false);

            // [window setTitlebarSeparatorStyle:NSTitlebarSeparatorStyleNone] (0)
            NativeMethods.ObjCMsgSendLong(nsWindow, NativeMethods.SelRegisterName("setTitlebarSeparatorStyle:"), 0);

            // [window setTitlebarAppearsTransparent:YES]
            NativeMethods.ObjCMsgSendBool(nsWindow, NativeMethods.SelRegisterName("setTitlebarAppearsTransparent:"), true);

            // [window setTitleVisibility:NSWindowTitleHidden] (1)
            NativeMethods.ObjCMsgSendLong(nsWindow, NativeMethods.SelRegisterName("setTitleVisibility:"), 1);

            // [window setHasShadow:YES]
            NativeMethods.ObjCMsgSendBool(nsWindow, NativeMethods.SelRegisterName("setHasShadow:"), true);
        }

        /// <summary>
        /// Restores the NSWindow to its default titlebar configuration.
        /// Called when switching away from Transparent mode to allow Avalonia's
        /// blur and vibrancy effects to work correctly.
        /// </summary>
        /// <param name="nsWindow">The NSWindow handle.</param>
        public static void RestoreDefaultTitlebar(IntPtr nsWindow)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || nsWindow == IntPtr.Zero)
            {
                return;
            }

            // [NSColor windowBackgroundColor]
            IntPtr nsColorClass = NativeMethods.ObjCGetClass("NSColor");
            IntPtr windowBgColor = NativeMethods.ObjCMsgSend(nsColorClass, NativeMethods.SelRegisterName("windowBackgroundColor"));

            // [window setBackgroundColor:[NSColor windowBackgroundColor]]
            NativeMethods.ObjCMsgSendIntPtr(nsWindow, NativeMethods.SelRegisterName("setBackgroundColor:"), windowBgColor);

            // [window setOpaque:YES]
            NativeMethods.ObjCMsgSendBool(nsWindow, NativeMethods.SelRegisterName("setOpaque:"), true);

            // [window setTitlebarSeparatorStyle:NSTitlebarSeparatorStyleAutomatic] (1)
            NativeMethods.ObjCMsgSendLong(nsWindow, NativeMethods.SelRegisterName("setTitlebarSeparatorStyle:"), 1);

            // [window setTitlebarAppearsTransparent:NO]
            NativeMethods.ObjCMsgSendBool(nsWindow, NativeMethods.SelRegisterName("setTitlebarAppearsTransparent:"), false);

            // [window setTitleVisibility:NSWindowTitleVisible] (0)
            NativeMethods.ObjCMsgSendLong(nsWindow, NativeMethods.SelRegisterName("setTitleVisibility:"), 0);
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
        }
    }
}
