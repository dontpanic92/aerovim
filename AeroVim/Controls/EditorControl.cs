// <copyright file="EditorControl.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Controls;

using System.Collections.Concurrent;
using AeroVim.Editor;
using AeroVim.Editor.Capabilities;
using AeroVim.Editor.Utilities;
using AeroVim.Utilities;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using MouseButton = AeroVim.Editor.MouseButton;

/// <summary>
/// The editor control using Avalonia custom rendering with SkiaSharp.
/// </summary>
public class EditorControl : Control, IDisposable
{
    private readonly IEditorClient editorClient;
    private readonly ConcurrentQueue<Action> pendingActions = new();
    private readonly EditorTextInputMethodClient imeClient;
    private readonly FontFallbackChain fontChain = new FontFallbackChain();
    private readonly LigatureTextShaper ligatureTextShaper = new();
    private readonly EditorRenderer renderer;
    private readonly EditorInputHandler inputHandler;
    private readonly CursorStateManager cursorState;
    private readonly EditorDrawOperation drawOperation;

    private TextLayoutParameters textParam;
    private List<string> currentGuiFontNames = new List<string>();
    private List<string> currentFontPriorityList = new List<string>
    {
        AeroVim.Editor.Utilities.FontPriorityList.GuiFontSentinel,
        AeroVim.Editor.Utilities.FontPriorityList.SystemMonoSentinel,
    };

    private volatile bool isDisposed;
    private int redrawQueued;

    /// <summary>
    /// Initializes a new instance of the <see cref="EditorControl"/> class.
    /// </summary>
    /// <param name="editorClient">The editor client.</param>
    public EditorControl(IEditorClient editorClient)
    {
        this.editorClient = editorClient;
        this.editorClient.Redraw += this.OnRedraw;
        this.editorClient.FontChanged += this.OnFontChanged;

        this.ClipToBounds = true;
        this.Focusable = true;

        this.imeClient = new EditorTextInputMethodClient(this);
        this.renderer = new EditorRenderer(this.fontChain, this.ligatureTextShaper, this.imeClient);
        this.inputHandler = new EditorInputHandler(editorClient);
        this.cursorState = new CursorStateManager(this.InvalidateVisual, () => this.editorClient.ModeInfo);
        this.drawOperation = new EditorDrawOperation(this, default);
        this.AddHandler(
            InputElement.TextInputMethodClientRequestedEvent,
            (_, e) =>
            {
                e.Client = this.imeClient;
            });

        this.RebuildFontChain();
        this.textParam = new TextLayoutParameters(this.fontChain.PrimaryFontName, 11);
    }

    /// <summary>
    /// Gets or sets a value indicating whether font ligature is enabled.
    /// </summary>
    public bool EnableLigature { get; set; }

    /// <summary>
    /// Gets a value indicating whether an IME composition is in progress.
    /// </summary>
    public bool IsComposing => this.imeClient.IsComposing;

    /// <summary>
    /// Gets or sets the alpha channel used for the default background.
    /// </summary>
    public byte BackgroundAlpha { get; set; } = byte.MaxValue;

    /// <summary>
    /// Gets or sets a value indicating whether the external UI overlays
    /// (floating cmdline popup and completion menu) are handling display.
    /// When <c>true</c>, <see cref="EditorRenderer"/> skips its built-in
    /// cmdline and popup menu drawing since no external events are delivered
    /// when the overlays are inactive, and the overlays handle all rendering
    /// when they are active.
    /// </summary>
    public bool UseExternalUI { get; set; }

    /// <summary>
    /// Gets the current primary font family name.
    /// </summary>
    public string FontName => this.textParam.FontName;

    /// <summary>
    /// Gets the current font size in device-independent pixels (Skia units).
    /// </summary>
    public double FontSize => this.textParam.SkiaFontSize;

    /// <summary>
    /// Gets the current character cell width in pixels.
    /// </summary>
    public double CharWidth => this.textParam.CharWidth;

    /// <summary>
    /// Gets the current line height in pixels.
    /// </summary>
    public double LineHeight => this.textParam.LineHeight;

    /// <summary>
    /// Gets the desired row count.
    /// </summary>
    public uint DesiredRowCount
    {
        get
        {
            var c = (uint)(this.Bounds.Height / this.textParam.LineHeight);
            return c == 0 ? 1 : c;
        }
    }

    /// <summary>
    /// Gets the desired column count.
    /// </summary>
    public uint DesiredColCount
    {
        get
        {
            var c = (uint)(this.Bounds.Width / this.textParam.CharWidth);
            return c == 0 ? 1 : c;
        }
    }

    /// <summary>
    /// Gets the currently resolved pointer cursor type for tests.
    /// </summary>
    internal StandardCursorType? ResolvedPointerCursorType => this.cursorState.ResolvedPointerCursorType;

