// <copyright file="KeyMapping.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Utilities;

using Avalonia.Input;

/// <summary>
/// Define the key mapping from an Avalonia key to vim input.
/// </summary>
public static class KeyMapping
{
    private static readonly IReadOnlyDictionary<Key, string> SpecialKeys = new Dictionary<Key, string>()
    {
        { Key.Back, "BS" },
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
    public static bool TryMap(Avalonia.Input.KeyEventArgs e, out string? text)
    {
        bool control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        if (SpecialKeys.TryGetValue(e.Key, out text))
        {
            text = DecorateInput(text, control, shift, alt);
            return true;
        }

        // Without Ctrl/Alt modifiers, let TextInput handle character input
        if (!control && !alt)
        {
            text = null;
            return false;
        }

        string? baseChar = KeyToBaseCharacter(e.Key);
        if (baseChar is null)
        {
            text = null;
            return false;
        }

        if (baseChar == "<")
        {
            text = DecorateInput("lt", control, shift, alt);
            return true;
        }

        if (baseChar == "\\")
        {
            text = DecorateInput("Bslash", control, shift, alt);
            return true;
        }

        text = DecorateInput(baseChar, control, shift, alt);
        return true;
    }

    private static string DecorateInput(string input, bool control, bool shift, bool alt)
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

        return $"{output}{input}>";
    }

    private static string? KeyToBaseCharacter(Key key)
    {
        if (key >= Key.A && key <= Key.Z)
        {
            return ((char)('a' + (key - Key.A))).ToString();
        }

        if (key >= Key.D0 && key <= Key.D9)
        {
            return ((char)('0' + (key - Key.D0))).ToString();
        }

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            return ((char)('0' + (key - Key.NumPad0))).ToString();
        }

        return key switch
        {
            Key.Multiply => "*",
            Key.Add => "+",
            Key.Subtract => "-",
            Key.Decimal => ".",
            Key.Divide => "/",
            Key.OemSemicolon => ";",
            Key.OemPlus => "=",
            Key.OemComma => ",",
            Key.OemMinus => "-",
            Key.OemPeriod => ".",
            Key.OemQuestion => "/",
            Key.OemTilde => "`",
            Key.OemOpenBrackets => "[",
            Key.OemPipe => "\\",
            Key.OemBackslash => "\\",
            Key.OemCloseBrackets => "]",
            Key.OemQuotes => "'",
            _ => null,
        };
    }
}
