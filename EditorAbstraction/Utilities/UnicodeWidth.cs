// <copyright file="UnicodeWidth.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor.Utilities;

/// <summary>
/// Determines the display width of Unicode characters for terminal grid layout.
/// Wide (double-width) characters occupy two cells; all others occupy one.
/// </summary>
public static class UnicodeWidth
{
    /// <summary>
    /// Returns true if the given Unicode code point is a wide (double-width) character
    /// that occupies two cells in a terminal grid.
    /// </summary>
    /// <param name="codePoint">A Unicode code point.</param>
    /// <returns>True if the character is double-width.</returns>
    public static bool IsWideCharacter(int codePoint)
    {
        // Hangul Jamo (initial consonants are wide)
        if (codePoint >= 0x1100 && codePoint <= 0x115F)
        {
            return true;
        }

        // Miscellaneous Technical: LEFT/RIGHT-POINTING ANGLE BRACKET
        if (codePoint == 0x2329 || codePoint == 0x232A)
        {
            return true;
        }

        // CJK Radicals Supplement .. Kangxi Radicals
        if (codePoint >= 0x2E80 && codePoint <= 0x2FDF)
        {
            return true;
        }

        // Ideographic Description Characters .. CJK Symbols and Punctuation
        if (codePoint >= 0x2FF0 && codePoint <= 0x303E)
        {
            return true;
        }

        // Hiragana .. Katakana
        if (codePoint >= 0x3041 && codePoint <= 0x30FF)
        {
            return true;
        }

        // Bopomofo
        if (codePoint >= 0x3105 && codePoint <= 0x312F)
        {
            return true;
        }

        // Hangul Compatibility Jamo
        if (codePoint >= 0x3131 && codePoint <= 0x318E)
        {
            return true;
        }

        // Bopomofo Extended
        if (codePoint >= 0x31A0 && codePoint <= 0x31BF)
        {
            return true;
        }

        // Katakana Phonetic Extensions
        if (codePoint >= 0x31F0 && codePoint <= 0x31FF)
        {
            return true;
        }

        // Enclosed CJK Letters and Months .. CJK Compatibility
        if (codePoint >= 0x3200 && codePoint <= 0x33FF)
        {
            return true;
        }

        // CJK Unified Ideographs Extension A
        if (codePoint >= 0x3400 && codePoint <= 0x4DBF)
        {
            return true;
        }

        // CJK Unified Ideographs
        if (codePoint >= 0x4E00 && codePoint <= 0x9FFF)
        {
            return true;
        }

        // Yi Syllables .. Yi Radicals
        if (codePoint >= 0xA000 && codePoint <= 0xA4CF)
        {
            return true;
        }

        // Hangul Syllables
        if (codePoint >= 0xAC00 && codePoint <= 0xD7A3)
        {
            return true;
        }

        // Hangul Jamo Extended-B
        if (codePoint >= 0xD7B0 && codePoint <= 0xD7FF)
        {
            return true;
        }

        // CJK Compatibility Ideographs
        if (codePoint >= 0xF900 && codePoint <= 0xFAFF)
        {
            return true;
        }

        // CJK Compatibility Forms
        if (codePoint >= 0xFE30 && codePoint <= 0xFE4F)
        {
            return true;
        }

        // Fullwidth Forms (excluding halfwidth katakana U+FF61..FFDC)
        if (codePoint >= 0xFF01 && codePoint <= 0xFF60)
        {
            return true;
        }

        // Fullwidth currency symbols etc.
        if (codePoint >= 0xFFE0 && codePoint <= 0xFFE6)
        {
            return true;
        }

        // CJK Unified Ideographs Extension B
        if (codePoint >= 0x20000 && codePoint <= 0x2A6DF)
        {
            return true;
        }

        // CJK Unified Ideographs Extension C
        if (codePoint >= 0x2A700 && codePoint <= 0x2B73F)
        {
            return true;
        }

        // CJK Unified Ideographs Extension D
        if (codePoint >= 0x2B740 && codePoint <= 0x2B81F)
        {
            return true;
        }

        // CJK Unified Ideographs Extension E
        if (codePoint >= 0x2B820 && codePoint <= 0x2CEAF)
        {
            return true;
        }

        // CJK Unified Ideographs Extension F
        if (codePoint >= 0x2CEB0 && codePoint <= 0x2EBEF)
        {
            return true;
        }

        // CJK Compatibility Ideographs Supplement
        if (codePoint >= 0x2F800 && codePoint <= 0x2FA1F)
        {
            return true;
        }

        // CJK Unified Ideographs Extension G
        if (codePoint >= 0x30000 && codePoint <= 0x3134F)
        {
            return true;
        }

        // CJK Unified Ideographs Extension H
        if (codePoint >= 0x31350 && codePoint <= 0x323AF)
        {
            return true;
        }

        // Emoji and symbol ranges that are rendered as wide (double-width) characters.

        // Miscellaneous Symbols (subset commonly rendered wide)
        if (codePoint >= 0x2600 && codePoint <= 0x26FF)
        {
            return true;
        }

        // Dingbats (subset commonly rendered wide)
        if (codePoint >= 0x2700 && codePoint <= 0x27BF)
        {
            return true;
        }

        // Supplemental Arrows-B / Misc Symbols and Arrows (some wide)
        if (codePoint >= 0x2B05 && codePoint <= 0x2B55)
        {
            return true;
        }

        // Regional Indicator Symbols
        if (codePoint >= 0x1F1E0 && codePoint <= 0x1F1FF)
        {
            return true;
        }

        // Miscellaneous Symbols and Pictographs
        if (codePoint >= 0x1F300 && codePoint <= 0x1F5FF)
        {
            return true;
        }

        // Emoticons
        if (codePoint >= 0x1F600 && codePoint <= 0x1F64F)
        {
            return true;
        }

        // Transport and Map Symbols
        if (codePoint >= 0x1F680 && codePoint <= 0x1F6FF)
        {
            return true;
        }

        // Geometric Shapes Extended
        if (codePoint >= 0x1F780 && codePoint <= 0x1F7FF)
        {
            return true;
        }

        // Supplemental Symbols and Pictographs
        if (codePoint >= 0x1F900 && codePoint <= 0x1F9FF)
        {
            return true;
        }

        // Symbols and Pictographs Extended-A
        if (codePoint >= 0x1FA00 && codePoint <= 0x1FA6F)
        {
            return true;
        }

        // Symbols and Pictographs Extended-B
        if (codePoint >= 0x1FA70 && codePoint <= 0x1FAFF)
        {
            return true;
        }

        // Mahjong Tiles
        if (codePoint >= 0x1F000 && codePoint <= 0x1F02F)
        {
            return true;
        }

        // Domino Tiles
        if (codePoint >= 0x1F030 && codePoint <= 0x1F09F)
        {
            return true;
        }

        // Playing Cards
        if (codePoint >= 0x1F0A0 && codePoint <= 0x1F0FF)
        {
            return true;
        }

        return false;
    }
}