    /// <summary>
    /// Sets the font priority list (user fonts + sentinels) and rebuilds the font chain.
    /// </summary>
    /// <param name="priorityList">Ordered list that may contain user font names and sentinels.</param>
    public void SetFontPriorityList(List<string> priorityList)
    {
        this.currentFontPriorityList = priorityList;
        AeroVim.Diagnostics.AppLogger.For<EditorControl>().Info(
            $"SetFontPriorityList called with [{string.Join(", ", priorityList)}].");
        this.pendingActions.Enqueue(() =>
        {
            this.fontChain.Rebuild(
                this.currentFontPriorityList,
                this.currentGuiFontNames,
                Utilities.Helpers.GetDefaultFallbackFontNames());

            AeroVim.Diagnostics.AppLogger.For<EditorControl>().Info(
                $"SetFontPriorityList rebuild: primary='{this.fontChain.PrimaryFontName}', pointSize={this.textParam.PointSize}.");

            if (this.fontChain.PrimaryFontName.Length > 0)
            {
                this.textParam = new TextLayoutParameters(this.fontChain.PrimaryFontName, this.textParam.PointSize);
                this.editorClient.TryResize(this.DesiredColCount, this.DesiredRowCount);
            }
        });

        Dispatcher.UIThread.Post(() => this.InvalidateVisual());
    }

    /// <summary>
    /// Sets the user-configured fallback font list and rebuilds the font chain.
    /// This is a convenience wrapper that accepts a plain font list (without
    /// sentinels) for backward compatibility.
    /// </summary>
    /// <param name="fonts">Ordered list of user fallback font names.</param>
    public void SetFallbackFonts(List<string> fonts)
    {
        this.SetFontPriorityList(AeroVim.Editor.Utilities.FontPriorityList.Normalize(fonts));
    }

    /// <summary>
    /// Input text to the editor.
    /// </summary>
    /// <param name="text">The input text sequence.</param>
    public void Input(string text)
    {
        this.editorClient.Input(text);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        this.drawOperation.Bounds = new Rect(0, 0, this.Bounds.Width, this.Bounds.Height);
        context.Custom(this.drawOperation);
    }

    /// <summary>
    /// Dispose the control.
    /// </summary>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Renders the control directly to a supplied Skia canvas for tests.
    /// </summary>
    /// <param name="canvas">The target Skia canvas.</param>
    internal void RenderForTesting(SKCanvas canvas)
    {
        this.RenderWithSkia(canvas);
    }

    /// <summary>
    /// Sets the IME preedit text for renderer tests.
    /// </summary>
    /// <param name="preeditText">The preedit text, or <c>null</c> to clear it.</param>
    /// <param name="cursorPos">The cursor position within the preedit text.</param>
    internal void SetPreeditTextForTesting(string? preeditText, int? cursorPos)
    {
        this.imeClient.SetPreeditText(preeditText, cursorPos);
    }

    /// <summary>
    /// Recomputes editor UI state from the current backend capability snapshot for tests.
    /// </summary>
    internal void RefreshEditorUiStateForTesting()
    {
        this.ApplyEditorUiState(this.editorClient.ModeInfo, resetCursorBlink: true);
    }

    /// <summary>
    /// Gets the resolved primary font name from the font chain for tests.
    /// Returns the font that is currently used for cell metric calculations.
    /// </summary>
    /// <returns>The primary font family name, or empty if no font is resolved.</returns>
    internal string GetPrimaryFontNameForTesting()
    {
        return this.fontChain.PrimaryFontName;
    }

    /// <summary>
    /// Gets the current text layout font name for tests.
    /// This is the font name stored in <see cref="TextLayoutParameters"/>.
    /// </summary>
    /// <returns>The font name used for grid metrics.</returns>
    internal string GetTextParamFontNameForTesting()
    {
        return this.textParam.FontName;
    }

    /// <summary>
    /// Sets the current cursor blink visibility state for renderer tests.
    /// </summary>
    /// <param name="visible">A value indicating whether the cursor should be rendered as visible.</param>
    internal void SetCursorBlinkVisibleForTesting(bool visible)
    {
        this.cursorState.SetCursorBlinkVisible(visible);
    }

    /// <summary>
    /// Handles a pointer press using grid coordinates for tests.
    /// </summary>
    /// <param name="button">The mouse button.</param>
    /// <param name="row">The zero-based grid row.</param>
    /// <param name="col">The zero-based grid column.</param>
    /// <param name="modifiers">The active key modifiers.</param>
    /// <returns>A value indicating whether the event was handled.</returns>
    internal bool HandlePointerPressedForTesting(MouseButton button, int row, int col, KeyModifiers modifiers = KeyModifiers.None)
    {
        return this.inputHandler.HandlePointerPressed(button, row, col, modifiers);
    }

