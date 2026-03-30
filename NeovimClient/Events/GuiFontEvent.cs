// <copyright file="GuiFontEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

using AeroVim.Editor.Utilities;

/// <summary>
/// The GuiFont event.
/// </summary>
public class GuiFontEvent : IRedrawEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GuiFontEvent"/> class.
    /// </summary>
    /// <param name="rawValue">The option value.</param>
    public GuiFontEvent(string rawValue)
    {
        var values = rawValue.Split(':');
        var font = new FontSettings()
        {
            FontName = values[0],
            FontPointSize = 11,
            Bold = false,
            Italic = false,
            StrikeOut = false,
            Underline = false,
        };

        foreach (var value in values.Skip(1))
        {
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            switch (value[0])
            {
                case 'h':
                    if (!float.TryParse(value.Substring(1), out var heightPoint))
                    {
                        continue;
                    }

                    font.FontPointSize = heightPoint;
                    break;
                case 'b':
                    font.Bold = true;
                    break;
                case 'i':
                    font.Italic = true;
                    break;
                case 'u':
                    font.Underline = true;
                    break;
                case 's':
                    font.StrikeOut = true;
                    break;
            }
        }

        this.FontSettings = font;
    }

    /// <summary>
    /// Gets the font settings.
    /// </summary>
    public FontSettings FontSettings { get; }
}
