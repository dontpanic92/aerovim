// <copyright file="StringExtensions.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Extensions for string.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Convert a string to codepoints.
        /// </summary>
        /// <param name="s">the string.</param>
        /// <returns>codepoints.</returns>
        public static int[] CodePoints(this string s)
        {
            var codePoints = new List<int>();
            for (int i = 0; i < s.Length; i += char.IsSurrogatePair(s, i) ? 2 : 1)
            {
                codePoints.Add(char.ConvertToUtf32(s, i));
            }

            return codePoints.ToArray();
        }
    }
}
