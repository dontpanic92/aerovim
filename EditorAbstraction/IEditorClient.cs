// <copyright file="IEditorClient.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor;

using AeroVim.Editor.Utilities;

/// <summary>
/// Callback for title changes.
/// </summary>
/// <param name="title">The new title.</param>
public delegate void TitleChangedHandler(string title);

/// <summary>
/// Callback for redraw events.
/// </summary>
public delegate void RedrawHandler();

/// <summary>
/// Callback for editor process exit.
/// </summary>
/// <param name="exitCode">The exit code.</param>
public delegate void EditorExitedHandler(int exitCode);

/// <summary>
/// Callback for color changes.
/// </summary>
/// <param name="color">The color value as an integer.</param>
public delegate void ColorChangedHandler(int color);

/// <summary>
/// Callback for font changes.
/// </summary>
/// <param name="fontSettings">The new font settings.</param>
public delegate void FontChangedHandler(FontSettings fontSettings);

/// <summary>
/// Abstraction over an editor backend (Neovim, Vim, etc.).
/// </summary>
public interface IEditorClient : IDisposable
{
    /// <summary>
    /// Raised when the editor title changes.
    /// </summary>
    event TitleChangedHandler TitleChanged;

    /// <summary>
    /// Raised when the editor should redraw.
    /// </summary>
    event RedrawHandler Redraw;

    /// <summary>
    /// Raised when the editor process exits.
    /// </summary>
    event EditorExitedHandler EditorExited;

    /// <summary>
    /// Raised when the editor foreground color changes.
    /// </summary>
    event ColorChangedHandler ForegroundColorChanged;

    /// <summary>
    /// Raised when the editor background color changes.
    /// </summary>
    event ColorChangedHandler BackgroundColorChanged;

    /// <summary>
    /// Raised when the editor font changes.
    /// </summary>
    event FontChangedHandler FontChanged;

    /// <summary>
    /// Gets the current mode info (cursor shape, size, blink state).
    /// </summary>
    ModeInfo? ModeInfo { get; }

    /// <summary>
    /// Gets the current font settings.
    /// </summary>
    FontSettings FontSettings { get; }

    /// <summary>
    /// Try to resize the editor screen.
    /// </summary>
    /// <param name="width">Column count.</param>
    /// <param name="height">Row count.</param>
    void TryResize(uint width, uint height);

    /// <summary>
    /// Send keyboard input to the editor.
    /// </summary>
    /// <param name="text">The input key sequence in Vim notation.</param>
    void Input(string text);

    /// <summary>
    /// Send a mouse event to the editor.
    /// </summary>
    /// <param name="button">Mouse button: "left", "right", "middle", "wheel", or "move".</param>
    /// <param name="action">Action: "press", "drag", "release" for buttons; "up", "down", "left", "right" for wheel.</param>
    /// <param name="modifier">Modifier keys string, e.g. "", "S", "C", "A", "C-S".</param>
    /// <param name="grid">Grid id (0 when multigrid is not enabled).</param>
    /// <param name="row">Zero-based grid row.</param>
    /// <param name="col">Zero-based grid column.</param>
    void InputMouse(string button, string action, string modifier, int grid, int row, int col);

    /// <summary>
    /// Execute an editor command.
    /// </summary>
    /// <param name="command">The command string.</param>
    void Command(string command);

    /// <summary>
    /// Get the current screen state for rendering.
    /// </summary>
    /// <returns>The current screen state, or null if not yet initialized.</returns>
    Screen? GetScreen();
}
