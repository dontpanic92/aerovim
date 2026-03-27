// <copyright file="Global.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.NeovimClient.API
{
    using System.Collections.Generic;

    /// <summary>
    /// Global API functions.
    /// </summary>
    public class Global
    {
        private readonly MsgPackRpc msgPackRpc;

        /// <summary>
        /// Initializes a new instance of the <see cref="Global"/> class.
        /// </summary>
        /// <param name="msgPackRpc">The RPC client.</param>
        public Global(MsgPackRpc msgPackRpc)
        {
            this.msgPackRpc = msgPackRpc;
        }

        /// <summary>
        /// Input keys.
        /// </summary>
        /// <param name="keys">String of keys.</param>
        public void Input(string keys)
        {
            this.msgPackRpc.SendRequest("nvim_input", new List<object>() { keys });
        }

        /// <summary>
        /// Writes an error message to the vim error buffer.
        /// </summary>
        /// <param name="message">The error message.</param>
        public void WriteErrorMessage(string message)
        {
            this.msgPackRpc.SendRequest("nvim_err_writeln", new List<object>() { message });
        }

        /// <summary>
        /// Set a global (g:) variable.
        /// </summary>
        /// <param name="name">Variable name.</param>
        /// <param name="value">Variable value.</param>
        public void SetGlobalVariable(string name, string value)
        {
            this.msgPackRpc.SendRequest("nvim_set_var", new List<object>() { name, value });
        }

        /// <summary>
        /// Send a mouse event to Neovim.
        /// </summary>
        /// <param name="button">Mouse button: "left", "right", "middle", "wheel", or "move".</param>
        /// <param name="action">Action: "press", "drag", "release" for buttons; "up", "down", "left", "right" for wheel.</param>
        /// <param name="modifier">Modifier keys string, e.g. "", "S", "C", "A", "C-S".</param>
        /// <param name="grid">Grid id (0 when multigrid is not enabled).</param>
        /// <param name="row">Zero-based grid row.</param>
        /// <param name="col">Zero-based grid column.</param>
        public void InputMouse(string button, string action, string modifier, int grid, int row, int col)
        {
            this.msgPackRpc.SendRequest("nvim_input_mouse", new List<object>() { button, action, modifier, grid, row, col });
        }

        /// <summary>
        /// Execute a Neovim command.
        /// </summary>
        /// <param name="command">The command string.</param>
        public void Command(string command)
        {
            this.msgPackRpc.SendRequest("nvim_command", new List<object>() { command });
        }
    }
}
