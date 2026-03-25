// <copyright file="CursorEffects.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls.Utilities
{
    using System;
    using System.Linq;
    using Vortice.Mathematics;
    using D2D = Vortice.Direct2D1;

    /// <summary>
    /// Text Cursor renderer.
    /// </summary>
    public sealed class CursorEffects : EffectChain
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CursorEffects"/> class.
        /// </summary>
        /// <param name="deviceContext">The D2D device context.</param>
        public CursorEffects(D2D.ID2D1DeviceContext deviceContext)
            : base(deviceContext)
        {
            // Set alpha = 1
            var colorMatrix = new Matrix5x4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1);

            this.PushEffect(D2D.EffectGuids.Crop)
                    .SetupLast((e) => e.SetValue((uint)D2D.CropProperties.Rectangle, new System.Numerics.Vector4(0, 0, 0, 0)))
                .PushEffect(D2D.EffectGuids.Invert)
                .PushEffect(D2D.EffectGuids.ColorMatrix)
                    .SetupLast((e) => e.SetValue((uint)D2D.ColorMatrixProperties.ColorMatrix, colorMatrix))
                .SetCompositionMode(D2D.CompositeMode.DestinationOver);
        }

        /// <summary>
        /// Set the cursor boundary.
        /// </summary>
        /// <param name="cursorRect">The cursor rect.</param>
        public void SetCursorRect(Vortice.RawRectF cursorRect)
        {
            this.Effects[0].SetValue((uint)D2D.CropProperties.Rectangle, new System.Numerics.Vector4(cursorRect.Left, cursorRect.Top, cursorRect.Right, cursorRect.Bottom));
        }
    }
}
