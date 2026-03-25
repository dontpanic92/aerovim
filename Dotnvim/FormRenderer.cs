// <copyright file="FormRenderer.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Windows.Forms;
    using Dotnvim.Controls;
    using Dotnvim.Utilities;
    using Vortice.Mathematics;
    using D2D = Vortice.Direct2D1;
    using D3D = Vortice.Direct3D;
    using D3D11 = Vortice.Direct3D11;
    using DComp = Vortice.DirectComposition;
    using DWrite = Vortice.DirectWrite;
    using DXGI = Vortice.DXGI;

    /// <summary>
    /// The renderer to render a form.
    /// </summary>
    public sealed class FormRenderer : IDisposable
    {
        private readonly DXGI.IDXGISwapChain1 swapChain;
        private readonly D3D11.ID3D11Device device;
        private readonly D2D.ID2D1Factory1 factory2d = D2D.D2D1.D2D1CreateFactory<D2D.ID2D1Factory1>();
        private readonly D2D.ID2D1Device device2d;
        private readonly DWrite.IDWriteFactory factoryDWrite = DWrite.DWrite.DWriteCreateFactory<DWrite.IDWriteFactory>();
        private readonly D2D.ID2D1DeviceContext deviceContext2D;
        private readonly DComp.IDCompositionDevice deviceComp;
        private readonly DComp.IDCompositionTarget compositionTarget;

#if DEBUG
        private readonly Vortice.Direct3D11.Debug.ID3D11Debug deviceDebug;
#endif
        private readonly Form1 form;

        private D3D11.ID3D11Texture2D backBuffer;
        private D3D11.ID3D11Texture2D renderBuffer;
        private D2D.ID2D1Bitmap1 backBitmap;
        private D2D.ID2D1Bitmap1 renderBitmap;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormRenderer"/> class.
        /// </summary>
        /// <param name="form">The form.</param>
        public FormRenderer(Form1 form)
        {
            this.form = form;
#if DEBUG
            var creationFlags = D3D11.DeviceCreationFlags.BgraSupport | D3D11.DeviceCreationFlags.Debug;
            var debugFactory = true;
#else
            var creationFlags = D3D11.DeviceCreationFlags.BgraSupport;
            var debugFactory = false;
#endif

            D3D11.D3D11.D3D11CreateDevice(null, D3D.DriverType.Hardware, creationFlags, null, out var d3dDevice);
            this.device = d3dDevice;

#if DEBUG
            this.deviceDebug = this.device.QueryInterface<Vortice.Direct3D11.Debug.ID3D11Debug>();
#endif

            using (var dxgiDevice = this.device.QueryInterface<DXGI.IDXGIDevice>())
            {
                using (var dxgiFactory = DXGI.DXGI.CreateDXGIFactory2<DXGI.IDXGIFactory2>(debugFactory))
                {
                    var desc = new DXGI.SwapChainDescription1()
                    {
                        BufferCount = 2,
                        AlphaMode = DXGI.AlphaMode.Premultiplied,
                        SampleDescription = new DXGI.SampleDescription(1, 0),
                        BufferUsage = DXGI.Usage.RenderTargetOutput,
                        SwapEffect = DXGI.SwapEffect.FlipDiscard,
                        Format = DXGI.Format.B8G8R8A8_UNorm,
                        Width = (uint)form.Width,
                        Height = (uint)form.Height,
                    };

                    this.swapChain = dxgiFactory.CreateSwapChainForComposition(dxgiDevice, desc);

                    this.deviceComp = DComp.DComp.DCompositionCreateDevice<DComp.IDCompositionDevice>(dxgiDevice);
                    this.deviceComp.CreateTargetForHwnd(form.Handle, true, out var compTarget);
                    this.compositionTarget = compTarget;

                    using (var visual = this.deviceComp.CreateVisual())
                    {
                        visual.SetContent(this.swapChain);
                        this.compositionTarget.SetRoot(visual);
                    }

                    this.deviceComp.Commit();
                }
            }

            using (var dxgiDevice = this.device.QueryInterface<DXGI.IDXGIDevice>())
            {
                this.device2d = this.factory2d.CreateDevice(dxgiDevice);
            }

            this.deviceContext2D = this.device2d.CreateDeviceContext(D2D.DeviceContextOptions.None);
            var desktopDpi = this.factory2d.GetDesktopDpi();
            this.deviceContext2D.SetDpi(desktopDpi.Width, desktopDpi.Height);
            this.deviceContext2D.AntialiasMode = D2D.AntialiasMode.PerPrimitive;

            this.CreateResources();
        }

        /// <summary>
        /// Gets the Direct2D factory.
        /// </summary>
        public D2D.ID2D1Factory1 Factory => this.factory2d;

        /// <summary>
        /// Gets the Direct2D device.
        /// </summary>
        public D2D.ID2D1Device Device2D => this.device2d;

        /// <summary>
        /// Gets the Direc3D device.
        /// </summary>
        public D3D11.ID3D11Device Device => this.device;

        /// <summary>
        /// Gets the DesktopDpi.
        /// </summary>
        public SizeF Dpi => this.factory2d.GetDesktopDpi();

        /// <summary>
        /// Gets the Direct2D DeviceContext.
        /// </summary>
        public D2D.ID2D1DeviceContext DeviceContext2D => this.deviceContext2D;

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            this.ReleaseResources();
            this.renderBitmap?.Dispose();
            this.deviceContext2D.Dispose();
            this.device2d.Dispose();
            this.compositionTarget.Dispose();
            this.deviceComp.Dispose();
#if DEBUG
            this.deviceDebug.Dispose();
#endif
            this.device.Dispose();
            this.swapChain.Dispose();
            this.factory2d.Dispose();
            this.factoryDWrite.Dispose();
        }

        /// <summary>
        /// Draw the form.
        /// </summary>
        /// <param name="controls">The children controls.</param>
        /// <param name="backgroundColor">The background color.</param>
        /// <param name="dwmBorderSize">The dwm border size.</param>
        public void Draw(IList<IElement> controls, Color4 backgroundColor, float dwmBorderSize)
        {
            if (this.backBitmap == null)
            {
                return;
            }

            this.deviceContext2D.BeginDraw();
            this.deviceContext2D.Target = this.renderBitmap;

            var borderColor = new Color4(backgroundColor.R, backgroundColor.G, backgroundColor.B, 1);
            this.deviceContext2D.Clear(borderColor);

            var rect = Rect.FromLTRB(dwmBorderSize, dwmBorderSize, this.deviceContext2D.Size.Width - dwmBorderSize, this.deviceContext2D.Size.Height - dwmBorderSize);
            this.deviceContext2D.PushAxisAlignedClip(rect, D2D.AntialiasMode.Aliased);
            this.deviceContext2D.Clear(backgroundColor);

            foreach (var control in controls)
            {
                var boundary = Rect.FromLTRB(
                    control.Position.X,
                    control.Position.Y,
                    control.Position.X + control.Size.Width,
                    control.Position.Y + control.Size.Height);

                this.deviceContext2D.PushAxisAlignedClip(boundary, D2D.AntialiasMode.Aliased);
                control.Draw(this.deviceContext2D);
                this.deviceContext2D.PopAxisAlignedClip();
            }

            this.deviceContext2D.PopAxisAlignedClip();

            this.deviceContext2D.Target = null;
            this.deviceContext2D.EndDraw();

            this.backBitmap.CopyFromBitmap(this.renderBitmap);
            this.device.ImmediateContext.Flush();
            this.swapChain.Present(1, DXGI.PresentFlags.None);
        }

        /// <summary>
        /// Resize.
        /// </summary>
        public void Resize()
        {
            this.ReleaseResources();
            this.swapChain.ResizeBuffers(2, (uint)this.form.Width, (uint)this.form.Height, DXGI.Format.B8G8R8A8_UNorm, DXGI.SwapChainFlags.None);
            this.CreateResources();

            // this.deviceDebug.ReportLiveDeviceObjects(D3D11.ReportingLevel.Detail);
        }

        private void ReleaseResources()
        {
            this.backBuffer?.Dispose();
            this.backBitmap?.Dispose();
        }

        private void CreateResources()
        {
            this.backBuffer = this.swapChain.GetBuffer<D3D11.ID3D11Texture2D>(0);
            this.backBuffer.DebugName = "BackBuffer";

            var dpi = this.Dpi;
            using (var surface = this.backBuffer.QueryInterface<DXGI.IDXGISurface>())
            {
                var properties = new D2D.BitmapProperties1(
                    new Vortice.DCommon.PixelFormat(DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                    dpi.Width,
                    dpi.Height,
                    D2D.BitmapOptions.CannotDraw);

                this.backBitmap = this.deviceContext2D.CreateBitmapFromDxgiSurface(surface, properties);

                if (this.renderBitmap != null)
                {
                    this.backBitmap.CopyFromBitmap(this.renderBitmap);
                }
            }

            if (this.renderBitmap != null)
            {
                this.renderBitmap.Dispose();
                this.renderBuffer.Dispose();
            }

            var p = new D2D.BitmapProperties1(
                new Vortice.DCommon.PixelFormat(DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                dpi.Width,
                dpi.Height,
                D2D.BitmapOptions.Target);

            var bitmapSize = this.backBitmap.Size;
            var pixelSize = Helpers.GetPixelSize(new SizeF(bitmapSize.Width, bitmapSize.Height), dpi);

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

            this.renderBuffer = this.device.CreateTexture2D(desc);
            using (var surface = this.renderBuffer.QueryInterface<DXGI.IDXGISurface>())
            {
                this.renderBitmap = this.deviceContext2D.CreateBitmapFromDxgiSurface(surface, p);
            }

            this.renderBitmap.CopyFromBitmap(this.backBitmap);
        }
    }
}
