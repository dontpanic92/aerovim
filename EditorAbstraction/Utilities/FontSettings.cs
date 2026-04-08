// <copyright file="FontSettings.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor.Utilities;

/// <summary>
/// Represents the settings of guifont.
/// </summary>
public sealed class FontSettings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FontSettings"/> class.
    /// </summary>
    public FontSettings()
    {
    }

    /// <summary>
    /// Gets or sets the ordered list of font names from guifont.
    /// The first entry is the primary font; subsequent entries are fallbacks.
    /// </summary>
    public List<string> FontNames { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the primary font name (convenience accessor for <see cref="FontNames"/>[0]).
    /// Setting this replaces the entire list with a single entry.
    /// </summary>
    public string FontName
    {
        get => this.FontNames.Count > 0 ? this.FontNames[0] : string.Empty;
        set
        {
            this.FontNames.Clear();
            if (!string.IsNullOrEmpty(value))
            {
                this.FontNames.Add(value);
            }
        }
    }

    /// <summary>
    /// Gets or sets the font point size.
    /// </summary>
    public float FontPointSize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the text is bold.
    /// </summary>
    public bool Bold { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the text is italic.
    /// </summary>
    public bool Italic { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the text is underlined.
    /// </summary>
    public bool Underline { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the text is struck out.
    /// </summary>
    public bool StrikeOut { get; set; }
}
