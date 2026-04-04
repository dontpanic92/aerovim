// <copyright file="EditorControlTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests;

using AeroVim.Controls;
using AeroVim.Tests.Helpers;
using NUnit.Framework;
using SkiaSharp;

/// <summary>
/// Tests editor rendering behavior with off-screen surfaces.
/// </summary>
public class EditorControlTests
{
    /// <summary>
    /// Rendering should reflect updated screen contents.
    /// </summary>
    [Test]
    public void RenderForTesting_ReflectsScreenContentChanges()
    {
        var editorClient = new TestEditorClient();
        var screenA = TestScreenBuilder.CreateScreen(2, 4);
        TestScreenBuilder.SetCell(screenA, 0, 0, "A", 0x000000, 0xFFFFFF);
        screenA.CursorPosition = (1, 1);
        editorClient.CurrentScreen = screenA;

        var control = new EditorControl(editorClient);
        byte[] firstRender = RenderToPng(control, 240, 120);

        var screenB = TestScreenBuilder.CreateScreen(2, 4);
        TestScreenBuilder.SetCell(screenB, 0, 0, "B", 0x000000, 0xFFFFFF);
        screenB.CursorPosition = (1, 1);
        editorClient.CurrentScreen = screenB;
        byte[] secondRender = RenderToPng(control, 240, 120);

        Assert.That(secondRender, Is.Not.EqualTo(firstRender));
    }

    /// <summary>
    /// Rendering with wide cells and IME preedit text should remain stable and change the output.
    /// </summary>
    [Test]
    public void RenderForTesting_WithWideCharacterAndPreedit_ChangesOutput()
    {
        var editorClient = new TestEditorClient();
        var screen = TestScreenBuilder.CreateScreen(1, 4);
        TestScreenBuilder.SetCell(screen, 0, 0, "中", 0x202020, 0xFFFFFF);
        TestScreenBuilder.SetCell(screen, 0, 1, null, 0x202020, 0xFFFFFF);
        screen.CursorPosition = (0, 2);
        editorClient.CurrentScreen = screen;

        var control = new EditorControl(editorClient);
        byte[] withoutPreedit = RenderToPng(control, 240, 120);

        control.SetPreeditTextForTesting("x", 0);
        byte[] withPreedit = RenderToPng(control, 240, 120);

        Assert.That(withPreedit, Is.Not.EqualTo(withoutPreedit));
    }

    private static byte[] RenderToPng(EditorControl control, int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        surface.Canvas.Clear(SKColors.Transparent);
        control.RenderForTesting(surface.Canvas);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
