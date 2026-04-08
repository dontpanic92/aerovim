// <copyright file="FontPriorityListTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests;

using AeroVim.Editor.Utilities;
using NUnit.Framework;

/// <summary>
/// Tests for <see cref="FontPriorityList"/> sentinel normalization.
/// </summary>
public class FontPriorityListTests
{
    /// <summary>
    /// A null list should produce the default sentinel-only list.
    /// </summary>
    [Test]
    public void Normalize_NullList_ReturnsDefaults()
    {
        var result = FontPriorityList.Normalize(null);
        Assert.That(result, Is.EqualTo(new[] { "$GUIFONT", "$SYSTEM_MONO" }));
    }

    /// <summary>
    /// An empty list should produce the default sentinel-only list.
    /// </summary>
    [Test]
    public void Normalize_EmptyList_ReturnsDefaults()
    {
        var result = FontPriorityList.Normalize(new List<string>());
        Assert.That(result, Is.EqualTo(new[] { "$GUIFONT", "$SYSTEM_MONO" }));
    }

    /// <summary>
    /// A list with only user fonts should get sentinels inserted at default positions.
    /// </summary>
    [Test]
    public void Normalize_UserFontsOnly_InsertsSentinels()
    {
        var result = FontPriorityList.Normalize(new List<string> { "Hack", "Fira Code" });
        Assert.That(result, Is.EqualTo(new[] { "$GUIFONT", "Hack", "Fira Code", "$SYSTEM_MONO" }));
    }

    /// <summary>
    /// A list with both sentinels should pass through unchanged.
    /// </summary>
    [Test]
    public void Normalize_WithBothSentinels_PreservesOrder()
    {
        var input = new List<string> { "Hack", "$GUIFONT", "Fira Code", "$SYSTEM_MONO" };
        var result = FontPriorityList.Normalize(input);
        Assert.That(result, Is.EqualTo(new[] { "Hack", "$GUIFONT", "Fira Code", "$SYSTEM_MONO" }));
    }

    /// <summary>
    /// Duplicate sentinels should be removed (keep first occurrence).
    /// </summary>
    [Test]
    public void Normalize_DuplicateSentinels_KeepsFirst()
    {
        var input = new List<string> { "$GUIFONT", "Hack", "$GUIFONT", "$SYSTEM_MONO", "$SYSTEM_MONO" };
        var result = FontPriorityList.Normalize(input);
        Assert.That(result, Is.EqualTo(new[] { "$GUIFONT", "Hack", "$SYSTEM_MONO" }));
    }

    /// <summary>
    /// Missing $GUIFONT should be inserted before the first user font.
    /// </summary>
    [Test]
    public void Normalize_MissingGuifont_InsertsBeforeFirstUserFont()
    {
        var input = new List<string> { "Hack", "$SYSTEM_MONO" };
        var result = FontPriorityList.Normalize(input);
        Assert.That(result, Is.EqualTo(new[] { "$GUIFONT", "Hack", "$SYSTEM_MONO" }));
    }

    /// <summary>
    /// Missing $SYSTEM_MONO should be appended at the end.
    /// </summary>
    [Test]
    public void Normalize_MissingSystemMono_AppendsAtEnd()
    {
        var input = new List<string> { "$GUIFONT", "Hack" };
        var result = FontPriorityList.Normalize(input);
        Assert.That(result, Is.EqualTo(new[] { "$GUIFONT", "Hack", "$SYSTEM_MONO" }));
    }

    /// <summary>
    /// Misspelled sentinels should be kept as regular font names.
    /// </summary>
    [Test]
    public void Normalize_MisspelledSentinel_TreatedAsUserFont()
    {
        var input = new List<string> { "$guifont", "Hack" };
        var result = FontPriorityList.Normalize(input);

        // "$guifont" (lowercase) is not recognized, kept as user font.
        // Both real sentinels are auto-inserted.
        Assert.That(result, Has.Member("$guifont"));
        Assert.That(result, Has.Member("$GUIFONT"));
        Assert.That(result, Has.Member("$SYSTEM_MONO"));
    }

    /// <summary>
    /// ExtractUserFonts should return only non-sentinel entries.
    /// </summary>
    [Test]
    public void ExtractUserFonts_ReturnsOnlyUserEntries()
    {
        var input = new List<string> { "Hack", "$GUIFONT", "Fira Code", "$SYSTEM_MONO" };
        var result = FontPriorityList.ExtractUserFonts(input);
        Assert.That(result, Is.EqualTo(new[] { "Hack", "Fira Code" }));
    }

    /// <summary>
    /// IsSentinel should return true for both recognized sentinel strings.
    /// </summary>
    [Test]
    public void IsSentinel_RecognizedValues_ReturnsTrue()
    {
        Assert.That(FontPriorityList.IsSentinel("$GUIFONT"), Is.True);
        Assert.That(FontPriorityList.IsSentinel("$SYSTEM_MONO"), Is.True);
    }

    /// <summary>
    /// IsSentinel should return false for regular font names and misspelled sentinels.
    /// </summary>
    [Test]
    public void IsSentinel_NonSentinelValues_ReturnsFalse()
    {
        Assert.That(FontPriorityList.IsSentinel("Hack"), Is.False);
        Assert.That(FontPriorityList.IsSentinel("$guifont"), Is.False);
        Assert.That(FontPriorityList.IsSentinel(string.Empty), Is.False);
    }
}
