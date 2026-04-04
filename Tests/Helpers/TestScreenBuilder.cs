// <copyright file="TestScreenBuilder.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests.Helpers;

using AeroVim.Editor;

/// <summary>
/// Creates deterministic screen fixtures for rendering and client tests.
/// </summary>
internal static class TestScreenBuilder
{
    /// <summary>
    /// Creates a screen filled with blank cells.
    /// </summary>
    /// <param name="rows">Row count.</param>
    /// <param name="cols">Column count.</param>
    /// <param name="foreground">Default foreground color.</param>
    /// <param name="background">Default background color.</param>
    /// <returns>The created screen.</returns>
    public static Screen CreateScreen(int rows, int cols, int foreground = 0x000000, int background = 0xFFFFFF)
    {
        var cells = new Cell[rows, cols];
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                cells[row, col].Clear(foreground, background, 0);
            }
        }

        return new Screen
        {
            Cells = cells,
            CursorPosition = (0, 0),
            ForegroundColor = foreground,
            BackgroundColor = background,
        };
    }

    /// <summary>
    /// Sets a cell in the screen.
    /// </summary>
    /// <param name="screen">The screen to update.</param>
    /// <param name="row">The target row.</param>
    /// <param name="col">The target column.</param>
    /// <param name="character">The cell text.</param>
    /// <param name="foreground">Foreground color.</param>
    /// <param name="background">Background color.</param>
    /// <param name="special">Special color.</param>
    /// <param name="reverse">Whether reverse video is enabled.</param>
    /// <param name="italic">Whether italic is enabled.</param>
    /// <param name="bold">Whether bold is enabled.</param>
    /// <param name="underline">Whether underline is enabled.</param>
    /// <param name="undercurl">Whether undercurl is enabled.</param>
    public static void SetCell(
        Screen screen,
        int row,
        int col,
        string? character,
        int foreground,
        int background,
        int special = 0,
        bool reverse = false,
        bool italic = false,
        bool bold = false,
        bool underline = false,
        bool undercurl = false)
    {
        screen.Cells[row, col].Set(character, foreground, background, special, reverse, italic, bold, underline, undercurl);
    }
}
