// <copyright file="EditorInputHandler.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Controls;

using AeroVim.Editor;
using Avalonia;
using Avalonia.Input;

/// <summary>
/// Translates pointer events into editor client mouse commands.
/// </summary>
internal sealed class EditorInputHandler
{
    private readonly IEditorClient editorClient;
    private string? pressedMouseButton;

    /// <summary>
    /// Initializes a new instance of the <see cref="EditorInputHandler"/> class.
    /// </summary>
    /// <param name="editorClient">The editor client to forward mouse commands to.</param>
    public EditorInputHandler(IEditorClient editorClient)
    {
        this.editorClient = editorClient;
    }

    /// <summary>
    /// Clears the pressed mouse button state, e.g. when mouse support is disabled mid-drag.
    /// </summary>
    public void ClearPressedButton()
    {
        this.pressedMouseButton = null;
    }

    /// <summary>
    /// Handles a pointer press event.
    /// </summary>
    /// <param name="button">The button name ("left", "right", "middle"), or null.</param>
    /// <param name="row">The zero-based grid row.</param>
    /// <param name="col">The zero-based grid column.</param>
    /// <param name="modifiers">The active key modifiers.</param>
    /// <returns>True if the event was handled.</returns>
    public bool HandlePointerPressed(string? button, int row, int col, KeyModifiers modifiers)
    {
        if (button is null)
        {
            return false;
        }

        if (!this.editorClient.MouseEnabled)
        {
            this.pressedMouseButton = null;
            return false;
        }

        this.pressedMouseButton = button;
        this.editorClient.InputMouse(button, "press", GetModifierString(modifiers), 0, row, col);
        return true;
    }

    /// <summary>
    /// Handles a pointer move (drag) event.
    /// </summary>
    /// <param name="row">The zero-based grid row.</param>
    /// <param name="col">The zero-based grid column.</param>
    /// <param name="modifiers">The active key modifiers.</param>
    /// <returns>True if the event was handled.</returns>
    public bool HandlePointerMoved(int row, int col, KeyModifiers modifiers)
    {
        if (this.pressedMouseButton is null)
        {
            return false;
        }

        if (!this.editorClient.MouseEnabled)
        {
            this.pressedMouseButton = null;
            return false;
        }

        this.editorClient.InputMouse(this.pressedMouseButton, "drag", GetModifierString(modifiers), 0, row, col);
        return true;
    }

    /// <summary>
    /// Handles a pointer release event.
    /// </summary>
    /// <param name="row">The zero-based grid row.</param>
    /// <param name="col">The zero-based grid column.</param>
    /// <param name="modifiers">The active key modifiers.</param>
    /// <returns>True if the event was handled.</returns>
    public bool HandlePointerReleased(int row, int col, KeyModifiers modifiers)
    {
        if (this.pressedMouseButton is null)
        {
            return false;
        }

        if (!this.editorClient.MouseEnabled)
        {
            this.pressedMouseButton = null;
            return false;
        }

        this.editorClient.InputMouse(this.pressedMouseButton, "release", GetModifierString(modifiers), 0, row, col);
        this.pressedMouseButton = null;
        return true;
    }

    /// <summary>
    /// Handles a pointer wheel event.
    /// </summary>
    /// <param name="row">The zero-based grid row.</param>
    /// <param name="col">The zero-based grid column.</param>
    /// <param name="delta">The wheel delta.</param>
    /// <param name="modifiers">The active key modifiers.</param>
    /// <returns>True if the event was handled.</returns>
    public bool HandlePointerWheel(int row, int col, Vector delta, KeyModifiers modifiers)
    {
        if (!this.editorClient.MouseEnabled)
        {
            return false;
        }

        var modifier = GetModifierString(modifiers);
        bool handled = false;
        if (delta.Y > 0)
        {
            this.editorClient.InputMouse("wheel", "up", modifier, 0, row, col);
            handled = true;
        }
        else if (delta.Y < 0)
        {
            this.editorClient.InputMouse("wheel", "down", modifier, 0, row, col);
            handled = true;
        }

        if (delta.X > 0)
        {
            this.editorClient.InputMouse("wheel", "right", modifier, 0, row, col);
            handled = true;
        }
        else if (delta.X < 0)
        {
            this.editorClient.InputMouse("wheel", "left", modifier, 0, row, col);
            handled = true;
        }

        return handled;
    }

    private static string GetModifierString(KeyModifiers modifiers)
    {
        var parts = string.Empty;

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts += "S";
        }

        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            parts += "C";
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts += "A";
        }

        return parts;
    }
}
