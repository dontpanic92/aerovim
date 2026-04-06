// <copyright file="IRedrawEventFactory.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient;

/// <summary>
/// Represents a rendering session.
/// </summary>
/// <typeparam name="TRedrawEvent">The base redraw event type.</typeparam>
public interface IRedrawEventFactory<TRedrawEvent>
{
    /// <summary>
    /// Create SetTitle event.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateSetTitleEvent(string title);

    /// <summary>
    /// Create SetIconTitle event.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateSetIconTitleEvent(string title);

    /// <summary>
    /// Create Clear event.
    /// </summary>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateClearEvent();

    /// <summary>
    /// Create EolClear event.
    /// </summary>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateEolClearEvent();

    /// <summary>
    /// Create ModeChange event.
    /// </summary>
    /// <param name="name">The name of this mode.</param>
    /// <param name="index">The index of this mode.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateModeChangeEvent(string name, int index);

    /// <summary>
    /// Create ModeInfoSet event.
    /// </summary>
    /// <param name="cursorStyleEnabled">whether cursor style is enabled.</param>
    /// <param name="modeInfo">The mode info.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateModeInfoSetEvent(bool cursorStyleEnabled, IList<IDictionary<string, string>> modeInfo);

    /// <summary>
    /// Create CursorGoto event.
    /// </summary>
    /// <param name="row">row.</param>
    /// <param name="col">column.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateCursorGotoEvent(uint row, uint col);

    /// <summary>
    /// Create Put event.
    /// </summary>
    /// <param name="text">Text.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreatePutEvent(IList<string?> text);

    /// <summary>
    /// Create CursorGoto event.
    /// </summary>
    /// <param name="row">row.</param>
    /// <param name="col">column.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateResizeEvent(uint row, uint col);

    /// <summary>
    /// Create HightlightSet event.
    /// </summary>
    /// <param name="foreground">Foreground color.</param>
    /// <param name="background">Background color.</param>
    /// <param name="special">Special color.</param>
    /// <param name="reverse">IsReverse.</param>
    /// <param name="italic">IsItalic.</param>
    /// <param name="bold">IsBold.</param>
    /// <param name="underline">IsUnderline.</param>
    /// <param name="undercurl">IsUnderCurl.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateHightlightSetEvent(int? foreground, int? background, int? special, bool reverse, bool italic, bool bold, bool underline, bool undercurl);

    /// <summary>
    /// Create UpdateFgEvent.
    /// </summary>
    /// <param name="color">Foreground color.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateUpdateFgEvent(int color);

    /// <summary>
    /// Create UpdateBgEvent.
    /// </summary>
    /// <param name="color">Background color.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateUpdateBgEvent(int color);

    /// <summary>
    /// Create UpdateSpEvent.
    /// </summary>
    /// <param name="color">Special color.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateUpdateSpEvent(int color);

    /// <summary>
    /// Create SetScrollRegion event.
    /// </summary>
    /// <param name="top">Top row in the region.</param>
    /// <param name="bottom">Bottom row in the region.</param>
    /// <param name="left">Leftmost col in the region.</param>
    /// <param name="right">Rightmost col in the region.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateSetScrollRegionEvent(int top, int bottom, int left, int right);

    /// <summary>
    /// Create Scroll Event.
    /// </summary>
    /// <param name="count">Row count to scroll.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateScrollEvent(int count);

    /// <summary>
    /// Create OptionSet event.
    /// </summary>
    /// <param name="name">The option name.</param>
    /// <param name="value">The option value.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateOptionSetEvent(string name, string value);

    /// <summary>
    /// Create MouseOn event.
    /// </summary>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateMouseOnEvent();

    /// <summary>
    /// Create MouseOff event.
    /// </summary>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateMouseOffEvent();

    /// <summary>
    /// Create HlAttrDefine event.
    /// </summary>
    /// <param name="id">The highlight ID.</param>
    /// <param name="rgbAttrs">The RGB highlight attributes.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateHlAttrDefineEvent(int id, AeroVim.Editor.Utilities.HighlightAttributes rgbAttrs);

