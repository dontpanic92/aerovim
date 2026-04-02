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
}
