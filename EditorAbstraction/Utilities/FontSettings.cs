// <copyright file="FontSettings.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor.Utilities;

/// <summary>
/// Represents the settings of guifont.
/// </summary>
public struct FontSettings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FontSettings"/> struct.
    /// </summary>
    public FontSettings()
    {
    }

    /// <summary>
    /// Gets or sets the font name.
    /// </summary>
    public string FontName { get; set; } = string.Empty;

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
