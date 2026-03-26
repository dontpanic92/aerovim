// <copyright file="KeyMapping.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim
{
    using System;
    using System.Collections.Generic;
    using Avalonia.Input;

    /// <summary>
    /// Define the key mapping from an Avalonia key to vim input.
    /// </summary>
    public static class KeyMapping
    {
        private static Dictionary<Key, string> specialKeys = new Dictionary<Key, string>()
        {
            { Key.Back, "Bs" },
            { Key.Tab, "Tab" },
            { Key.LineFeed, "NL" },
            { Key.Return, "CR" },
            { Key.Escape, "Esc" },
            { Key.Space, "Space" },
            { Key.OemBackslash, "Bslash" },
            { Key.Delete, "Del" },
            { Key.Up, "Up" },
            { Key.Down, "Down" },
            { Key.Left, "Left" },
            { Key.Right, "Right" },
            { Key.Help, "Help" },
            { Key.Insert, "Insert" },
            { Key.Home, "Home" },
            { Key.End, "End" },
            { Key.PageUp, "PageUp" },
            { Key.PageDown, "PageDown" },
            { Key.F1, "F1" },
            { Key.F2, "F2" },
            { Key.F3, "F3" },
            { Key.F4, "F4" },
            { Key.F5, "F5" },
            { Key.F6, "F6" },
            { Key.F7, "F7" },
            { Key.F8, "F8" },
            { Key.F9, "F9" },
            { Key.F10, "F10" },
            { Key.F11, "F11" },
            { Key.F12, "F12" },
        };

        /// <summary>
        /// Mapping a key to a vim recognizable text.
        /// </summary>
        /// <param name="e">The key event.</param>
        /// <param name="text">Converted text.</param>
        /// <returns>Whether the key has a map.</returns>
        public static bool TryMap(Avalonia.Input.KeyEventArgs e, out string text)
        {
            bool control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            bool alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

            if (specialKeys.TryGetValue(e.Key, out text))
            {
                text = NativeInterop.Methods.DecorateInput(text, control, shift, alt);
                return true;
            }

            if (!TryGetVirtualKey(e.Key, out int virtualKey))
            {
                text = null;
                return false;
            }

            text = NativeInterop.Methods.VirtualKeyToString(virtualKey);
            return text != null;
        }

        private static bool TryGetVirtualKey(Key key, out int virtualKey)
        {
            if (key >= Key.A && key <= Key.Z)
            {
                virtualKey = 'A' + ((int)key - (int)Key.A);
                return true;
            }

            if (key >= Key.D0 && key <= Key.D9)
            {
                virtualKey = '0' + ((int)key - (int)Key.D0);
                return true;
            }

            if (key >= Key.NumPad0 && key <= Key.NumPad9)
            {
                virtualKey = 0x60 + ((int)key - (int)Key.NumPad0);
                return true;
            }

            switch (key)
            {
                case Key.Multiply:
                    virtualKey = 0x6A;
                    return true;
                case Key.Add:
                    virtualKey = 0x6B;
                    return true;
                case Key.Separator:
                    virtualKey = 0x6C;
                    return true;
                case Key.Subtract:
                    virtualKey = 0x6D;
                    return true;
                case Key.Decimal:
                    virtualKey = 0x6E;
                    return true;
                case Key.Divide:
                    virtualKey = 0x6F;
                    return true;
                case Key.OemSemicolon:
                    virtualKey = 0xBA;
                    return true;
                case Key.OemPlus:
                    virtualKey = 0xBB;
                    return true;
                case Key.OemComma:
                    virtualKey = 0xBC;
                    return true;
                case Key.OemMinus:
                    virtualKey = 0xBD;
                    return true;
                case Key.OemPeriod:
                    virtualKey = 0xBE;
                    return true;
                case Key.OemQuestion:
                    virtualKey = 0xBF;
                    return true;
                case Key.OemTilde:
                    virtualKey = 0xC0;
                    return true;
                case Key.OemOpenBrackets:
                    virtualKey = 0xDB;
                    return true;
                case Key.OemPipe:
                case Key.OemBackslash:
                    virtualKey = 0xDC;
                    return true;
                case Key.OemCloseBrackets:
                    virtualKey = 0xDD;
                    return true;
                case Key.OemQuotes:
                    virtualKey = 0xDE;
                    return true;
                default:
                    virtualKey = default;
                    return false;
            }
        }
    }
}
