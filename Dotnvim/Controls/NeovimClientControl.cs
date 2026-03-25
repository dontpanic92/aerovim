using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using Dotnvim.Winforms.Controls.Rendering;
using Vortice.Mathematics;
using static Dotnvim.NeovimClient.NeovimClient;
using D2D = Vortice.Direct2D1;

namespace Dotnvim.Winforms.Controls
{
    public class NeovimClientControl : ControlBase
    {
        private readonly NeovimRenderer neovimRenderer;
        private readonly NeovimClient.NeovimClient neovimClient;

        private Rect boundary;
        private volatile RedrawArgs args = null;

        public NeovimClientControl(IControl parent, string neovimPath)
            : base(parent)
        {
            this.neovimRenderer = new NeovimRenderer(parent.Factory, parent.Device, true);

            this.neovimClient = new NeovimClient.NeovimClient(@"C:\Users\lishengq\Downloads\nvim-win64\Neovim\bin\nvim.exe");
            this.neovimClient.Redraw += this.OnNeovimRedraw;
            this.neovimClient.NeovimExited += this.NeovimExited;
        }

        public override Rect Boundary
        {
            get
            {
                return this.boundary;
            }

            set
            {
                this.boundary = value;
                this.neovimRenderer.Resize(new SizeF(value.Right - value.Left, value.Bottom - value.Top));
                this.neovimClient.TryResize(this.neovimRenderer.DesiredColCount, this.neovimRenderer.DesiredRowCount);
            }
        }

        public NeovimExitedHandler NeovimExited { get; set; }

        public TitleChangedHandler NeovimTitleChanged { get; set; }

        private void OnNeovimRedraw(RedrawArgs args)
        {
            this.args = args;
            this.Invalidate();
        }

        public override void Draw(D2D.ID2D1DeviceContext deviceContext)
        {
            this.neovimRenderer.Draw(deviceContext, this.Boundary);
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.IsDisposed)
            {
                this.neovimClient.Dispose();
                this.neovimRenderer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
