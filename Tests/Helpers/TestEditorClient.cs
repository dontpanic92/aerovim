// <copyright file="TestEditorClient.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests.Helpers;

using AeroVim.Editor;
using AeroVim.Editor.Utilities;

/// <summary>
/// Test implementation of <see cref="IEditorClient"/>.
/// </summary>
internal sealed class TestEditorClient : IEditorClient
{
    /// <inheritdoc />
    public event TitleChangedHandler? TitleChanged;

    /// <inheritdoc />
    public event RedrawHandler? Redraw;

    /// <inheritdoc />
    public event EditorExitedHandler? EditorExited;

    /// <inheritdoc />
    public event ColorChangedHandler? ForegroundColorChanged;

    /// <inheritdoc />
    public event ColorChangedHandler? BackgroundColorChanged;

    /// <inheritdoc />
    public event FontChangedHandler? FontChanged;

    /// <summary>
    /// Gets the recorded resize calls.
    /// </summary>
    public List<(uint Width, uint Height)> ResizeCalls { get; } = new List<(uint Width, uint Height)>();

    /// <summary>
    /// Gets the recorded text inputs.
    /// </summary>
    public List<string> InputCalls { get; } = new List<string>();

    /// <summary>
    /// Gets the recorded command invocations.
    /// </summary>
    public List<string> CommandCalls { get; } = new List<string>();

    /// <summary>
    /// Gets the recorded mouse input calls.
    /// </summary>
    public List<(MouseButton Button, MouseAction Action, string Modifier, int Grid, int Row, int Col)> MouseCalls { get; }
        = new List<(MouseButton Button, MouseAction Action, string Modifier, int Grid, int Row, int Col)>();

    /// <summary>
    /// Gets or sets the current test screen.
    /// </summary>
    public Screen? CurrentScreen { get; set; }

    /// <inheritdoc />
    public ModeInfo? ModeInfo { get; set; }

    /// <inheritdoc />
    public bool MouseEnabled { get; set; } = true;

    /// <inheritdoc />
    public MouseTrackingMode MouseTrackingMode { get; set; } = MouseTrackingMode.ButtonEvent;

    /// <inheritdoc />
    public FontSettings FontSettings { get; private set; } = new FontSettings
    {
        FontNames = new List<string> { "Consolas" },
        FontPointSize = 11,
    };

    /// <inheritdoc />
    public void TryResize(uint width, uint height)
    {
        this.ResizeCalls.Add((width, height));
    }

    /// <inheritdoc />
    public void Input(string text)
    {
        this.InputCalls.Add(text);
    }

    /// <inheritdoc />
    public void InputMouse(MouseButton button, MouseAction action, string modifier, int grid, int row, int col)
    {
        this.MouseCalls.Add((button, action, modifier, grid, row, col));
    }

    /// <inheritdoc />
    public void Command(string command)
    {
        this.CommandCalls.Add(command);
    }

    /// <inheritdoc />
    public Screen? GetScreen()
    {
        return this.CurrentScreen;
    }

    /// <summary>
    /// Raises the redraw event.
    /// </summary>
    public void RaiseRedraw()
    {
        this.Redraw?.Invoke();
    }

    /// <summary>
    /// Raises the font changed event.
    /// </summary>
    /// <param name="fontSettings">The new font settings.</param>
    public void RaiseFontChanged(FontSettings fontSettings)
    {
        this.FontSettings = fontSettings;
        this.FontChanged?.Invoke(fontSettings);
    }

    /// <summary>
    /// Raises the title changed event.
    /// </summary>
    /// <param name="title">The updated title.</param>
    public void RaiseTitleChanged(string title)
    {
        this.TitleChanged?.Invoke(title);
    }

    /// <summary>
    /// Raises the foreground color changed event.
    /// </summary>
    /// <param name="color">The new foreground color.</param>
    public void RaiseForegroundColorChanged(int color)
    {
        this.ForegroundColorChanged?.Invoke(color);
    }

    /// <summary>
    /// Raises the background color changed event.
    /// </summary>
    /// <param name="color">The new background color.</param>
    public void RaiseBackgroundColorChanged(int color)
    {
        this.BackgroundColorChanged?.Invoke(color);
    }

    /// <summary>
    /// Raises the editor exited event.
    /// </summary>
    /// <param name="exitCode">The exit code.</param>
    public void RaiseEditorExited(int exitCode)
    {
        this.EditorExited?.Invoke(exitCode);
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
