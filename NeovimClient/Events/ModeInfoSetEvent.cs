// <copyright file="ModeInfoSetEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events
{
    using System.Collections.Generic;
    using System.Linq;
    using AeroVim.NeovimClient.Utilities;

    /// <summary>
    /// The ModeInfoSet event.
    /// </summary>
    public class ModeInfoSetEvent : IRedrawEvent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModeInfoSetEvent"/> class.
        /// </summary>
        /// <param name="cursorStyleEnabled">Whether the cursor style needs to be enabled.</param>
        /// <param name="modeInfo">A list of available mode info.</param>
        public ModeInfoSetEvent(bool cursorStyleEnabled, IList<IDictionary<string, string>> modeInfo)
        {
            this.CursorStyleEnabled = cursorStyleEnabled;
            this.ModeInfo = modeInfo.Select(info => new ModeInfo(info)).ToList();
        }

        /// <summary>
        /// Gets a value indicating whether the the cursor style needs to be enabled.
        /// </summary>
        public bool CursorStyleEnabled { get; }

        /// <summary>
        /// Gets the list of mode info.
        /// </summary>
        public IList<ModeInfo> ModeInfo { get; }
    }
}
