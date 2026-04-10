// <copyright file="UI.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.API;

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
    /// Connect to Neovim as an external UI.
    /// </summary>
    /// <param name="width">The column count.</param>
    /// <param name="height">The row count.</param>
    /// <param name="extCmdline">
    /// When <c>true</c>, requests the <c>ext_cmdline</c> extension so Neovim
    /// sends command-line events instead of rendering in the grid.
    /// </param>
    /// <param name="extPopupmenu">
    /// When <c>true</c>, requests the <c>ext_popupmenu</c> extension so Neovim
    /// sends popup menu events instead of rendering in the grid.
    /// </param>
    public void Attach(uint width, uint height, bool extCmdline = false, bool extPopupmenu = false)
    {
        var options = new Dictionary<string, bool>()
        {
            ["rgb"] = true,
            ["ext_linegrid"] = true,
        };

        if (extCmdline)
        {
            options["ext_cmdline"] = true;
        }

        if (extPopupmenu)
        {
            options["ext_popupmenu"] = true;
        }

        // UI connection is critical — observe the response so failures are visible.
        this.msgPackRpc.SendRequestFireAndForget("nvim_ui_attach", new List<object>() { width, height, options });
    }

    /// <summary>
    /// Try resize the window.
    /// </summary>
    /// <param name="width">The column count.</param>
    /// <param name="height">The row count.</param>
    public void TryResize(uint width, uint height)
    {
        this.msgPackRpc.SendRequestFireAndForget("nvim_ui_try_resize", new List<object>() { width, height });
    }
}
