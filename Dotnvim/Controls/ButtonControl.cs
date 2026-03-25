// <copyright file="ButtonControl.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls
{
    using System.Drawing;
    using System.Numerics;
    using Dotnvim.Events;
    using Dotnvim.Utilities;
    using Vortice.Mathematics;
    using D2D = Vortice.Direct2D1;
    using DWrite = Vortice.DirectWrite;

    /// <summary>
    /// The button control.
    /// </summary>
    public class ButtonControl : ControlBase
    {
        private readonly Vector2 origin = new Vector2(0, 0);

        private DWrite.IDWriteFactory dwriteFactory = DWrite.DWrite.DWriteCreateFactory<DWrite.IDWriteFactory>();
        private DWrite.IDWriteTextFormat textFormat;
        private DWrite.IDWriteTextLayout textLayout;
        private D2D.ID2D1Brush foregroundBrush;
        private D2D.ID2D1Brush backgroundBrush;
        private string text = string.Empty;
        private Color4 backgroundColor;
        private Color4 foregroundColor;

        private bool isMouseOver = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ButtonControl"/> class.
        /// </summary>
        /// <param name="parent">The parent control.</param>
        /// <param name="text">The text on the button.</param>
        /// <param name="size">Button size.</param>
        public ButtonControl(IElement parent, string text = "", SizeF? size = null)
            : base(parent)
        {
            this.textFormat = this.dwriteFactory.CreateTextFormat("Segoe UI", Helpers.GetFontSize(10));
            this.textFormat.ParagraphAlignment = DWrite.ParagraphAlignment.Center;
            this.textFormat.TextAlignment = DWrite.TextAlignment.Center;
            this.textFormat.WordWrapping = DWrite.WordWrapping.NoWrap;

            this.text = text;

            if (size != null)
            {
                this.Size = size.Value;
                this.Layout();
            }

            this.BackgroundColor = new Color4(1, 1, 1, 1);
            this.ForegroundColor = new Color4(0, 0, 0, 1);
        }

        /// <summary>
        /// On click event handler type.
        /// </summary>
        public delegate void ButtonClickHandler();

        /// <summary>
        /// Gets or sets the text.
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
        /// Gets or sets the foreground color.
        /// </summary>
        public Color4 ForegroundColor
        {
            get
            {
                return this.foregroundColor;
            }

            set
            {
                this.foregroundColor = value;

                this.foregroundBrush?.Dispose();
                this.foregroundBrush = this.DeviceContext.CreateSolidColorBrush(this.foregroundColor);

                this.Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the background color.
        /// </summary>
        public Color4 BackgroundColor
        {
            get
            {
                return this.backgroundColor;
            }

            set
            {
                this.backgroundColor = value;

                this.backgroundBrush?.Dispose();
                this.backgroundBrush = this.DeviceContext.CreateSolidColorBrush(this.backgroundColor);

                this.Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the click event.
        /// </summary>
        public ButtonClickHandler Click { get; set; }

        /// <inheritdoc />
        public override void Layout()
        {
            base.Layout();

            this.textLayout?.Dispose();
            this.textLayout = this.dwriteFactory.CreateTextLayout(this.text, this.textFormat, this.Size.Width, this.Size.Height);
        }

        /// <inheritdoc />
        public override void OnMouseEnter(MouseEvent e)
        {
            base.OnMouseEnter(e);
            this.isMouseOver = true;
            this.Invalidate();
        }

        /// <inheritdoc />
        public override void OnMouseLeave(MouseEvent e)
        {
            base.OnMouseLeave(e);
            this.isMouseOver = false;
            this.Invalidate();
        }

        /// <inheritdoc />
        public override void OnMouseClick(MouseEvent e)
        {
            base.OnMouseClick(e);
            this.Click?.Invoke();
        }

        /// <inheritdoc />
        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            this.textFormat.Dispose();
            this.textLayout?.Dispose();
            this.foregroundBrush?.Dispose();
            this.backgroundBrush?.Dispose();
            this.dwriteFactory.Dispose();
        }

        /// <inheritdoc />
        protected override void Draw()
        {
            this.DeviceContext.BeginDraw();
            if (!this.isMouseOver)
            {
                var transparentBackgroundColor = new Color4(this.backgroundColor.R, this.backgroundColor.G, this.backgroundColor.B, 0);
                this.DeviceContext.Clear(transparentBackgroundColor);
                this.DeviceContext.DrawTextLayout(this.origin, this.textLayout, this.foregroundBrush);
            }
            else
            {
                this.DeviceContext.Clear(this.ForegroundColor);
                this.DeviceContext.DrawTextLayout(this.origin, this.textLayout, this.backgroundBrush);
            }

            this.DeviceContext.EndDraw();
        }
    }
}
