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

    /// <inheritdoc />
    public IRedrawEvent CreateMouseOnEvent()
    {
        return new MouseOnEvent();
    }

    /// <inheritdoc />
    public IRedrawEvent CreateMouseOffEvent()
    {
        return new MouseOffEvent();
    }

    /// <inheritdoc />
    public IRedrawEvent CreateHlAttrDefineEvent(int id, AeroVim.Editor.Utilities.HighlightAttributes rgbAttrs)
    {
        return new HlAttrDefineEvent(id, rgbAttrs);
    }

    /// <inheritdoc />
    public IRedrawEvent CreateDefaultColorsSetEvent(int rgbFg, int rgbBg, int rgbSp, int ctermFg, int ctermBg)
    {
        return new DefaultColorsSetEvent(rgbFg, rgbBg, rgbSp, ctermFg, ctermBg);
    }

    /// <inheritdoc />
    public IRedrawEvent CreateGridResizeEvent(int grid, int width, int height)
    {
        return new GridResizeEvent(grid, width, height);
    }

    /// <inheritdoc />
    public IRedrawEvent CreateGridLineEvent(int grid, int row, int colStart, GridLineCell[] cells, bool wrap)
    {
        return new GridLineEvent(grid, row, colStart, cells, wrap);
    }

    /// <inheritdoc />
    public IRedrawEvent CreateGridClearEvent(int grid)
    {
        return new GridClearEvent(grid);
    }

    /// <inheritdoc />
    public IRedrawEvent CreateGridCursorGotoEvent(int grid, int row, int col)
    {
        return new GridCursorGotoEvent(grid, row, col);
    }

    /// <inheritdoc />
    public IRedrawEvent CreateGridScrollEvent(int grid, int top, int bot, int left, int right, int rows, int cols)
    {
        return new GridScrollEvent(grid, top, bot, left, right, rows, cols);
    }

    /// <inheritdoc />
    public IRedrawEvent CreateFlushEvent()
    {
        return new FlushEvent();
    }

    /// <inheritdoc />
    public IRedrawEvent CreatePopupmenuShowEvent(AeroVim.Editor.PopupMenuItem[] items, int selected, int row, int col, int grid)
    {
        return new PopupmenuShowEvent(items, selected, row, col, grid);
    }

    /// <inheritdoc />
    public IRedrawEvent CreatePopupmenuSelectEvent(int selected)
    {
        return new PopupmenuSelectEvent(selected);
    }

    /// <inheritdoc />
    public IRedrawEvent CreatePopupmenuHideEvent()
    {
        return new PopupmenuHideEvent();
    }

    /// <inheritdoc />
    public IRedrawEvent CreateCmdlineShowEvent(
        IList<(int HlId, string Text)> content,
        int pos,
        string firstc,
        string prompt,
        int indent,
        int level)
    {
        return new CmdlineShowEvent(content, pos, firstc, prompt, indent, level);
    }

    /// <inheritdoc />
    public IRedrawEvent CreateCmdlinePosEvent(int pos, int level)
    {
        return new CmdlinePosEvent(pos, level);
    }

    /// <inheritdoc />
    public IRedrawEvent CreateCmdlineHideEvent()
    {
        return new CmdlineHideEvent();
    }
}
