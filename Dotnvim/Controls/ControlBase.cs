// <copyright file="ControlBase.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Numerics;
    using System.Text;
    using System.Threading.Tasks;
    using Dotnvim.Controls.Utilities;
    using Dotnvim.Utilities;
    using Vortice.Mathematics;
    using D2D = Vortice.Direct2D1;
    using D3D = Vortice.Direct3D;
    using D3D11 = Vortice.Direct3D11;
    using DWrite = Vortice.DirectWrite;
    using DXGI = Vortice.DXGI;
    using Size = System.Drawing.Size;
    using SizeF = System.Drawing.SizeF;

    /// <summary>
    /// The base class for controls.
    /// </summary>
    public abstract class ControlBase : ElementBase
    {
        private D2D.ID2D1Bitmap1 backBitmap;

        /// <summary>
        /// Initializes a new instance of the <see cref="ControlBase"/> class.
        /// </summary>
        /// <param name="parent">The parent control.</param>
        public ControlBase(IElement parent)
            : base(parent)
        {
            this.DeviceContext = this.Device2D.CreateDeviceContext(D2D.DeviceContextOptions.EnableMultithreadedOptimizations);
            var desktopDpi = this.Factory.GetDesktopDpi();
            this.DeviceContext.SetDpi(desktopDpi.Width, desktopDpi.Height);
            this.DeviceContext.AntialiasMode = D2D.AntialiasMode.PerPrimitive;
        }

        /// <summary>
        /// Gets the device context.
        /// </summary>
        protected D2D.ID2D1DeviceContext DeviceContext { get; }

        /// <summary>
        /// Gets the post effects.
        /// </summary>
        protected virtual EffectChain PostEffects => null;

        /// <inheritdoc />
        public override void Draw(D2D.ID2D1DeviceContext deviceContext)
        {
            if (this.Size.Width == 0 || this.Size.Height == 0)
            {
                return;
            }

            if (this.backBitmap == null || new SizeF(this.backBitmap.Size.Width, this.backBitmap.Size.Height) != this.Size)
            {
                this.InitializeBackBuffer(deviceContext, this.Size);
            }

            this.Draw();

            var boundary = Rect.FromLTRB(
                this.Position.X,
                this.Position.Y,
                this.Position.X + this.Size.Width,
                this.Position.Y + this.Size.Height);

            if (this.PostEffects?.Any() == true)
            {
                this.PostEffects.SetInput(this.backBitmap);
                using (var output = this.PostEffects.Output)
                {
                    deviceContext.DrawImage(output, new Vector2(boundary.Left, boundary.Top), D2D.InterpolationMode.NearestNeighbor);
                }
            }
            else
            {
                deviceContext.DrawBitmap(this.backBitmap, boundary, 1.0f, D2D.InterpolationMode.NearestNeighbor, null, null);

                // deviceContext.DrawImage(this.backBitmap, new Vector2(boundary.Left, boundary.Top), D2D.InterpolationMode.NearestNeighbor);
            }
        }

        /// <summary>
        /// Invalidate this control.
        /// </summary>
        public void Invalidate()
        {
            this.Parent.Invalidate(this);
        }

        /// <inheritdoc />
        public override void Layout()
        {
        }

        /// <summary>
        /// Draw the control.
        /// </summary>
        protected abstract void Draw();

        /// <inheritdoc />
        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            this.backBitmap?.Dispose();
            this.DeviceContext.Dispose();
        }

        private void InitializeBackBuffer(D2D.ID2D1DeviceContext deviceContext, SizeF size)
        {
            this.backBitmap?.Dispose();

            Size pixelSize = Helpers.GetPixelSize(size, this.Factory.GetDesktopDpi());

            var p = new D2D.BitmapProperties1(
                new Vortice.DCommon.PixelFormat(DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                this.Factory.GetDesktopDpi().Width,
                this.Factory.GetDesktopDpi().Height,
                D2D.BitmapOptions.Target);

            var desc = new D3D11.Texture2DDescription()
            {
                ArraySize = 1,
                BindFlags = D3D11.BindFlags.RenderTarget | D3D11.BindFlags.ShaderResource,
                CPUAccessFlags = D3D11.CpuAccessFlags.None,
                Format = DXGI.Format.B8G8R8A8_UNorm,
                MipLevels = 1,
                MiscFlags = D3D11.ResourceOptionFlags.Shared,
                Usage = D3D11.ResourceUsage.Default,
                SampleDescription = new DXGI.SampleDescription(1, 0),
                Width = (uint)pixelSize.Width,
                Height = (uint)pixelSize.Height,
            };

            using (var buffer = this.Device.CreateTexture2D(desc))
            using (var surface = buffer.QueryInterface<DXGI.IDXGISurface>())
            {
                this.backBitmap = this.DeviceContext.CreateBitmapFromDxgiSurface(surface, p);
            }

            this.DeviceContext.Target = this.backBitmap;
        }
    }
}
