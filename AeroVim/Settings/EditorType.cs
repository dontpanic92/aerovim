// <copyright file="EditorType.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Settings
{
    /// <summary>
    /// The type of editor backend to use.
    /// </summary>
    public enum EditorType
    {
        /// <summary>
        /// Neovim (MsgPack RPC via --embed).
        /// </summary>
        Neovim = 0,

        /// <summary>
        /// Vim (PTY with VT escape sequences).
        /// </summary>
        Vim = 1,
    }
}
