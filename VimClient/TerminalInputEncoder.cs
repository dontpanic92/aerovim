// <copyright file="TerminalInputEncoder.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.VimClient;

/// <summary>
/// Converts Vim-notation key sequences to terminal escape sequences for PTY input.
/// </summary>
public static class TerminalInputEncoder
{
    private static readonly Dictionary<string, string> SpecialKeys = new()
    {
        { "Esc", "\x1B" },
        { "CR", "\r" },
        { "Return", "\r" },
        { "Enter", "\r" },
        { "NL", "\n" },
        { "BS", "\x7F" },
        { "Tab", "\t" },
        { "Space", " " },
        { "lt", "<" },
        { "Bslash", "\\" },
        { "Bar", "|" },
        { "Del", "\x1B[3~" },
        { "Up", "\x1B[A" },
        { "Down", "\x1B[B" },
        { "Right", "\x1B[C" },
        { "Left", "\x1B[D" },
        { "Home", "\x1B[H" },
        { "End", "\x1B[F" },
        { "Insert", "\x1B[2~" },
        { "PageUp", "\x1B[5~" },
        { "PageDown", "\x1B[6~" },
        { "F1", "\x1BOP" },
        { "F2", "\x1BOQ" },
        { "F3", "\x1BOR" },
        { "F4", "\x1BOS" },
        { "F5", "\x1B[15~" },
        { "F6", "\x1B[17~" },
        { "F7", "\x1B[18~" },
        { "F8", "\x1B[19~" },
        { "F9", "\x1B[20~" },
        { "F10", "\x1B[21~" },
        { "F11", "\x1B[23~" },
        { "F12", "\x1B[24~" },
    };

    // Arrow/cursor keys use CSI {final} format; modified variants use CSI 1;{mod}{final}.
    private static readonly Dictionary<string, string> CsiKeyFinals = new()
    {
        { "Up", "A" },
        { "Down", "B" },
        { "Right", "C" },
        { "Left", "D" },
        { "Home", "H" },
        { "End", "F" },
    };

    // F1-F4 use SS3 unmodified; modified variants use CSI 1;{mod}{final}.
    private static readonly Dictionary<string, string> FunctionKeyFinals = new()
    {
        { "F1", "P" },
        { "F2", "Q" },
        { "F3", "R" },
        { "F4", "S" },
    };

    // Keys using CSI {num}~ format; modified variants use CSI {num};{mod}~.
    private static readonly Dictionary<string, int> TildeKeyParams = new()
    {
        { "Insert", 2 },
        { "Del", 3 },
        { "PageUp", 5 },
        { "PageDown", 6 },
        { "F5", 15 },
        { "F6", 17 },
        { "F7", 18 },
        { "F8", 19 },
        { "F9", 20 },
        { "F10", 21 },
        { "F11", 23 },
        { "F12", 24 },
    };

    // Application cursor keys mode (DECCKM) sends SS3 instead of CSI for unmodified arrow keys.
    private static readonly Dictionary<string, string> ApplicationCursorSequences = new()
    {
        { "Up", "\x1BOA" },
        { "Down", "\x1BOB" },
        { "Right", "\x1BOC" },
        { "Left", "\x1BOD" },
        { "Home", "\x1BOH" },
        { "End", "\x1BOF" },
    };

    /// <summary>
    /// Converts a Vim-notation key sequence to a terminal escape sequence.
    /// </summary>
    /// <param name="vimNotation">The key in Vim notation (e.g. "&lt;CR&gt;", "&lt;C-a&gt;", "x").</param>
    /// <param name="applicationCursorKeys">Whether DECCKM (application cursor keys) mode is active.</param>
    /// <returns>The terminal escape sequence string.</returns>
    public static string Encode(string vimNotation, bool applicationCursorKeys = false)
    {
        if (string.IsNullOrEmpty(vimNotation))
        {
            return string.Empty;
        }

        // Check if it's a special key notation: <...>
        // Only match short, ASCII-only content to avoid misidentifying pasted
        // text (e.g. HTML tags or bracketed paste content) as a key sequence.
        if (vimNotation.Length > 2 && vimNotation[0] == '<' && vimNotation[^1] == '>'
            && IsVimKeyNotation(vimNotation))
        {
            string inner = vimNotation.Substring(1, vimNotation.Length - 2);
            return EncodeSpecial(inner, applicationCursorKeys);
        }

        // Regular character or raw text — pass through
        return vimNotation;
    }

    private static bool IsVimKeyNotation(string s)
    {
        // Vim key notation is short: <Esc>, <C-S-F12>, <lt>, etc.
        // Reject strings that are too long or contain non-ASCII / whitespace.
        if (s.Length > 20)
        {
            return false;
        }

        for (int i = 1; i < s.Length - 1; i++)
        {
            char c = s[i];
            if (c <= 0x20 || c >= 0x7F || c == '<' || c == '>')
            {
                return false;
            }
        }

        return true;
    }

