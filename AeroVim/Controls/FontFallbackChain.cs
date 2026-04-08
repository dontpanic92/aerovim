// <copyright file="FontFallbackChain.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Controls;

using System.Runtime.InteropServices;
using AeroVim.Diagnostics;
using AeroVim.Utilities;
using SkiaSharp;

/// <summary>
/// Manages an ordered list of typefaces and provides per-glyph font lookup
/// with caching. The chain is built from the user's font priority list,
/// which may contain sentinel entries for the Neovim guifont and platform
/// system-monospace font lists. The OS font matcher is used as a last resort.
/// </summary>
public sealed class FontFallbackChain : IDisposable
{
    private readonly List<SKTypeface> chain = new List<SKTypeface>();
    private readonly Dictionary<TypefaceKey, SKTypeface> styledCache = new Dictionary<TypefaceKey, SKTypeface>();
    private readonly Dictionary<GlyphKey, SKTypeface> glyphCache = new Dictionary<GlyphKey, SKTypeface>();
    private bool isDisposed;

    /// <summary>
    /// Gets the primary typeface (first entry in the chain), used for
    /// calculating <c>CharWidth</c> and <c>LineHeight</c>.
    /// </summary>
    public SKTypeface? PrimaryTypeface => this.chain.Count > 0 ? this.chain[0] : null;

    /// <summary>
    /// Gets the family name of the primary typeface.
    /// </summary>
    public string PrimaryFontName => this.PrimaryTypeface?.FamilyName ?? string.Empty;

    /// <summary>
    /// Rebuilds the chain from a unified priority list. Each entry is
    /// either a user font name or a sentinel (<c>$GUIFONT</c>,
    /// <c>$SYSTEM_MONO</c>) that is expanded to the corresponding font
    /// name list. The order of the list is the priority order.
    /// Only fonts that are actually available on the system are included.
    /// </summary>
    /// <param name="priorityList">The ordered font priority list (may contain sentinels).</param>
    /// <param name="guiFontNames">Font names to substitute for the <c>$GUIFONT</c> sentinel.</param>
    /// <param name="platformDefaults">Font names to substitute for the <c>$SYSTEM_MONO</c> sentinel.</param>
    public void Rebuild(
        IReadOnlyList<string> priorityList,
        IReadOnlyList<string> guiFontNames,
        IReadOnlyList<string> platformDefaults)
    {
        this.DisposeAll();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in priorityList)
        {
            if (AeroVim.Editor.Utilities.FontPriorityList.IsGuiFontSentinel(entry))
            {
                this.AddFonts(guiFontNames, seen);
            }
            else if (AeroVim.Editor.Utilities.FontPriorityList.IsSystemMonoSentinel(entry))
            {
                this.AddFonts(platformDefaults, seen);
            }
            else if (!string.IsNullOrWhiteSpace(entry))
            {
                this.AddFont(entry, seen);
            }
        }

