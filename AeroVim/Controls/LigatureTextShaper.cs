// <copyright file="LigatureTextShaper.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Controls;

using System.Runtime.InteropServices;
using HarfBuzzSharp;
using SkiaSharp;
using HarfBuzzBuffer = HarfBuzzSharp.Buffer;
using HarfBuzzFont = HarfBuzzSharp.Font;

/// <summary>
/// Shapes text runs into positioned glyph data for ligature rendering.
/// </summary>
internal sealed class LigatureTextShaper : IDisposable
{
    private const float HarfBuzzScaleFactor = 64f;
    private static readonly Feature[] LigatureFeatures =
    [
        Feature.Parse("calt=1"),
        Feature.Parse("clig=1"),
        Feature.Parse("liga=1"),
    ];

    private readonly Dictionary<nint, CachedTypeface> typefaceCache = new();
    private bool isDisposed;

    /// <summary>
    /// Shapes a run of text using the specified typeface and size.
    /// </summary>
    /// <param name="typeface">The typeface to shape against.</param>
    /// <param name="textSize">The font size in device pixels.</param>
    /// <param name="text">The text to shape.</param>
    /// <returns>The shaped text run, or <c>null</c> if shaping produced no glyphs.</returns>
    public ShapedTextRun? ShapeText(SKTypeface typeface, float textSize, string text)
    {
        ObjectDisposedException.ThrowIf(this.isDisposed, this);

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var cachedTypeface = this.GetOrCreateTypeface(typeface);
        cachedTypeface.Font.SetScale(ToHarfBuzzScale(textSize), ToHarfBuzzScale(textSize));

        using var buffer = new HarfBuzzBuffer();
        buffer.AddUtf16(text);
        buffer.GuessSegmentProperties();
        cachedTypeface.Font.Shape(buffer, LigatureFeatures);

        var glyphInfos = buffer.GlyphInfos;
        var glyphPositions = buffer.GlyphPositions;
        if (glyphInfos.Length == 0)
        {
            return null;
        }

        ushort[] glyphs = new ushort[glyphInfos.Length];
        SKPoint[] points = new SKPoint[glyphInfos.Length];
        uint[] clusters = new uint[glyphInfos.Length];
        float penX = 0f;
        float penY = 0f;

        for (int i = 0; i < glyphInfos.Length; i++)
        {
            glyphs[i] = checked((ushort)glyphInfos[i].Codepoint);
            clusters[i] = glyphInfos[i].Cluster;

            float xOffset = glyphPositions[i].XOffset / HarfBuzzScaleFactor;
            float yOffset = glyphPositions[i].YOffset / HarfBuzzScaleFactor;
            points[i] = new SKPoint(penX + xOffset, penY - yOffset);

            penX += glyphPositions[i].XAdvance / HarfBuzzScaleFactor;
            penY += glyphPositions[i].YAdvance / HarfBuzzScaleFactor;
        }

        return new ShapedTextRun(glyphs, points, clusters, penX);
    }

