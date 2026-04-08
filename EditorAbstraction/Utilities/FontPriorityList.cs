// <copyright file="FontPriorityList.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor.Utilities;

/// <summary>
/// Helpers for the unified font priority list stored in settings.
/// The list contains user font names interleaved with two sentinel
/// strings that represent the Neovim guifont and the platform
/// system-monospace font lists.
/// </summary>
public static class FontPriorityList
{
    /// <summary>
    /// Sentinel representing the Neovim guifont font list.
    /// </summary>
    public const string GuiFontSentinel = "$GUIFONT";

    /// <summary>
    /// Sentinel representing the platform system-monospace font list.
    /// </summary>
    public const string SystemMonoSentinel = "$SYSTEM_MONO";

    /// <summary>
    /// Returns <c>true</c> when the entry is a recognised sentinel
    /// (<see cref="GuiFontSentinel"/> or <see cref="SystemMonoSentinel"/>).
    /// The comparison is ordinal and case-sensitive so that misspelled
    /// entries are treated as ordinary (invalid) font names.
    /// </summary>
    /// <param name="entry">The font list entry to test.</param>
    /// <returns><c>true</c> if the entry is a sentinel.</returns>
    public static bool IsSentinel(string entry)
    {
        return string.Equals(entry, GuiFontSentinel, StringComparison.Ordinal)
            || string.Equals(entry, SystemMonoSentinel, StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns <c>true</c> when the entry is the guifont sentinel.
    /// </summary>
    /// <param name="entry">The font list entry to test.</param>
    /// <returns><c>true</c> if the entry is the guifont sentinel.</returns>
    public static bool IsGuiFontSentinel(string entry)
    {
        return string.Equals(entry, GuiFontSentinel, StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns <c>true</c> when the entry is the system-monospace sentinel.
    /// </summary>
    /// <param name="entry">The font list entry to test.</param>
    /// <returns><c>true</c> if the entry is the system-monospace sentinel.</returns>
    public static bool IsSystemMonoSentinel(string entry)
    {
        return string.Equals(entry, SystemMonoSentinel, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the list contains exactly one <see cref="GuiFontSentinel"/>
    /// and exactly one <see cref="SystemMonoSentinel"/>.
    /// <list type="bullet">
    ///   <item>Missing sentinels are inserted at their default positions
    ///         (<c>$GUIFONT</c> before the first user font,
    ///          <c>$SYSTEM_MONO</c> at the end).</item>
    ///   <item>Duplicate sentinels beyond the first occurrence are removed.</item>
    ///   <item>An empty or null list is treated as the default
    ///         <c>["$GUIFONT", "$SYSTEM_MONO"]</c>.</item>
    /// </list>
    /// </summary>
    /// <param name="list">The raw list from settings (may be null).</param>
    /// <returns>A new list with exactly one of each sentinel.</returns>
    public static List<string> Normalize(IList<string>? list)
    {
        if (list is null || list.Count == 0)
        {
            return new List<string> { GuiFontSentinel, SystemMonoSentinel };
        }

        var result = new List<string>(list.Count + 2);
        bool hasGuiFont = false;
        bool hasSystemMono = false;

        foreach (var entry in list)
        {
            if (IsGuiFontSentinel(entry))
            {
                if (!hasGuiFont)
                {
                    result.Add(GuiFontSentinel);
                    hasGuiFont = true;
                }
            }
            else if (IsSystemMonoSentinel(entry))
            {
                if (!hasSystemMono)
                {
                    result.Add(SystemMonoSentinel);
                    hasSystemMono = true;
                }
            }
            else
            {
                result.Add(entry);
            }
        }

        // Insert missing sentinels at default positions.
        if (!hasGuiFont)
        {
            // $GUIFONT goes before the first user font entry.
            int insertIndex = 0;
            for (int i = 0; i < result.Count; i++)
            {
                if (!IsSentinel(result[i]))
                {
                    insertIndex = i;
                    break;
                }

                insertIndex = i + 1;
            }

            result.Insert(insertIndex, GuiFontSentinel);
        }

        if (!hasSystemMono)
        {
            result.Add(SystemMonoSentinel);
        }

        return result;
    }

    /// <summary>
    /// Extracts only the user font names from a normalized priority list
    /// (i.e. everything that is not a sentinel).
    /// </summary>
    /// <param name="list">A normalized priority list.</param>
    /// <returns>A list containing only user font names.</returns>
    public static List<string> ExtractUserFonts(IList<string> list)
    {
        var fonts = new List<string>();
        foreach (var entry in list)
        {
            if (!IsSentinel(entry))
            {
                fonts.Add(entry);
            }
        }

        return fonts;
    }
}
