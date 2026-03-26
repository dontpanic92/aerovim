// <copyright file="KeyMapping.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim
{
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

            // For non-special keys, try to get the character via Win32 API
            text = NativeInterop.Methods.VirtualKeyToString((int)e.Key);
            return text != null;
        }
    }
}
