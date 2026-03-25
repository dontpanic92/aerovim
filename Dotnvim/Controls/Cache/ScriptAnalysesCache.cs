// <copyright file="ScriptAnalysesCache.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls.Cache
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DWrite = Vortice.DirectWrite;

    /// <summary>
    /// ScriptAnalyses cache.
    /// </summary>
    public sealed class ScriptAnalysesCache
    {
        private const int RetireFrameCount = 50;
        private readonly Dictionary<string, CacheItem> cache = new Dictionary<string, CacheItem>();
        private int frameCount = 0;

        /// <summary>
        /// Start to draw a new frame. Increase the framecount, and clear the outdated items.
        /// </summary>
        public void StartNewFrame()
        {
            if (this.frameCount == int.MaxValue)
            {
                this.frameCount = 1;
                foreach (var v in this.cache.Values)
                {
                    v.LastUsedFrameCount = 0;
                }
            }
            else
            {
                this.frameCount++;
                var outdatedItems = this.cache
                    .Where((kvp) => this.frameCount - kvp.Value.LastUsedFrameCount > RetireFrameCount)
                    .Select((kvp) => kvp.Key)
                    .ToList();
                foreach (var item in outdatedItems)
                {
                    this.cache.Remove(item);
                }
            }
        }

        /// <summary>
        /// Try to get cached item according to the given text. If not present, the call the analysis func to get one.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="analyzeFunc">The function to analyze the text.</param>
        /// <returns>The cached or newly added item.</returns>
        public List<(int CodePointStart, int CodePointLength, DWrite.ScriptAnalysis ScriptAnalysis)> GetOrAddAnalysisResult(
                string text,
                Func<string, List<(int CodePointStart, int CodePointLength, DWrite.ScriptAnalysis ScriptAnalysis)>> analyzeFunc)
        {
            if (this.cache.TryGetValue(text, out var value))
            {
                value.LastUsedFrameCount = this.frameCount;
                return value.ScriptAnalysisResult;
            }
            else
            {
                var result = analyzeFunc(text);
                this.cache.Add(text, new CacheItem() { ScriptAnalysisResult = result, LastUsedFrameCount = this.frameCount });
                return result;
            }
        }

        private class CacheItem
        {
            public List<(int CodePointStart, int CodePointLength, DWrite.ScriptAnalysis ScriptAnalysis)> ScriptAnalysisResult { get; set; }

            public int LastUsedFrameCount { get; set; }
        }
    }
}
