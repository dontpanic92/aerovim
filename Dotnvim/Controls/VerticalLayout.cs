// <copyright file="VerticalLayout.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls
{
    using System.Drawing;
    using System.Numerics;

    /// <summary>
    /// Vertical Layout.
    /// </summary>
    public class VerticalLayout : BoxLayout
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VerticalLayout"/> class.
        /// </summary>
        /// <param name="parent">The Parent control.</param>
        public VerticalLayout(IElement parent)
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
                    control.Size = new SizeF(this.Size.Width - (this.Padding * 2), control.Size.Height);
                    nonStrenchLength += control.Size.Height;
                }
            }

            if (strenchControl != null)
            {
                var height = this.Size.Height - (this.Padding * 2) - nonStrenchLength;
                if (height < 0)
                {
                    height = 0;
                }

                strenchControl.Size = new SizeF(this.Size.Width - (this.Padding * 2), height);
            }

            float position = this.Padding;
            foreach (var (control, _) in this.Controls)
            {
                control.Position = new Vector2(this.Position.X + this.Padding, this.Position.Y + position);
                position += control.Size.Height;

                control.Layout();
            }
        }
    }
}