    /// <summary>
    /// Handles a pointer drag using grid coordinates for tests.
    /// </summary>
    /// <param name="row">The zero-based grid row.</param>
    /// <param name="col">The zero-based grid column.</param>
    /// <param name="modifiers">The active key modifiers.</param>
    /// <returns>A value indicating whether the event was handled.</returns>
    internal bool HandlePointerMovedForTesting(int row, int col, KeyModifiers modifiers = KeyModifiers.None)
    {
        return this.inputHandler.HandlePointerMoved(row, col, modifiers);
    }

    /// <summary>
    /// Handles a pointer release using grid coordinates for tests.
    /// </summary>
    /// <param name="row">The zero-based grid row.</param>
    /// <param name="col">The zero-based grid column.</param>
    /// <param name="modifiers">The active key modifiers.</param>
    /// <returns>A value indicating whether the event was handled.</returns>
    internal bool HandlePointerReleasedForTesting(int row, int col, KeyModifiers modifiers = KeyModifiers.None)
    {
        return this.inputHandler.HandlePointerReleased(row, col, modifiers);
    }

    /// <summary>
    /// Handles a pointer wheel event using grid coordinates for tests.
    /// </summary>
    /// <param name="row">The zero-based grid row.</param>
    /// <param name="col">The zero-based grid column.</param>
    /// <param name="delta">The wheel delta.</param>
    /// <param name="modifiers">The active key modifiers.</param>
    /// <returns>A value indicating whether the event was handled.</returns>
    internal bool HandlePointerWheelForTesting(int row, int col, Vector delta, KeyModifiers modifiers = KeyModifiers.None)
    {
        return this.inputHandler.HandlePointerWheel(row, col, delta, modifiers);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        return availableSize;
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            // Drain any queued actions (especially font changes) so that
            // DesiredColCount / DesiredRowCount reflect the final font
            // metrics before we compute the grid size.  Without this,
            // the very first TryResize may use stale (default font)
            // metrics, causing the PTY to be spawned at a wrong size
            // that is immediately corrected — producing a visible flash.
            while (this.pendingActions.TryDequeue(out var action))
            {
                action();
            }

            this.editorClient.TryResize(this.DesiredColCount, this.DesiredRowCount);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);
        var (row, col) = this.PixelToGridPosition(point.Position);
        MouseButton? button = null;

        if (point.Properties.IsLeftButtonPressed)
        {
            button = MouseButton.Left;
        }
        else if (point.Properties.IsRightButtonPressed)
        {
            button = MouseButton.Right;
        }
        else if (point.Properties.IsMiddleButtonPressed)
        {
            button = MouseButton.Middle;
        }

