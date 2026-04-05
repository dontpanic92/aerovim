// <copyright file="CursorStateManager.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Controls;

using AeroVim.Editor.Utilities;
using Avalonia.Input;
using Avalonia.Threading;

/// <summary>
/// Manages cursor blink state and pointer cursor shape resolution.
/// </summary>
internal sealed class CursorStateManager : IDisposable
{
    private static readonly TimeSpan DefaultCursorBlinkInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan DefaultCursorBlinkWait = TimeSpan.FromMilliseconds(700);

    private readonly Action invalidateVisual;
    private readonly Func<ModeInfo?> getModeInfo;
    private DispatcherTimer? cursorBlinkTimer;
    private Cursor? pointerCursor;
    private StandardCursorType? resolvedPointerCursorType;
    private bool cursorBlinkVisible = true;
    private bool cursorBlinkStarted;
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CursorStateManager"/> class.
    /// </summary>
    /// <param name="invalidateVisual">Callback to request a visual refresh when blink state changes.</param>
    /// <param name="getModeInfo">Callback to retrieve the current mode info.</param>
    public CursorStateManager(Action invalidateVisual, Func<ModeInfo?> getModeInfo)
    {
        this.invalidateVisual = invalidateVisual;
        this.getModeInfo = getModeInfo;
    }

    /// <summary>
    /// Gets the currently resolved pointer cursor type, or null.
    /// </summary>
    public StandardCursorType? ResolvedPointerCursorType => this.resolvedPointerCursorType;

    /// <summary>
    /// Gets a value indicating whether the cursor should be drawn given the current blink state.
    /// </summary>
    /// <param name="modeInfo">The current mode info.</param>
    /// <returns>True if the cursor should be drawn.</returns>
    public bool ShouldDrawCursor(ModeInfo? modeInfo)
    {
        if (modeInfo is { CursorVisible: false })
        {
            return false;
        }

        return !ShouldBlinkCursor(modeInfo) || this.cursorBlinkVisible;
    }

    /// <summary>
    /// Sets the cursor blink visibility directly, for testing purposes.
    /// </summary>
    /// <param name="visible">Whether the cursor should be visible.</param>
    public void SetCursorBlinkVisible(bool visible)
    {
        this.cursorBlinkVisible = visible;
    }

    /// <summary>
    /// Updates the pointer cursor based on mode info.
    /// </summary>
    /// <param name="modeInfo">The current mode info.</param>
    /// <param name="mouseEnabled">Whether mouse input is enabled.</param>
    /// <returns>The new cursor to apply, or null to clear the cursor.</returns>
    public Cursor? UpdatePointerCursor(ModeInfo? modeInfo, bool mouseEnabled)
    {
        var pointerCursorType = this.ResolvePointerCursorType(modeInfo, mouseEnabled);
        if (this.resolvedPointerCursorType == pointerCursorType)
        {
            return this.pointerCursor;
        }

        this.resolvedPointerCursorType = pointerCursorType;
        this.DisposePointerCursor();
        if (pointerCursorType is null)
        {
            return null;
        }

        if (Avalonia.Application.Current is null)
        {
            return null;
        }

        this.pointerCursor = new Cursor(pointerCursorType.Value);
        return this.pointerCursor;
    }

