// <copyright file="UI.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.API
{
    using System.Collections.Generic;

    /// <summary>
    /// The apis of UI part.
    /// </summary>
    public class UI
    {
        private MsgPackRpc msgPackRpc;

        /// <summary>
        /// Initializes a new instance of the <see cref="UI"/> class.
        /// </summary>
        /// <param name="msgPackRpc">The RPC client.</param>
        public UI(MsgPackRpc msgPackRpc)
        {
            this.msgPackRpc = msgPackRpc;
        }

        /// <summary>
        /// Attach to Neovim.
        /// </summary>
        /// <param name="width">The column count.</param>
        /// <param name="height">The row count.</param>
        public void Attach(uint width, uint height)
        {
            var options = new Dictionary<string, bool>()
            {
                ["rgb"] = true,
            };

            this.msgPackRpc.SendRequest("nvim_ui_attach", new List<object>() { width, height, options });
        }

        /// <summary>
        /// Try resize the window.
        /// </summary>
        /// <param name="width">The column count.</param>
        /// <param name="height">The row count.</param>
        public void TryResize(uint width, uint height)
        {
            this.msgPackRpc.SendRequest("nvim_ui_try_resize", new List<object>() { width, height });
        }
    }
}
