// <copyright file="Methods.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.NativeInterop
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// Native Windows API interop methods for window management and keyboard input.
    /// </summary>
    public static class Methods
    {
        /// <summary>
        /// Handles the WM_GETMINMAXINFO message to constrain window size to the monitor work area.
        /// </summary>
        /// <param name="hwnd">The window handle.</param>
        /// <param name="lParam">The lParam from the window message containing the MINMAXINFO pointer.</param>
        public static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                var monitorInfo = default(NativeMethods.MONITORINFO);
                monitorInfo.CbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
                NativeMethods.GetMonitorInfo(monitor, ref monitorInfo);

                var minMaxInfo = Marshal.PtrToStructure<NativeMethods.MINMAXINFO>(lParam);
                minMaxInfo.PtMaxPosition.X = Math.Abs(monitorInfo.RcWork.Left - monitorInfo.RcMonitor.Left);
                minMaxInfo.PtMaxPosition.Y = Math.Abs(monitorInfo.RcWork.Top - monitorInfo.RcMonitor.Top);
                minMaxInfo.PtMaxSize.X = Math.Abs(monitorInfo.RcWork.Right - monitorInfo.RcWork.Left);
                minMaxInfo.PtMaxSize.Y = Math.Abs(monitorInfo.RcWork.Bottom - monitorInfo.RcWork.Top);
                Marshal.StructureToPtr(minMaxInfo, lParam, true);
            }
        }

        /// <summary>
        /// Converts a virtual key code to a Vim-compatible input string, including modifier notation.
        /// </summary>
        /// <param name="virtualKey">The virtual key code.</param>
        /// <returns>The Vim-compatible string representation, or null if the key cannot be converted.</returns>
        public static string VirtualKeyToString(int virtualKey)
        {
            byte[] keyboardState = new byte[256];
            NativeMethods.GetKeyboardState(keyboardState);

            bool control = (keyboardState[NativeMethods.VK_CONTROL] & 0x80) != 0;
            bool shift = (keyboardState[NativeMethods.VK_SHIFT] & 0x80) != 0;
            bool alt = (keyboardState[NativeMethods.VK_MENU] & 0x80) != 0;

            keyboardState[NativeMethods.VK_CONTROL] &= 0x7F;
            keyboardState[NativeMethods.VK_SHIFT] &= 0x7F;
            keyboardState[NativeMethods.VK_MENU] &= 0x7F;

            uint scanCode = NativeMethods.MapVirtualKey((uint)virtualKey, NativeMethods.MAPVK_VK_TO_VSC);
            string text = GetUnicode((uint)virtualKey, scanCode, keyboardState);

            if (control)
            {
                keyboardState[NativeMethods.VK_CONTROL] |= 0x80;
                string textWithControl = GetUnicode((uint)virtualKey, scanCode, keyboardState);
                if (!string.IsNullOrEmpty(textWithControl))
                {
                    text = textWithControl;
                    control = false;
                }

                keyboardState[NativeMethods.VK_CONTROL] &= 0x7F;
            }

            if (shift)
            {
                keyboardState[NativeMethods.VK_SHIFT] |= 0x80;
                string textWithShift = GetUnicode((uint)virtualKey, scanCode, keyboardState);
                if (!string.IsNullOrEmpty(textWithShift))
                {
                    text = textWithShift;
                    shift = false;
                }

                keyboardState[NativeMethods.VK_SHIFT] &= 0x7F;
            }

            if (string.Equals(text, "<"))
            {
                text = "lt";
                return DecorateInput(text, control, shift, alt);
            }
            else if (string.Equals(text, "\\"))
            {
                text = "Bslash";
                return DecorateInput(text, control, shift, alt);
            }
            else if ((control || shift || alt) && !string.IsNullOrEmpty(text))
            {
                return DecorateInput(text, control, shift, alt);
            }

            return text;
        }

        /// <summary>
        /// Decorates an input string with Vim modifier notation.
        /// </summary>
        /// <param name="input">The key name or character.</param>
        /// <param name="control">Whether the Control modifier is active.</param>
        /// <param name="shift">Whether the Shift modifier is active.</param>
        /// <param name="alt">Whether the Alt modifier is active.</param>
        /// <returns>The decorated Vim input string.</returns>
        public static string DecorateInput(string input, bool control, bool shift, bool alt)
        {
            string output = "<";

            if (control)
            {
                output += "C-";
            }

            if (shift)
            {
                output += "S-";
            }

            if (alt)
            {
                output += "A-";
            }

            output += input + ">";

            return output;
        }

        /// <summary>
        /// Extends the DWM frame into the client area for custom window chrome.
        /// </summary>
        /// <param name="handle">The window handle.</param>
        /// <param name="dwmBorderSizeX">The horizontal border size in pixels.</param>
        /// <param name="dwmBorderSizeY">The vertical border size in pixels.</param>
        public static void ExtendFrame(IntPtr handle, int dwmBorderSizeX, int dwmBorderSizeY)
        {
            var margins = new NativeMethods.MARGINS
            {
                Left = dwmBorderSizeX,
                Right = dwmBorderSizeX,
                Top = dwmBorderSizeY,
                Bottom = dwmBorderSizeY,
            };

            int val = 2;
            NativeMethods.DwmSetWindowAttribute(handle, 2, ref val, 4);
            NativeMethods.DwmExtendFrameIntoClientArea(handle, ref margins);
        }

        /// <summary>
        /// Performs non-client hit testing for custom window chrome.
        /// </summary>
        /// <param name="handle">The window handle.</param>
        /// <param name="lParam">The lParam from WM_NCHITTEST containing mouse coordinates.</param>
        /// <param name="xBorderWidth">The horizontal border width in pixels.</param>
        /// <param name="yBorderWidth">The vertical border width in pixels.</param>
        /// <param name="titleBarHeight">The title bar height in pixels.</param>
        /// <param name="clientAreaHitTest">A delegate to test if a point is in a custom client area control.</param>
        /// <returns>The hit test result code.</returns>
        public static IntPtr NCHitTest(IntPtr handle, IntPtr lParam, int xBorderWidth, int yBorderWidth, int titleBarHeight, Func<int, int, bool> clientAreaHitTest)
        {
            int x = (short)(lParam.ToInt64() & 0xFFFF);
            int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

            var point = new NativeMethods.POINT { X = x, Y = y };
            NativeMethods.ScreenToClient(handle, ref point);

            if (clientAreaHitTest(point.X, point.Y))
            {
                return (IntPtr)NativeMethods.HTCLIENT;
            }

            var windowInfo = default(NativeMethods.WINDOWINFO);
            windowInfo.CbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.WINDOWINFO));
            NativeMethods.GetWindowInfo(handle, ref windowInfo);
            int height = windowInfo.RcWindow.Bottom - windowInfo.RcWindow.Top;
            int width = windowInfo.RcWindow.Right - windowInfo.RcWindow.Left;

            if (!NativeMethods.IsZoomed(handle))
            {
                if (point.X < xBorderWidth)
                {
                    if (point.Y < yBorderWidth)
                    {
                        return (IntPtr)NativeMethods.HTTOPLEFT;
                    }
                    else if (point.Y > height - yBorderWidth)
                    {
                        return (IntPtr)NativeMethods.HTBOTTOMLEFT;
                    }
                    else
                    {
                        return (IntPtr)NativeMethods.HTLEFT;
                    }
                }
                else if (point.X > width - xBorderWidth)
                {
                    if (point.Y < yBorderWidth)
                    {
                        return (IntPtr)NativeMethods.HTTOPRIGHT;
                    }
                    else if (point.Y > height - yBorderWidth)
                    {
                        return (IntPtr)NativeMethods.HTBOTTOMRIGHT;
                    }
                    else
                    {
                        return (IntPtr)NativeMethods.HTRIGHT;
                    }
                }
                else if (point.Y < yBorderWidth)
                {
                    return (IntPtr)NativeMethods.HTTOP;
                }
                else if (point.Y > height - yBorderWidth)
                {
                    return (IntPtr)NativeMethods.HTBOTTOM;
                }
            }

            if (point.Y < titleBarHeight + yBorderWidth)
            {
                return (IntPtr)NativeMethods.HTCAPTION;
            }

            return (IntPtr)NativeMethods.HTCLIENT;
        }

        private static string GetUnicode(uint virtualKey, uint scanCode, byte[] keyboardState)
        {
            var buffer = new StringBuilder(2);
            int result = NativeMethods.ToUnicode(virtualKey, scanCode, keyboardState, buffer, 2, 0);
            if (result <= 0)
            {
                return null;
            }

            return buffer.ToString(0, result);
        }
    }
}