    /// <summary>
    /// Updates the cursor blink state based on mode info.
    /// </summary>
    /// <param name="modeInfo">The current mode info.</param>
    /// <param name="resetCursorBlink">Whether to restart the blink cycle.</param>
    public void UpdateCursorBlink(ModeInfo? modeInfo, bool resetCursorBlink)
    {
        if (!ShouldBlinkCursor(modeInfo))
        {
            this.StopCursorBlink();
            return;
        }

        if (!resetCursorBlink && this.cursorBlinkTimer is not null && this.cursorBlinkTimer.IsEnabled)
        {
            return;
        }

        this.cursorBlinkVisible = true;
        this.cursorBlinkStarted = modeInfo!.CursorBlinking != CursorBlinking.BlinkWait;
        this.EnsureCursorBlinkTimer();
        this.cursorBlinkTimer!.Interval = this.cursorBlinkStarted ? DefaultCursorBlinkInterval : DefaultCursorBlinkWait;
        this.cursorBlinkTimer.Start();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!this.isDisposed)
        {
            if (this.cursorBlinkTimer is not null)
            {
                this.cursorBlinkTimer.Stop();
                this.cursorBlinkTimer.Tick -= this.OnCursorBlinkTick;
                this.cursorBlinkTimer = null;
            }

            this.DisposePointerCursor();
            this.isDisposed = true;
        }
    }

    private static bool ShouldBlinkCursor(ModeInfo? modeInfo)
    {
        return modeInfo is { CursorVisible: true, CursorStyleEnabled: true }
            && modeInfo.CursorBlinking != CursorBlinking.BlinkOff;
    }

    private static bool ShouldHidePointer(ModeInfo modeInfo, bool mouseEnabled)
    {
        return modeInfo.PointerMode switch
        {
            1 => !mouseEnabled,
            2 => true,

            // Avalonia cannot keep the cursor hidden after it leaves the control,
            // so "always hide even on leave" degrades to "always hide over the editor".
            3 => true,
            _ => false,
        };
    }

    private static StandardCursorType? MapPointerShape(string? pointerShape)
    {
        if (string.IsNullOrWhiteSpace(pointerShape))
        {
            return null;
        }

        return pointerShape.Trim().ToLowerInvariant() switch
        {
            "arrow" => StandardCursorType.Arrow,
            "beam" or "ibeam" or "text" => StandardCursorType.Ibeam,
            "hand" or "pointer" => StandardCursorType.Hand,
            "cross" or "crosshair" => StandardCursorType.Cross,
            "help" => StandardCursorType.Help,
            "wait" or "busy" => StandardCursorType.Wait,
            "move" or "sizeall" => StandardCursorType.SizeAll,
            "no" or "forbidden" or "not-allowed" => StandardCursorType.No,
            "ew-resize" or "col-resize" or "sizewe" => StandardCursorType.SizeWestEast,
            "ns-resize" or "row-resize" or "sizens" => StandardCursorType.SizeNorthSouth,
            "n-resize" => StandardCursorType.TopSide,
            "s-resize" => StandardCursorType.BottomSide,
            "w-resize" => StandardCursorType.LeftSide,
            "e-resize" => StandardCursorType.RightSide,
            "nw-resize" => StandardCursorType.TopLeftCorner,
            "ne-resize" => StandardCursorType.TopRightCorner,
            "sw-resize" => StandardCursorType.BottomLeftCorner,
            "se-resize" => StandardCursorType.BottomRightCorner,
            "copy" => StandardCursorType.DragCopy,
            "link" => StandardCursorType.DragLink,
            _ => null,
        };
    }

    private StandardCursorType? ResolvePointerCursorType(ModeInfo? modeInfo, bool mouseEnabled)
    {
        if (modeInfo is null)
        {
            return null;
        }

        if (ShouldHidePointer(modeInfo, mouseEnabled))
        {
            return StandardCursorType.None;
        }

        return MapPointerShape(modeInfo.PointerShape);
    }

    private void EnsureCursorBlinkTimer()
    {
        if (this.cursorBlinkTimer is not null)
        {
            return;
        }

        this.cursorBlinkTimer = new DispatcherTimer();
        this.cursorBlinkTimer.Tick += this.OnCursorBlinkTick;
    }

    private void OnCursorBlinkTick(object? sender, EventArgs e)
    {
        var modeInfo = this.getModeInfo();
        if (!ShouldBlinkCursor(modeInfo))
        {
            bool wasHidden = !this.cursorBlinkVisible;
            this.StopCursorBlink();
            if (wasHidden)
            {
                this.invalidateVisual();
            }

            return;
        }

        if (!this.cursorBlinkStarted)
        {
            this.cursorBlinkStarted = true;
            this.cursorBlinkVisible = false;
            this.cursorBlinkTimer!.Interval = DefaultCursorBlinkInterval;
        }
        else
        {
            this.cursorBlinkVisible = !this.cursorBlinkVisible;
        }

        this.invalidateVisual();
    }

    private void StopCursorBlink()
    {
        if (this.cursorBlinkTimer is not null)
        {
            this.cursorBlinkTimer.Stop();
        }

        this.cursorBlinkStarted = false;
        this.cursorBlinkVisible = true;
    }

    private void DisposePointerCursor()
    {
        if (this.pointerCursor is not null)
        {
            this.pointerCursor.Dispose();
            this.pointerCursor = null;
        }
    }
}