    /// <summary>
    /// Clears the cached HarfBuzz font bridges.
    /// </summary>
    public void ClearCache()
    {
        foreach (var cachedTypeface in this.typefaceCache.Values)
        {
            cachedTypeface.Dispose();
        }

        this.typefaceCache.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!this.isDisposed)
        {
            this.ClearCache();
            this.isDisposed = true;
        }
    }

    /// <summary>
    /// Builds a text blob from positioned glyph data.
    /// </summary>
    /// <param name="typeface">The typeface used for the glyphs.</param>
    /// <param name="textSize">The font size in device pixels.</param>
    /// <param name="glyphIds">The glyph ids to render.</param>
    /// <param name="points">The glyph positions relative to the blob origin.</param>
    /// <returns>The created text blob, or <c>null</c> if the blob could not be created.</returns>
    public SKTextBlob? CreateTextBlob(SKTypeface typeface, float textSize, ReadOnlySpan<ushort> glyphIds, ReadOnlySpan<SKPoint> points)
    {
        using var font = new SKFont(typeface, textSize, 1f, 0f);
        using var builder = new SKTextBlobBuilder();
        builder.AddPositionedRun(glyphIds, font, points);
        return builder.Build();
    }

    private static int ToHarfBuzzScale(float textSize)
    {
        return (int)MathF.Round(textSize * HarfBuzzScaleFactor);
    }

    private CachedTypeface GetOrCreateTypeface(SKTypeface typeface)
    {
        if (this.typefaceCache.TryGetValue(typeface.Handle, out var cachedTypeface))
        {
            return cachedTypeface;
        }

        cachedTypeface = new CachedTypeface(typeface);
        this.typefaceCache[typeface.Handle] = cachedTypeface;
        return cachedTypeface;
    }

    /// <summary>
    /// A shaped text run ready for drawing.
    /// </summary>
    internal sealed class ShapedTextRun
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ShapedTextRun"/> class.
        /// </summary>
        /// <param name="glyphIds">The glyph ids in the shaped run.</param>
        /// <param name="points">The glyph positions in the shaped run.</param>
        /// <param name="clusters">The HarfBuzz cluster indices for each glyph.</param>
        /// <param name="width">The total advance width of the shaped run.</param>
        public ShapedTextRun(ushort[] glyphIds, SKPoint[] points, uint[] clusters, float width)
        {
            this.GlyphIds = glyphIds;
            this.Points = points;
            this.Clusters = clusters;
            this.Width = width;
        }

        /// <summary>
        /// Gets the glyph ids in the shaped run.
        /// </summary>
        public ushort[] GlyphIds { get; }

        /// <summary>
        /// Gets the glyph positions in the shaped run.
        /// </summary>
        public SKPoint[] Points { get; }

        /// <summary>
        /// Gets the HarfBuzz cluster index for each glyph.
        /// </summary>
        public uint[] Clusters { get; }

        /// <summary>
        /// Gets the number of glyphs in the shaped run.
        /// </summary>
        public int GlyphCount => this.GlyphIds.Length;

        /// <summary>
        /// Gets the total advance width of the shaped run.
        /// </summary>
        public float Width { get; }
    }

    private sealed class CachedTypeface : IDisposable
    {
        private readonly byte[] fontBytes;
        private readonly GCHandle fontBytesHandle;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedTypeface"/> class.
        /// </summary>
        /// <param name="typeface">The typeface whose font data should be cached.</param>
        public CachedTypeface(SKTypeface typeface)
        {
            using var stream = typeface.OpenStream(out int fontIndex);
            if (stream is null || !stream.HasLength || stream.Length <= 0)
            {
                throw new InvalidOperationException($"Unable to open font data for '{typeface.FamilyName}'.");
            }

            this.fontBytes = new byte[stream.Length];
            int bytesRead = stream.Read(this.fontBytes, this.fontBytes.Length);
            if (bytesRead != this.fontBytes.Length)
            {
                throw new InvalidOperationException($"Unable to read font data for '{typeface.FamilyName}'.");
            }

            this.fontBytesHandle = GCHandle.Alloc(this.fontBytes, GCHandleType.Pinned);
            this.Blob = new Blob(this.fontBytesHandle.AddrOfPinnedObject(), this.fontBytes.Length, MemoryMode.ReadOnly);
            this.Face = new Face(this.Blob, fontIndex);
            this.Font = new HarfBuzzFont(this.Face);
            this.Font.SetFunctionsOpenType();
        }

        /// <summary>
        /// Gets the pinned HarfBuzz blob.
        /// </summary>
        public Blob Blob { get; }

        /// <summary>
        /// Gets the HarfBuzz face.
        /// </summary>
        public Face Face { get; }

        /// <summary>
        /// Gets the HarfBuzz font bridge.
        /// </summary>
        public HarfBuzzFont Font { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.Font.Dispose();
            this.Face.Dispose();
            this.Blob.Dispose();

            if (this.fontBytesHandle.IsAllocated)
            {
                this.fontBytesHandle.Free();
            }

            this.isDisposed = true;
        }
    }
}
