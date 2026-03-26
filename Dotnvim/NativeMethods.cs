// <copyright file="NativeMethods.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// Windows native API declarations for keyboard input.
    /// </summary>
    [SuppressMessage("StyleCop", "SA1600", Justification = "Windows API declarations")]
    [SuppressMessage("StyleCop", "SA1602", Justification = "Windows API declarations")]
    [SuppressMessage("StyleCop", "SA1310", Justification = "Windows API constant names")]
    [SuppressMessage("StyleCop", "SA1201", Justification = "Grouping Windows API declarations")]
    internal static class NativeMethods
    {
        internal const int VK_SHIFT = 0x10;
        internal const int VK_CONTROL = 0x11;
        internal const int VK_MENU = 0x12;

        internal const uint MAPVK_VK_TO_VSC = 0;

        [DllImport("user32.dll")]
        internal static extern int ToUnicode(
            uint wVirtKey,
            uint wScanCode,
            byte[] lpKeyState,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff,
            int cchBuff,
            uint wFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        internal static extern uint MapVirtualKey(uint uCode, uint uMapType);
    }
}
