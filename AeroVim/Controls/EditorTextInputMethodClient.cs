// <copyright file="EditorTextInputMethodClient.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Controls;

using Avalonia;
using Avalonia.Input.TextInput;

/// <summary>
/// Connects the <see cref="EditorControl"/> to the platform Input Method Editor (IME),
/// enabling CJK and other composed-text input.
/// </summary>
internal sealed class EditorTextInputMethodClient : TextInputMethodClient
{
    private readonly EditorControl editorControl;
    private Rect cursorRectangle;

    /// <summary>
    /// Initializes a new instance of the <see cref="EditorTextInputMethodClient"/> class.
    /// </summary>
    /// <param name="editorControl">The owning editor control.</param>
    public EditorTextInputMethodClient(EditorControl editorControl)
    {
        this.editorControl = editorControl;
    }

    /// <summary>
    /// Gets the current preedit (composition) text, or null when idle.
    /// </summary>
    public string? PreeditText { get; private set; }

    /// <summary>
    /// Gets the cursor offset within the preedit text, or null.
    /// </summary>
    public int? PreeditCursorPos { get; private set; }

    /// <inheritdoc />
    public override Visual TextViewVisual => this.editorControl;

    /// <inheritdoc />
    public override bool SupportsPreedit => true;

    /// <inheritdoc />
    public override bool SupportsSurroundingText => false;

    /// <inheritdoc />
    public override string SurroundingText => string.Empty;

    /// <inheritdoc />
    public override Rect CursorRectangle => this.cursorRectangle;

    /// <inheritdoc />
    public override TextSelection Selection
    {
        get => default;
        set { }
    }

    /// <summary>
    /// Gets a value indicating whether an IME composition is in progress.
    /// </summary>
    public bool IsComposing => this.PreeditText is not null;

    /// <inheritdoc />
    public override void SetPreeditText(string? preeditText, int? cursorPos)
    {
        this.PreeditText = string.IsNullOrEmpty(preeditText) ? null : preeditText;
        this.PreeditCursorPos = cursorPos;
        this.editorControl.InvalidateVisual();
    }

    /// <summary>
    /// Updates the cursor rectangle reported to the IME so the candidate window
    /// tracks the editor cursor.
    /// </summary>
    /// <param name="rect">The cursor rectangle in <see cref="EditorControl"/> coordinates.</param>
    public void UpdateCursorRectangle(Rect rect)
    {
        if (this.cursorRectangle != rect)
        {
            this.cursorRectangle = rect;
            this.RaiseCursorRectangleChanged();
        }
    }
}
