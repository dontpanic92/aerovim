// <copyright file="TerminalInputEncoder.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.VimClient
{
    using System.Collections.Generic;

    /// <summary>
    /// Converts Vim-notation key sequences to terminal escape sequences for PTY input.
    /// </summary>
    public static class TerminalInputEncoder
    {
        private static readonly Dictionary<string, string> SpecialKeys = new Dictionary<string, string>
        {
            { "Esc", "\x1B" },
            { "CR", "\r" },
            { "Return", "\r" },
            { "Enter", "\r" },
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

        // Modifier codes for xterm-style modified keys: 2=Shift, 3=Alt, 5=Ctrl, etc.
        private static readonly Dictionary<string, string> ArrowKeys = new Dictionary<string, string>
        {
            { "Up", "A" },
            { "Down", "B" },
            { "Right", "C" },
            { "Left", "D" },
            { "Home", "H" },
            { "End", "F" },
        };

        /// <summary>
        /// Converts a Vim-notation key sequence to a terminal escape sequence.
        /// </summary>
        /// <param name="vimNotation">The key in Vim notation (e.g. "&lt;CR&gt;", "&lt;C-a&gt;", "x").</param>
        /// <returns>The terminal escape sequence string.</returns>
        public static string Encode(string vimNotation)
        {
            if (string.IsNullOrEmpty(vimNotation))
            {
                return string.Empty;
            }

            // Check if it's a special key notation: <...>
            if (vimNotation.Length > 2 && vimNotation[0] == '<' && vimNotation[vimNotation.Length - 1] == '>')
            {
                string inner = vimNotation.Substring(1, vimNotation.Length - 2);
                return EncodeSpecial(inner);
            }

            // Regular character
            return vimNotation;
        }

        private static string EncodeSpecial(string inner)
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

            // Handle modified arrow/special keys with xterm modifier encoding
            if ((ctrl || shift || alt) && ArrowKeys.TryGetValue(keyName, out string arrowFinal))
            {
                int mod = GetXtermModifier(ctrl, shift, alt);
                return $"\x1B[1;{mod}{arrowFinal}";
            }

            // Handle Ctrl + single character
            if (ctrl && !shift && !alt && keyName.Length == 1)
            {
                char ch = char.ToUpper(keyName[0]);
                if (ch >= 'A' && ch <= 'Z')
                {
                    return ((char)(ch - 'A' + 1)).ToString();
                }

                switch (ch)
                {
                    case '@': return "\x00";
                    case '[': return "\x1B";
                    case '\\': return "\x1C";
                    case ']': return "\x1D";
                    case '^': return "\x1E";
                    case '_': return "\x1F";
                }
            }

            // Handle Alt + single character (send ESC + char)
            if (alt && !ctrl && keyName.Length == 1)
            {
                char ch = shift ? char.ToUpper(keyName[0]) : keyName[0];
                return "\x1B" + ch;
            }

            // Look up unmodified special key
            if (SpecialKeys.TryGetValue(keyName, out string sequence))
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
}
