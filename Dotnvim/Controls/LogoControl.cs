// <copyright file="LogoControl.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls
{
    using System.Drawing;
    using System.Drawing.Imaging;
    using Dotnvim.Utilities;
    using Vortice.Mathematics;
    using D2D = Vortice.Direct2D1;
    using DXGI = Vortice.DXGI;
    using Size = System.Drawing.Size;
    using SizeF = System.Drawing.SizeF;

    /// <summary>
    /// The Logo control.
    /// </summary>
    public class LogoControl : ControlBase
    {
        private const float VerticalPadding = 3;
        private const float HorinzontalPadding = 8;
        private readonly D2D.ID2D1Bitmap bitmap;
        private readonly float ratio;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogoControl"/> class.
        /// </summary>
        /// <param name="parent">The parent control.</param>
        public LogoControl(IElement parent)
            : base(parent)
        {
            var image = Properties.Resources.neovim_logo_flat;
            var size = new Rectangle(0, 0, image.Width, image.Height);
            var bitmapProperties = new D2D.BitmapProperties1(
                new Vortice.DCommon.PixelFormat(DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                this.Factory.GetDesktopDpi().Width,
                this.Factory.GetDesktopDpi().Height,
                D2D.BitmapOptions.None);

            var bitmapData = image.LockBits(size, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            this.bitmap = this.DeviceContext.CreateBitmap(
                new SizeI(image.Width, image.Height),
                bitmapData.Scan0,
                (uint)bitmapData.Stride,
                bitmapProperties);
            image.UnlockBits(bitmapData);

            this.ratio = (float)image.Width / image.Height;
        }

        /// <inheritdoc />
        public override void Layout()
        {
            base.Layout();

            this.Size = new SizeF(((this.Size.Height - (2 * VerticalPadding)) * this.ratio) + (2 * HorinzontalPadding), this.Size.Height);
        }

        /// <inheritdoc />
        protected override void Draw()
        {
            var dest = Rect.FromLTRB(
                HorinzontalPadding,
                VerticalPadding,
                this.Size.Width - HorinzontalPadding,
                this.Size.Height - VerticalPadding);

            this.DeviceContext.BeginDraw();
            this.DeviceContext.Clear(new Color4(0, 0, 0, 0));
            this.DeviceContext.DrawBitmap(
                this.bitmap,
                dest,
                1.0f,
                D2D.InterpolationMode.HighQualityCubic,
                null,
                null);
            this.DeviceContext.EndDraw();
        }

        /// <inheritdoc />
        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            this.bitmap.Dispose();
        }
    }
}
