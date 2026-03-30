// <copyright file="ModeInfoSetEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

using AeroVim.Editor.Utilities;

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
        this.ModeInfo = modeInfo.Select(info => ParseModeInfo(info)).ToList();
    }

    /// <summary>
    /// Gets a value indicating whether the the cursor style needs to be enabled.
    /// </summary>
    public bool CursorStyleEnabled { get; }

    /// <summary>
    /// Gets the list of mode info.
    /// </summary>
    public IList<ModeInfo> ModeInfo { get; }

    private static ModeInfo ParseModeInfo(IDictionary<string, string> info)
    {
        var cursorShape = CursorShape.Block;
        if (info.TryGetValue("cursor_shape", out var cursorShapeStr))
        {
            if (System.Enum.TryParse<CursorShape>(cursorShapeStr, true, out var shape))
            {
                cursorShape = shape;
            }
        }

        int cellPercentage = 100;
        if (info.TryGetValue("cell_percentage", out var cellPercentageStr))
        {
            if (int.TryParse(cellPercentageStr, out var percentage))
            {
                cellPercentage = percentage;
            }
        }

        var cursorBlinking = CursorBlinking.BlinkOff;
        if (info.TryGetValue("blinkwait", out var wait) && int.TryParse(wait, out var intWait) && intWait == 1)
        {
            cursorBlinking = CursorBlinking.BlinkWait;
        }
        else if (info.TryGetValue("blinkon", out var on) && int.TryParse(on, out var intOn) && intOn == 1)
        {
            cursorBlinking = CursorBlinking.BlinkOn;
        }

        return new ModeInfo(cursorShape, cellPercentage, cursorBlinking);
    }
}