        AppLogger.For<FontFallbackChain>().Debug(
            $"Rebuild: chain=[{string.Join(", ", this.chain.Select(t => t.FamilyName))}], "
            + $"primary='{this.PrimaryFontName}'.");
    }

    /// <summary>
    /// Returns the typeface that should be used to render the given code point,
    /// searching the chain in priority order with styled-variant support.
    /// Results are cached per (weight, slant, codePoint) combination.
    /// </summary>
    /// <param name="codePoint">The Unicode code point to look up.</param>
    /// <param name="text">The string representation of the code point (for <c>ContainsGlyphs</c>).</param>
    /// <param name="weight">The desired font weight (normal or bold).</param>
    /// <param name="slant">The desired font slant (upright or italic).</param>
    /// <returns>The best-matching typeface, or the primary typeface if nothing matches.</returns>
    public SKTypeface GetTypefaceForGlyph(int codePoint, string text, SKFontStyleWeight weight, SKFontStyleSlant slant)
    {
        var key = new GlyphKey(weight, slant, codePoint);
        if (this.glyphCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        // Search through the chain for a typeface that contains the glyph.
        foreach (var baseTypeface in this.chain)
        {
            var styled = this.GetStyledVariant(baseTypeface, weight, slant);
            if (styled.ContainsGlyphs(text))
            {
                this.glyphCache[key] = styled;
                return styled;
            }
        }

        // Last resort: ask the OS font matcher.
        var osMatch = SKFontManager.Default.MatchCharacter(codePoint);
        if (osMatch is not null)
        {
            this.glyphCache[key] = osMatch;
            return osMatch;
        }

        // Nothing found; return primary styled variant so rendering doesn't crash.
        var fallback = this.GetStyledTypeface(weight, slant);
        this.glyphCache[key] = fallback;
        return fallback;
    }

    /// <summary>
    /// Returns a styled (bold/italic) variant of the primary typeface.
    /// </summary>
    /// <param name="weight">The desired font weight.</param>
    /// <param name="slant">The desired font slant.</param>
    /// <returns>The styled typeface.</returns>
    public SKTypeface GetStyledTypeface(SKFontStyleWeight weight, SKFontStyleSlant slant)
    {
        if (this.PrimaryTypeface is null)
        {
            return SKTypeface.Default;
        }

        return this.GetStyledVariant(this.PrimaryTypeface, weight, slant);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!this.isDisposed)
        {
            this.DisposeAll();
            this.isDisposed = true;
        }
    }

    private static SKTypeface? CreateValidatedTypeface(string fontName)
    {
        var typeface = SKTypeface.FromFamilyName(fontName);
        if (typeface is not null
            && string.Equals(typeface.FamilyName, fontName, StringComparison.OrdinalIgnoreCase))
        {
            return typeface;
        }

        // SkiaSharp silently substituted another font; reject it.
        AppLogger.For<FontFallbackChain>().Debug(
            $"Font '{fontName}' rejected: resolved to '{typeface?.FamilyName ?? "null"}'.");
        typeface?.Dispose();
        return null;
    }

    private SKTypeface GetStyledVariant(SKTypeface baseTypeface, SKFontStyleWeight weight, SKFontStyleSlant slant)
    {
        var key = new TypefaceKey(baseTypeface.FamilyName, weight, slant);
        if (this.styledCache.TryGetValue(key, out var styled))
        {
            return styled;
        }

        // For the normal/upright case, return the base typeface directly.
        if (weight == SKFontStyleWeight.Normal && slant == SKFontStyleSlant.Upright)
        {
            this.styledCache[key] = baseTypeface;
            return baseTypeface;
        }

        var variant = SKTypeface.FromFamilyName(
            baseTypeface.FamilyName, weight, SKFontStyleWidth.Normal, slant);
        styled = variant ?? baseTypeface;
        this.styledCache[key] = styled;
        return styled;
    }

    private void AddFonts(IReadOnlyList<string> fontNames, HashSet<string> seen)
    {
        foreach (var name in fontNames)
        {
            this.AddFont(name, seen);
        }
    }

    private void AddFont(string name, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
        {
            return;
        }

        var typeface = CreateValidatedTypeface(name);
        if (typeface is not null)
        {
            this.chain.Add(typeface);
        }
    }

    private void DisposeAll()
    {
        // Collect all unique typefaces to dispose (chain + styled cache + glyph cache).
        var toDispose = new HashSet<SKTypeface>();

        foreach (var tf in this.chain)
        {
            toDispose.Add(tf);
        }

        foreach (var tf in this.styledCache.Values)
        {
            toDispose.Add(tf);
        }

        foreach (var tf in this.glyphCache.Values)
        {
            toDispose.Add(tf);
        }

        this.chain.Clear();
        this.styledCache.Clear();
        this.glyphCache.Clear();

        foreach (var tf in toDispose)
        {
            // SKTypeface.Default must not be disposed.
            if (tf != SKTypeface.Default)
            {
                tf.Dispose();
            }
        }
    }

    /// <summary>
    /// Cache key for styled font variants (bold/italic).
    /// </summary>
    private readonly struct TypefaceKey : IEquatable<TypefaceKey>
    {
        public TypefaceKey(string fontName, SKFontStyleWeight weight, SKFontStyleSlant slant)
        {
            this.FontName = fontName;
            this.Weight = weight;
            this.Slant = slant;
        }

        public string FontName { get; }

        public SKFontStyleWeight Weight { get; }

        public SKFontStyleSlant Slant { get; }

        public bool Equals(TypefaceKey other)
        {
            return string.Equals(this.FontName, other.FontName, StringComparison.Ordinal)
                && this.Weight == other.Weight
                && this.Slant == other.Slant;
        }

        public override bool Equals(object? obj)
        {
            return obj is TypefaceKey other && this.Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.FontName, this.Weight, this.Slant);
        }
    }

    /// <summary>
    /// Cache key for per-glyph typeface lookups.
    /// </summary>
    private readonly struct GlyphKey : IEquatable<GlyphKey>
    {
        public GlyphKey(SKFontStyleWeight weight, SKFontStyleSlant slant, int codePoint)
        {
            this.Weight = weight;
            this.Slant = slant;
            this.CodePoint = codePoint;
        }

        public SKFontStyleWeight Weight { get; }

        public SKFontStyleSlant Slant { get; }

        public int CodePoint { get; }

        public bool Equals(GlyphKey other)
        {
            return this.Weight == other.Weight
                && this.Slant == other.Slant
                && this.CodePoint == other.CodePoint;
        }

        public override bool Equals(object? obj)
        {
            return obj is GlyphKey other && this.Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.Weight, this.Slant, this.CodePoint);
        }
    }
}
