// <copyright file="Methods.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.NativeInterop
{
    using System.Text;

    /// <summary>
    /// Native Windows API interop methods for keyboard input.
    /// </summary>
    public static class Methods
    {
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
