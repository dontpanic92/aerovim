// <copyright file="DefaultRedrawEventFactory.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient;

using AeroVim.NeovimClient.Events;

/// <summary>
/// Represents a rendering session.
/// </summary>
public class DefaultRedrawEventFactory : IRedrawEventFactory<IRedrawEvent>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRedrawEventFactory"/> class.
    /// </summary>
    public DefaultRedrawEventFactory()
    {
    }

    /// <inheritdoc />
    public IRedrawEvent CreateClearEvent()
    {
        return new ClearEvent();
    }

    /// <inheritdoc />
    public IRedrawEvent CreateEolClearEvent()
    {
        return new EolClearEvent();
    }

    /// <inheritdoc />
    public IRedrawEvent CreateCursorGotoEvent(uint row, uint col)
    {
        return new CursorGotoEvent(row, col);
    }

    /// <inheritdoc />
    public IRedrawEvent CreateModeChangeEvent(string name, int index)
    {
        return new ModeChangeEvent(name, index);
    }

    /// <inheritdoc />
    public IRedrawEvent CreateModeInfoSetEvent(bool cursorStyleEnabled, IList<IDictionary<string, string>> modeInfo)
    {
        return new ModeInfoSetEvent(cursorStyleEnabled, modeInfo);
    }

    /// <inheritdoc />
    public IRedrawEvent CreatePutEvent(IList<string?> text)
    {
        return new PutEvent(text);
    }

    /// <inheritdoc />
    public IRedrawEvent CreateSetIconTitleEvent(string title)
    {
        return new SetIconTitleEvent(title);
    }

    /// <inheritdoc />
    public IRedrawEvent CreateSetTitleEvent(string title)
    {
        return new SetTitleEvent(title);
    }

    /// <inheritdoc />
    public IRedrawEvent CreateResizeEvent(uint row, uint col)
    {
        return new ResizeEvent(row, col);
    }

    /// <inheritdoc />
    public IRedrawEvent CreateHightlightSetEvent(int? foreground, int? background, int? special, bool reverse, bool italic, bool bold, bool underline, bool undercurl)
    {
        return new HighlightSetEvent(foreground, background, special, reverse, italic, bold, underline, undercurl);
    }

    /// <inheritdoc />
    public IRedrawEvent CreateUpdateFgEvent(int color)
    {
        return new UpdateFgEvent(color);
    }

    /// <inheritdoc />
    public IRedrawEvent CreateUpdateBgEvent(int color)
    {
        return new UpdateBgEvent(color);
    }

    /// <inheritdoc />
    public IRedrawEvent CreateUpdateSpEvent(int color)
    {
        return new UpdateSpEvent(color);
    }

    /// <inheritdoc />
    public IRedrawEvent CreateSetScrollRegionEvent(int top, int bottom, int left, int right)
    {
        return new SetScrollRegionEvent(top, bottom, left, right);
    }

    /// <inheritdoc />
    public IRedrawEvent CreateScrollEvent(int count)
    {
        return new ScrollEvent(count);
    }

    /// <inheritdoc />
    public IRedrawEvent CreateOptionSetEvent(string name, string value)
    {
        switch (name)
        {
            case "guifont":
                return new GuiFontEvent(value);
        }

        return new NopEvent();
    }
}
