// <copyright file="Form1.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Drawing;
    using System.Numerics;
    using System.Windows.Forms;
    using Dotnvim.Controls;
    using Dotnvim.Events;
    using Dotnvim.Utilities;
    using Vortice.Mathematics;
    using Color = System.Drawing.Color;
    using D2D = Vortice.Direct2D1;
    using D3D11 = Vortice.Direct3D11;
    using Size = System.Drawing.Size;
    using SizeF = System.Drawing.SizeF;

    /// <summary>
    /// The Mainform.
    /// </summary>
    public partial class Form1 : Form, IElement
    {
        private const float TitleBarHeight = 28;
        private const float BorderWidth = 6.5f;
        private const float DwmBorderSize = 1;

        private FormRenderer renderer;
        private int backgroundColor = 0;
        private NeovimClient.NeovimClient neovimClient;
        private VerticalLayout layout;
        private NeovimControl neovimControl;
        private LogoControl logoControl;
        private TitleControl titleControl;
        private ButtonControl settingsButton;
        private ButtonControl minimizeButton;
        private ButtonControl maximizeButton;
        private ButtonControl closeButton;

        private Size formerSize;
        private List<IElement> renderElements;
        private Size cachedTitleBarSize;
        private Size cachedBorderSize;
        private SizeF cachedDpi;

        /// <summary>
        /// Initializes a new instance of the <see cref="Form1"/> class.
        /// </summary>
        public Form1()
        {
            this.InitializeComponent();

            while (true)
            {
                try
                {
                    this.neovimClient = new NeovimClient.NeovimClient(Properties.Settings.Default.NeovimPath);
                    break;
                }
                catch (Exception)
                {
                    var dialog = new Dotnvim.Dialogs.SettingsDialog("Please specify the path to Neovim");
                    dialog.ShowDialog();
                    if (dialog.CloseReason == Dotnvim.Dialogs.SettingsDialog.Result.Cancel)
                    {
                        Environment.Exit(0);
                    }
                }
            }

            this.InitializeControls();

            var dwmBorderSize = Helpers.GetPixelSize(new SizeF(DwmBorderSize, DwmBorderSize), this.renderer.Dpi);
            NativeInterop.Methods.ExtendFrame(this.Handle, dwmBorderSize.Width, dwmBorderSize.Height);

            var initialColor = Helpers.GetColor(this.backgroundColor);
            this.BlurBehind(
                Color.FromArgb((int)(initialColor.A * 255), (int)(initialColor.R * 255), (int)(initialColor.G * 255), (int)(initialColor.B * 255)),
                Properties.Settings.Default.BackgroundOpacity,
                Properties.Settings.Default.BlurType);

            Properties.Settings.Default.PropertyChanged += this.Default_PropertyChanged;
        }

        /// <inheritdoc />
        public D2D.ID2D1Factory1 Factory => this.renderer.Factory;

        /// <inheritdoc />
        public D2D.ID2D1Device Device2D => this.renderer.Device2D;

        /// <inheritdoc />
        public D3D11.ID3D11Device Device => this.renderer.Device;

        /// <inheritdoc />
        SizeF IElement.Size { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <inheritdoc />
        Vector2 IElement.Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <inheritdoc />
        protected override CreateParams CreateParams
        {
            get
            {
                var param = base.CreateParams;
                param.ExStyle |= 0x00200000;

                // Remove WS_SYSMENU so DWM doesn't render native caption buttons
                // behind the backdrop. The app draws its own custom titlebar buttons.
                param.Style &= ~0x00080000;
                return param;
            }
        }

        /// <inheritdoc />
        protected override bool CanEnableIme => true;

        /// <summary>
        /// A control needs redrawing.
        /// </summary>
        /// <param name="control">The control that needs redrawing.</param>
        public void Invalidate(IElement control)
        {
            this.Invalidate();
        }

        /// <inheritdoc />
        void IElement.Draw(D2D.ID2D1DeviceContext deviceContext)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        void IElement.Layout()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        bool IElement.HitTest(Vector2 point)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        void IElement.OnMouseMove(MouseEvent e)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        void IElement.OnMouseEnter(MouseEvent e)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        void IElement.OnMouseLeave(MouseEvent e)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        void IElement.OnMouseClick(MouseEvent e)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Save window state
            Properties.WindowState.Default.Save();

            base.OnFormClosing(e);
            this.neovimClient.NeovimExited -= this.OnNeovimExited;
            this.neovimClient.Dispose();
            this.layout?.Dispose();
            this.renderer?.Dispose();
        }

        /// <inheritdoc />
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            KeyMapping.TryMap(e, out var text);
            if (!string.IsNullOrEmpty(text))
            {
                this.neovimClient.Input(text);
                e.Handled = true;
            }
        }

        /// <inheritdoc />
        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            this.OnResize();
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Load window size and state
            this.Size = Properties.WindowState.Default.WindowSize;
            this.WindowState = Properties.WindowState.Default.IsMaximized ? FormWindowState.Maximized : FormWindowState.Normal;
            this.OnResize();
        }

        /// <inheritdoc />
        protected override void OnPaint(PaintEventArgs e)
        {
            var backgroundColor = Helpers.GetColor(this.backgroundColor);
            if (Helpers.BlurBehindEnabled())
            {
                backgroundColor = new Color4(backgroundColor.R, backgroundColor.G, backgroundColor.B, (float)Properties.Settings.Default.BackgroundOpacity);
            }

            this.renderer.Draw(this.renderElements, backgroundColor, DwmBorderSize);
        }

        /// <inheritdoc />
        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case 0x24: // WM_GETMINMAXINFO
                    NativeInterop.Methods.WmGetMinMaxInfo(this.Handle, m.LParam);
                    return;
                case 0x83: // WM_NCCALCSIZE
                    m.Result = (IntPtr)0xF0;
                    return;
                case 0x84: // WM_NCHITTEST
                    m.Result = NativeInterop.Methods.NCHitTest(
                        this.Handle,
                        m.LParam,
                        this.cachedBorderSize.Width,
                        this.cachedBorderSize.Height,
                        this.WindowState == FormWindowState.Maximized ? this.cachedTitleBarSize.Height : this.cachedTitleBarSize.Height - this.cachedBorderSize.Height,
                        (int x, int y) =>
                        {
                            if (this.renderer != null)
                            {
                                var point = Helpers.GetDipPoint(x, y, this.cachedDpi);
                                return this.settingsButton.HitTest(point)
                                    || this.minimizeButton.HitTest(point)
                                    || this.maximizeButton.HitTest(point)
                                    || this.closeButton.HitTest(point);
                            }

                            return false;
                        });

                    return;
                case 0x02E0: // WM_DPICHANGED
                    this.UpdateCachedDpiValues();
                    break;
                case 0x0286: // WM_IME_CHAR
                    char ch = (char)m.WParam.ToInt64();
                    this.neovimControl.Input(ch.ToString());
                    break;
            }

            base.WndProc(ref m);

            switch (m.Msg)
            {
                case 0x112: // WM_SYSCOMMAND
                    int wParam = m.WParam.ToInt32() & 0xFFF0;
                    if (wParam == 0xF030 || wParam == 0xF020 || wParam == 0xF120)
                    {
                        // wParam == 0xF000 || SC_MAXIMIZE || SC_MINIMIZE || SC_RESTORE
                        this.OnResize();
                    }

                    break;
                case 0xA3: // WM_NCLBUTTONDBLCLK
                    this.OnResize();
                    break;
            }
        }

        /// <inheritdoc />
        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        /// <inheritdoc />
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            MouseEvent.Buttons button = MouseEvent.Buttons.None;
            switch (e.Button)
            {
                case MouseButtons.Left:
                    button = MouseEvent.Buttons.Left;
                    break;
                case MouseButtons.Right:
                    button = MouseEvent.Buttons.Right;
                    break;
            }

            var point = Helpers.GetDipPoint(e.X, e.Y, this.renderer.Factory.GetDesktopDpi());
            var mouseEvent = new MouseEvent(MouseEvent.Type.MouseMove, point, button);
            this.layout.OnMouseMove(mouseEvent);
        }

        /// <inheritdoc />
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            var point = Helpers.GetDipPoint(-1, -1, this.renderer.Factory.GetDesktopDpi());
            var mouseEvent = new MouseEvent(MouseEvent.Type.MouseMove, point, MouseEvent.Buttons.None);
            this.layout.OnMouseLeave(mouseEvent);
        }

        /// <inheritdoc />
        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            if (e.Button == MouseButtons.Left)
            {
                var point = Helpers.GetDipPoint(e.X, e.Y, this.renderer.Factory.GetDesktopDpi());
                var mouseEvent = new MouseEvent(MouseEvent.Type.MouseClick, point, MouseEvent.Buttons.Left);
                this.layout.OnMouseClick(mouseEvent);
            }
        }

        private void OnNeovimExited(int exitCode)
        {
            this.BeginInvoke(new MethodInvoker(() =>
            {
                this.Close();
            }));
        }

        private void OnResize()
        {
            if (this.Size == this.formerSize)
            {
                return;
            }

            if (this.WindowState == FormWindowState.Maximized)
            {
                Properties.WindowState.Default.IsMaximized = true;
            }
            else
            {
                Properties.WindowState.Default.IsMaximized = false;
                Properties.WindowState.Default.WindowSize = this.Size;
            }

            this.formerSize = this.Size;

            if (this.renderer != null)
            {
                this.renderer.Resize();

                if (this.WindowState == FormWindowState.Maximized)
                {
                    this.layout.Padding = 6.5f;
                }
                else
                {
                    this.layout.Padding = DwmBorderSize;
                }

                this.layout.Size = Helpers.GetDipSize(new Size(this.Width, this.Height), this.renderer.Factory.GetDesktopDpi());
                this.layout.Layout();
                this.Invalidate();
            }
        }

        private void Default_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Properties.Settings.EnableBlurBehind):
                case nameof(Properties.Settings.Default.BlurType):
                    var color = Helpers.GetColor(this.backgroundColor);
                    this.BlurBehind(
                        Color.FromArgb((int)(color.A * 255), (int)(color.R * 255), (int)(color.G * 255), (int)(color.B * 255)),
                        Properties.Settings.Default.BackgroundOpacity,
                        Properties.Settings.Default.BlurType);
                    this.Invalidate();
                    break;
                case nameof(Properties.Settings.BackgroundOpacity):
                    this.Invalidate();
                    break;
                case nameof(Properties.Settings.EnableLigature):
                    this.neovimControl.EnableLigature = Properties.Settings.Default.EnableLigature;
                    this.Invalidate();
                    break;
            }
        }

        private void InitializeControls()
        {
            this.renderer = new FormRenderer(this);
            this.layout = new VerticalLayout(this)
            {
                Size = Helpers.GetDipSize(new Size(this.Width, this.Height), this.renderer.Factory.GetDesktopDpi()),
            };

            this.neovimClient.NeovimExited += this.OnNeovimExited;

            this.neovimControl = new NeovimControl(this.layout, this.neovimClient);
            this.neovimControl.EnableLigature = Properties.Settings.Default.EnableLigature;

            this.neovimClient.BackgroundColorChanged += (int intColor) =>
            {
                this.backgroundColor = intColor;
                this.Invoke(new MethodInvoker(() =>
                {
                    var color = Helpers.GetColor(intColor);
                    this.BlurBehind(
                        Color.FromArgb((int)(color.A * 255), (int)(color.R * 255), (int)(color.G * 255), (int)(color.B * 255)),
                        Properties.Settings.Default.BackgroundOpacity,
                        Properties.Settings.Default.BlurType);
                }));
            };

            var buttonSize = Helpers.GetDipSize(
                new Size(SystemInformation.CaptionButtonSize.Width, SystemInformation.CaptionButtonSize.Height),
                this.renderer.Factory.GetDesktopDpi());

            var titleBar = new HorizontalLayout(this.layout)
            {
                Size = new SizeF(1, TitleBarHeight),
            };
            this.logoControl = new LogoControl(titleBar);
            this.titleControl = new TitleControl(titleBar);
            this.settingsButton = new ButtonControl(titleBar, "⚙", buttonSize)
            {
                Click = () =>
                {
                    var dialog = new Dotnvim.Dialogs.SettingsDialog();
                    dialog.ShowDialog();
                },
            };
            this.minimizeButton = new ButtonControl(titleBar, "🗕", buttonSize)
            {
                Click = () =>
                {
                    this.WindowState = FormWindowState.Minimized;
                    this.OnResize();
                },
            };
            this.maximizeButton = new ButtonControl(titleBar, "🗖", buttonSize)
            {
                Click = () =>
                {
                    this.WindowState =
                       this.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
                    this.OnResize();
                },
            };
            this.closeButton = new ButtonControl(titleBar, "✕", buttonSize)
            {
                Click = this.Close,
            };
            titleBar.AddControl(this.logoControl);
            titleBar.AddControl(this.titleControl, true);
            titleBar.AddControl(this.settingsButton);
            titleBar.AddControl(this.minimizeButton);
            titleBar.AddControl(this.maximizeButton);
            titleBar.AddControl(this.closeButton);

            this.neovimClient.TitleChanged += (string title) =>
            {
                this.BeginInvoke(new MethodInvoker(() =>
                {
                    this.Text = title;
                }));
                this.titleControl.Text = title;
            };

            this.neovimClient.ForegroundColorChanged += (int intColor) =>
            {
                var color = Helpers.GetColor(intColor);
                this.titleControl.Color = color;
                this.settingsButton.ForegroundColor = color;
                this.minimizeButton.ForegroundColor = color;
                this.maximizeButton.ForegroundColor = color;
                this.closeButton.ForegroundColor = color;
            };

            this.neovimClient.BackgroundColorChanged += (int intColor) =>
            {
                var color = Helpers.GetColor(intColor);
                this.settingsButton.BackgroundColor = color;
                this.minimizeButton.BackgroundColor = color;
                this.maximizeButton.BackgroundColor = color;
                this.closeButton.BackgroundColor = color;
            };

            this.layout.AddControl(titleBar);
            this.layout.AddControl(this.neovimControl, true);
            this.layout.Layout();

            this.renderElements = new List<IElement> { this.layout };
            this.UpdateCachedDpiValues();
        }

        private void UpdateCachedDpiValues()
        {
            this.cachedDpi = this.renderer.Factory.GetDesktopDpi();
            this.cachedTitleBarSize = Helpers.GetPixelSize(new SizeF(1, TitleBarHeight), this.cachedDpi);
            this.cachedBorderSize = Helpers.GetPixelSize(new SizeF(BorderWidth, BorderWidth), this.cachedDpi);
        }
    }
}
