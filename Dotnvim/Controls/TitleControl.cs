// <copyright file="TitleControl.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Text;
    using System.Threading.Tasks;
    using Dotnvim.Utilities;
    using Vortice.Mathematics;
    using D2D = Vortice.Direct2D1;
    using DWrite = Vortice.DirectWrite;

    /// <summary>
    /// The Title Control.
    /// </summary>
    public class TitleControl : ControlBase
    {
        private const float PaddingHorizontal = 8;
        private readonly Vector2 origin = new Vector2(PaddingHorizontal, 0);

        private DWrite.IDWriteFactory dwriteFactory = DWrite.DWrite.DWriteCreateFactory<DWrite.IDWriteFactory>();
        private DWrite.IDWriteTextFormat textFormat;
        private DWrite.IDWriteTextLayout textLayout;
        private D2D.ID2D1Brush textBrush;

        private string text = "dotnvim";
        private Color4 color;

        /// <summary>
        /// Initializes a new instance of the <see cref="TitleControl"/> class.
        /// </summary>
        /// <param name="parent">The parent control.</param>
        public TitleControl(IElement parent)
            : base(parent)
        {
            this.textFormat = this.dwriteFactory.CreateTextFormat("Segoe UI", Helpers.GetFontSize(10));
            this.textFormat.ParagraphAlignment = DWrite.ParagraphAlignment.Center;
            this.textFormat.TextAlignment = DWrite.TextAlignment.Trailing;
            this.textFormat.WordWrapping = DWrite.WordWrapping.NoWrap;
            var sign = this.dwriteFactory.CreateEllipsisTrimmingSign(this.textFormat);
            this.textFormat.SetTrimming(new DWrite.Trimming() { Granularity = DWrite.TrimmingGranularity.Character, Delimiter = 0, DelimiterCount = 0 }, sign);
            sign.Dispose();

            this.Color = new Color4(1, 1, 1, 1);
        }

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string Text
        {
            get
            {
                return this.text;
            }

            set
            {
                this.text = value;
                this.Layout();

                this.Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the text color.
        /// </summary>
        public Color4 Color
        {
            get
            {
                return this.color;
            }

            set
            {
                this.color = value;

                this.textBrush?.Dispose();
                this.textBrush = this.DeviceContext.CreateSolidColorBrush(this.color);

                this.Invalidate();
            }
        }

        /// <inheritdoc />
        public override void Layout()
        {
            base.Layout();

            this.textLayout?.Dispose();

            var width = this.Size.Width - (PaddingHorizontal * 2);
            this.textLayout = this.dwriteFactory.CreateTextLayout(this.text, this.textFormat, width <= 0 ? 1 : width, this.Size.Height);
        }

        /// <inheritdoc />
        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            this.textFormat.Dispose();
            this.textLayout?.Dispose();
            this.textBrush?.Dispose();
            this.dwriteFactory.Dispose();
        }

        /// <inheritdoc />
        protected override void Draw()
        {
            this.DeviceContext.BeginDraw();
            this.DeviceContext.Clear(new Color4(0, 0, 0, 0));

            this.DeviceContext.DrawTextLayout(this.origin, this.textLayout, this.textBrush);

            this.DeviceContext.EndDraw();
        }
    }
}
