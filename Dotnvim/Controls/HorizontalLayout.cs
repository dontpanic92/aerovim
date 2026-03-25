// <copyright file="HorizontalLayout.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls
{
    using System.Drawing;
    using System.Numerics;

    /// <summary>
    /// Horizontal Layout.
    /// </summary>
    public class HorizontalLayout : BoxLayout
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HorizontalLayout"/> class.
        /// </summary>
        /// <param name="parent">The parent control.</param>
        public HorizontalLayout(IElement parent)
            : base(parent)
        {
        }

        /// <inheritdoc />
        public override void Layout()
        {
            IElement strenchControl = null;
            float nonStrenchLength = 0;
            foreach (var (control, state) in this.Controls)
            {
                if (state.Strentch)
                {
                    strenchControl = control;
                }
                else
                {
                    control.Size = new SizeF(control.Size.Width, this.Size.Height - (this.Padding * 2));
                    nonStrenchLength += control.Size.Width;
                }
            }

            if (strenchControl != null)
            {
                var width = this.Size.Width - (this.Padding * 2) - nonStrenchLength;
                if (width < 0)
                {
                    width = 0;
                }

                strenchControl.Size = new SizeF(width, this.Size.Height - (this.Padding * 2));
            }

            float position = this.Padding;
            foreach (var (control, _) in this.Controls)
            {
                control.Position = new Vector2(this.Position.X + position, this.Position.Y + this.Padding);
                position += control.Size.Width;

                control.Layout();
            }
        }
    }
}