        e.Handled = this.inputHandler.HandlePointerPressed(button, row, col, e.KeyModifiers);
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var point = e.GetCurrentPoint(this);
        var (row, col) = this.PixelToGridPosition(point.Position);
        e.Handled = this.inputHandler.HandlePointerMoved(row, col, e.KeyModifiers);
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        var point = e.GetCurrentPoint(this);
        var (row, col) = this.PixelToGridPosition(point.Position);
        e.Handled = this.inputHandler.HandlePointerReleased(row, col, e.KeyModifiers);
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var point = e.GetCurrentPoint(this);
        var (row, col) = this.PixelToGridPosition(point.Position);
        e.Handled = this.inputHandler.HandlePointerWheel(row, col, e.Delta, e.KeyModifiers);
    }

    /// <summary>
    /// Dispose managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose, false from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this.isDisposed)
        {
            // Set the flag BEFORE disposing resources so any concurrent
            // render (on the composition thread) sees the flag and bails
            // out before touching disposed Skia objects.
            this.isDisposed = true;

            if (disposing)
            {
                this.editorClient.Redraw -= this.OnRedraw;
                this.editorClient.FontChanged -= this.OnFontChanged;

                // Defer resource disposal to the next UI-thread cycle so
                // any in-flight render operation that already passed the
                // isDisposed check can complete safely.
                Dispatcher.UIThread.Post(this.DisposeCachedResources, DispatcherPriority.Background);
            }
        }
    }

    private (int Row, int Col) PixelToGridPosition(Point pixel)
    {
        int row = (int)(pixel.Y / this.textParam.LineHeight);
        int col = (int)(pixel.X / this.textParam.CharWidth);

        int maxRow = (int)this.DesiredRowCount - 1;
        int maxCol = (int)this.DesiredColCount - 1;

        row = Math.Clamp(row, 0, maxRow);
        col = Math.Clamp(col, 0, maxCol);

        return (row, col);
    }

    private void OnRedraw()
    {
        if (Interlocked.CompareExchange(ref this.redrawQueued, 1, 0) != 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            this.ApplyEditorUiState(this.editorClient.ModeInfo, resetCursorBlink: true);
            this.InvalidateVisual();
            Interlocked.Exchange(ref this.redrawQueued, 0);
        });
    }

    private void OnFontChanged(FontSettings font)
    {
        AeroVim.Diagnostics.AppLogger.For<EditorControl>().Info(
            $"OnFontChanged: guifont=[{string.Join(", ", font.FontNames)}], pointSize={font.FontPointSize}.");
        this.pendingActions.Enqueue(() =>
        {
            this.currentGuiFontNames = font.FontNames;
            this.RebuildFontChain();

            if (this.fontChain.PrimaryFontName.Length == 0)
            {
                AeroVim.Diagnostics.AppLogger.For<EditorControl>().Warning(
                    "None of the guifont names resolved to a valid font. Keeping current font.");
                return;
            }

            AeroVim.Diagnostics.AppLogger.For<EditorControl>().Info(
                $"OnFontChanged rebuild: primary='{this.fontChain.PrimaryFontName}', pointSize={font.FontPointSize}, "
                + $"priorityList=[{string.Join(", ", this.currentFontPriorityList)}].");

            this.textParam = new TextLayoutParameters(this.fontChain.PrimaryFontName, font.FontPointSize);
            this.editorClient.TryResize(this.DesiredColCount, this.DesiredRowCount);
        });

        Dispatcher.UIThread.Post(() => this.InvalidateVisual());
    }

    private void RenderWithSkia(SKCanvas canvas)
    {
        if (this.isDisposed)
        {
            return;
        }

        while (this.pendingActions.TryDequeue(out var action))
        {
            action();
        }

        var args = this.editorClient.GetScreen();
        if (args is null)
        {
            return;
        }

        Interlocked.Exchange(ref this.redrawQueued, 0);

        // Update IME cursor position from the screen we just obtained,
        // avoiding a second GetScreen() call and lock acquisition.
        var cursorPos = args.CursorPosition;
        var tp = this.textParam;
        Dispatcher.UIThread.Post(() =>
        {
            if (!this.isDisposed)
            {
                float x = cursorPos.Col * tp.CharWidth;
                float y = cursorPos.Row * tp.LineHeight;
                this.imeClient.UpdateCursorRectangle(new Rect(x, y, tp.CharWidth, tp.LineHeight));
            }
        });

        bool drawCursor = this.cursorState.ShouldDrawCursor(this.editorClient.ModeInfo);

        // Hide the editor grid cursor while the floating cmdline popup is
        // showing — the popup draws its own blinking caret.
        if (drawCursor && this.UseExternalUI)
        {
            var extCmdline = this.editorClient as IExternalCmdline;
            if (extCmdline?.Cmdline is not null)
            {
                drawCursor = false;
            }
        }

        this.renderer.Render(
            canvas,
            args,
            this.textParam,
            this.editorClient.ModeInfo,
            this.EnableLigature,
            this.BackgroundAlpha,
            drawCursor,
            this.UseExternalUI ? null : this.editorClient as IExternalPopupMenu,
            this.UseExternalUI ? null : this.editorClient as IExternalCmdline);
    }

    private void DisposeCachedResources()
    {
        this.Cursor = null;
        this.cursorState.Dispose();
        this.renderer.Dispose();
        this.ligatureTextShaper.Dispose();
        this.fontChain.Dispose();
    }

    private void RebuildFontChain()
    {
        this.ligatureTextShaper.ClearCache();
        this.renderer.DiscardBackbuffer();
        this.fontChain.Rebuild(
            this.currentFontPriorityList,
            this.currentGuiFontNames,
            Utilities.Helpers.GetDefaultFallbackFontNames());
    }

    private void ApplyEditorUiState(ModeInfo? modeInfo, bool resetCursorBlink)
    {
        if (!this.editorClient.MouseEnabled)
        {
            this.inputHandler.ClearPressedButton();
        }

        this.Cursor = this.cursorState.UpdatePointerCursor(modeInfo, this.editorClient.MouseEnabled);
        this.cursorState.UpdateCursorBlink(modeInfo, resetCursorBlink);
    }

    /// <summary>
    /// Custom draw operation for rendering the editor grid with SkiaSharp.
    /// Reused across frames to avoid per-frame allocation.
    /// </summary>
    private sealed class EditorDrawOperation : ICustomDrawOperation
    {
        private readonly EditorControl control;

        public EditorDrawOperation(EditorControl control, Rect bounds)
        {
            this.control = control;
            this.Bounds = bounds;
        }

        public Rect Bounds { get; set; }

        public void Dispose()
        {
        }

        public bool Equals(ICustomDrawOperation? other) => false;

        public bool HitTest(Point p) => this.Bounds.Contains(p);

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature is null)
            {
                return;
            }

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            this.control.RenderWithSkia(canvas);
        }
    }
}
