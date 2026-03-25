// <copyright file="BrushCache.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls.Cache
{
    using System;
    using System.Collections.Generic;
    using Dotnvim.Utilities;
    using D2D = Vortice.Direct2D1;

    /// <summary>
    /// Brush Cache.
    /// </summary>
    public sealed class BrushCache : IDisposable
    {
        private readonly Dictionary<int, D2D.ID2D1SolidColorBrush> brushCache
            = new Dictionary<int, D2D.ID2D1SolidColorBrush>();

        /// <summary>
        /// Get brush using specified color.
        /// </summary>
        /// <param name="dc">Device context.</param>
        /// <param name="color">Color in int.</param>
        /// <returns>Created or cached brush.</returns>
        public D2D.ID2D1SolidColorBrush GetBrush(D2D.ID2D1DeviceContext dc, int color)
        {
            if (this.brushCache.TryGetValue(color, out var brush))
            {
                return brush;
            }
            else
            {
                var newBrush = dc.CreateSolidColorBrush(Helpers.GetColor(color));
                this.brushCache.Add(color, newBrush);
                return newBrush;
            }
        }

        /// <summary>
        /// Clear brush cache.
        /// </summary>
        public void ClearBrushCache()
        {
            foreach (var brush in this.brushCache.Values)
            {
                brush.Dispose();
            }
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            this.ClearBrushCache();
        }
    }
}