    /// <summary>
    /// Create DefaultColorsSet event.
    /// </summary>
    /// <param name="rgbFg">Default RGB foreground color.</param>
    /// <param name="rgbBg">Default RGB background color.</param>
    /// <param name="rgbSp">Default RGB special color.</param>
    /// <param name="ctermFg">Default cterm foreground color code.</param>
    /// <param name="ctermBg">Default cterm background color code.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateDefaultColorsSetEvent(int rgbFg, int rgbBg, int rgbSp, int ctermFg, int ctermBg);

    /// <summary>
    /// Create GridResize event.
    /// </summary>
    /// <param name="grid">The grid identifier.</param>
    /// <param name="width">Column count.</param>
    /// <param name="height">Row count.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateGridResizeEvent(int grid, int width, int height);

    /// <summary>
    /// Create GridLine event.
    /// </summary>
    /// <param name="grid">The grid identifier.</param>
    /// <param name="row">The row index.</param>
    /// <param name="colStart">The starting column.</param>
    /// <param name="cells">The cell data.</param>
    /// <param name="wrap">Whether this line wraps.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateGridLineEvent(int grid, int row, int colStart, Events.GridLineCell[] cells, bool wrap);

    /// <summary>
    /// Create GridClear event.
    /// </summary>
    /// <param name="grid">The grid identifier.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateGridClearEvent(int grid);

    /// <summary>
    /// Create GridCursorGoto event.
    /// </summary>
    /// <param name="grid">The grid identifier.</param>
    /// <param name="row">The row index.</param>
    /// <param name="col">The column index.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateGridCursorGotoEvent(int grid, int row, int col);

    /// <summary>
    /// Create GridScroll event.
    /// </summary>
    /// <param name="grid">The grid identifier.</param>
    /// <param name="top">Top row (inclusive).</param>
    /// <param name="bot">Bottom row (exclusive).</param>
    /// <param name="left">Left column (inclusive).</param>
    /// <param name="right">Right column (exclusive).</param>
    /// <param name="rows">Rows to scroll.</param>
    /// <param name="cols">Columns to scroll (reserved).</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateGridScrollEvent(int grid, int top, int bot, int left, int right, int rows, int cols);

    /// <summary>
    /// Create Flush event.
    /// </summary>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateFlushEvent();

    /// <summary>
    /// Create PopupmenuShow event.
    /// </summary>
    /// <param name="items">The completion items.</param>
    /// <param name="selected">The initially selected item index.</param>
    /// <param name="row">The anchor row.</param>
    /// <param name="col">The anchor column.</param>
    /// <param name="grid">The anchor grid.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreatePopupmenuShowEvent(AeroVim.Editor.PopupMenuItem[] items, int selected, int row, int col, int grid);

    /// <summary>
    /// Create PopupmenuSelect event.
    /// </summary>
    /// <param name="selected">The selected item index.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreatePopupmenuSelectEvent(int selected);

    /// <summary>
    /// Create PopupmenuHide event.
    /// </summary>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreatePopupmenuHideEvent();

    /// <summary>
    /// Create CmdlineShow event.
    /// </summary>
    /// <param name="content">The content chunks.</param>
    /// <param name="pos">The cursor byte position.</param>
    /// <param name="firstc">The first character.</param>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="indent">The indent level.</param>
    /// <param name="level">The nesting level.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateCmdlineShowEvent(
        IList<(int HlId, string Text)> content,
        int pos,
        string firstc,
        string prompt,
        int indent,
        int level);

    /// <summary>
    /// Create CmdlinePos event.
    /// </summary>
    /// <param name="pos">The cursor byte position.</param>
    /// <param name="level">The nesting level.</param>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateCmdlinePosEvent(int pos, int level);

    /// <summary>
    /// Create CmdlineHide event.
    /// </summary>
    /// <returns>The created redraw event.</returns>
    TRedrawEvent CreateCmdlineHideEvent();
}