    private static string EncodeSpecial(string inner, bool applicationCursorKeys)
    {
        // Parse modifiers: C- (Ctrl), S- (Shift), A- (Alt)
        bool ctrl = false;
        bool shift = false;
        bool alt = false;
        string keyName = inner;

        while (keyName.Length > 2)
        {
            if (keyName.StartsWith("C-"))
            {
                ctrl = true;
                keyName = keyName.Substring(2);
            }
            else if (keyName.StartsWith("S-"))
            {
                shift = true;
                keyName = keyName.Substring(2);
            }
            else if (keyName.StartsWith("A-"))
            {
                alt = true;
                keyName = keyName.Substring(2);
            }
            else
            {
                break;
            }
        }

        bool hasModifiers = ctrl || shift || alt;

        // Modified arrow/cursor keys: CSI 1;{mod}{final}
        if (hasModifiers && CsiKeyFinals.TryGetValue(keyName, out string? csiFinal))
        {
            int mod = GetXtermModifier(ctrl, shift, alt);
            return $"\x1B[1;{mod}{csiFinal}";
        }

        // Modified F1-F4: CSI 1;{mod}{final} (instead of SS3)
        if (hasModifiers && FunctionKeyFinals.TryGetValue(keyName, out string? fkFinal))
        {
            int mod = GetXtermModifier(ctrl, shift, alt);
            return $"\x1B[1;{mod}{fkFinal}";
        }

        // Modified tilde-keys (F5-F12, Insert, Del, PageUp, PageDown): CSI {num};{mod}~
        if (hasModifiers && TildeKeyParams.TryGetValue(keyName, out int tildeNum))
        {
            int mod = GetXtermModifier(ctrl, shift, alt);
            return $"\x1B[{tildeNum};{mod}~";
        }

        // Shift+Tab → back-tab (CSI Z); other modified Tab combos use CSI 1;{mod}Z
        if (hasModifiers && keyName == "Tab")
        {
            if (shift && !ctrl && !alt)
            {
                return "\x1B[Z";
            }

            int mod = GetXtermModifier(ctrl, shift, alt);
            return $"\x1B[1;{mod}Z";
        }

        // Unmodified arrow/cursor keys in DECCKM (application cursor) mode
        if (!hasModifiers && applicationCursorKeys
            && ApplicationCursorSequences.TryGetValue(keyName, out string? appSequence))
        {
            return appSequence;
        }

        // Ctrl(+Shift) + single character
        if (ctrl && !alt && keyName.Length == 1)
        {
            char ch = char.ToUpper(keyName[0]);
            if (ch >= 'A' && ch <= 'Z')
            {
                return ((char)(ch - 'A' + 1)).ToString();
            }

            return ch switch
            {
                '@' => "\x00",
                '[' => "\x1B",
                '\\' => "\x1C",
                ']' => "\x1D",
                '^' => "\x1E",
                '_' => "\x1F",
                _ => keyName,
            };
        }

        // Ctrl+Alt(+Shift) + single character: ESC + Ctrl byte
        if (ctrl && alt && keyName.Length == 1)
        {
            char ch = char.ToUpper(keyName[0]);
            if (ch >= 'A' && ch <= 'Z')
            {
                return $"\x1B{(char)(ch - 'A' + 1)}";
            }

            string? ctrlByte = ch switch
            {
                '@' => "\x00",
                '[' => "\x1B",
                '\\' => "\x1C",
                ']' => "\x1D",
                '^' => "\x1E",
                '_' => "\x1F",
                _ => null,
            };
            return ctrlByte is not null ? $"\x1B{ctrlByte}" : $"\x1B{keyName}";
        }

        // Alt(+Shift) + single character: ESC + char
        if (alt && !ctrl && keyName.Length == 1)
        {
            char ch = shift ? char.ToUpper(keyName[0]) : keyName[0];
            return $"\x1B{ch}";
        }

        // Alt + multi-char special key: ESC prefix + base encoding
        if (alt && !ctrl && SpecialKeys.TryGetValue(keyName, out string? altBase))
        {
            return $"\x1B{altBase}";
        }

        // Look up unmodified special key
        if (SpecialKeys.TryGetValue(keyName, out string? sequence))
        {
            return sequence;
        }

        // Unknown key — return as-is
        return keyName;
    }

    private static int GetXtermModifier(bool ctrl, bool shift, bool alt)
    {
        int mod = 1;
        if (shift)
        {
            mod += 1;
        }

        if (alt)
        {
            mod += 2;
        }

        if (ctrl)
        {
            mod += 4;
        }

        return mod;
    }
}
