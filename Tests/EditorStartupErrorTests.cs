// <copyright file="EditorStartupErrorTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests;

using AeroVim.Services;
using AeroVim.Utilities;
using NUnit.Framework;

/// <summary>
/// Tests for editor startup error classification and path validation.
/// </summary>
public class EditorStartupErrorTests
{
    /// <summary>
    /// ValidateEditorPath should return an error when the path is null.
    /// </summary>
    [Test]
    public void ValidateEditorPath_NullPath_ReturnsError()
    {
        var result = EditorPathDetector.ValidateEditorPath(EditorType.Neovim, null);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("not configured"));
    }

    /// <summary>
    /// ValidateEditorPath should return an error when the path is empty.
    /// </summary>
    [Test]
    public void ValidateEditorPath_EmptyPath_ReturnsError()
    {
        var result = EditorPathDetector.ValidateEditorPath(EditorType.Neovim, string.Empty);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("not configured"));
    }

    /// <summary>
    /// ValidateEditorPath should return an error when the path is whitespace.
    /// </summary>
    [Test]
    public void ValidateEditorPath_WhitespacePath_ReturnsError()
    {
        var result = EditorPathDetector.ValidateEditorPath(EditorType.Vim, "   ");
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("not configured"));
    }

    /// <summary>
    /// ValidateEditorPath should return an error when the absolute path does not exist.
    /// </summary>
    [Test]
    public void ValidateEditorPath_NonexistentAbsolutePath_ReturnsError()
    {
        string fakePath = Path.Combine(Path.GetTempPath(), "nonexistent_editor_abc123.exe");
        var result = EditorPathDetector.ValidateEditorPath(EditorType.Neovim, fakePath);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("not found"));
    }

    /// <summary>
    /// ValidateEditorPath should return null for a bare executable name
    /// (no directory separator) since it may be resolved by the OS.
    /// </summary>
    [Test]
    public void ValidateEditorPath_BareExecutableName_ReturnsNull()
    {
        var result = EditorPathDetector.ValidateEditorPath(EditorType.Neovim, "nvim");
        Assert.That(result, Is.Null);
    }

    /// <summary>
    /// ValidateEditorPath should include the editor name in the error message.
    /// </summary>
    [Test]
    public void ValidateEditorPath_IncludesEditorName()
    {
        var neovimResult = EditorPathDetector.ValidateEditorPath(EditorType.Neovim, null);
        Assert.That(neovimResult, Does.Contain("Neovim"));

        var vimResult = EditorPathDetector.ValidateEditorPath(EditorType.Vim, null);
        Assert.That(vimResult, Does.Contain("Vim"));
    }

    /// <summary>
    /// EditorNotFoundException should include the editor name and path in UserMessage.
    /// </summary>
    [Test]
    public void EditorNotFoundException_NullPath_MessageMentionsSettings()
    {
        var ex = new EditorNotFoundException("Neovim", null);
        Assert.That(ex.UserMessage, Does.Contain("not configured"));
        Assert.That(ex.UserMessage, Does.Contain("Neovim"));
        Assert.That(ex.EditorName, Is.EqualTo("Neovim"));
        Assert.That(ex.AttemptedPath, Is.Null);
    }

    /// <summary>
    /// EditorNotFoundException with a path should mention the path tried.
    /// </summary>
    [Test]
    public void EditorNotFoundException_WithPath_MessageMentionsPath()
    {
        var ex = new EditorNotFoundException("Vim", "/usr/local/bin/vim");
        Assert.That(ex.UserMessage, Does.Contain("/usr/local/bin/vim"));
        Assert.That(ex.UserMessage, Does.Contain("not found"));
        Assert.That(ex.AttemptedPath, Is.EqualTo("/usr/local/bin/vim"));
    }

    /// <summary>
    /// EditorLaunchException should include the path and inner exception message.
    /// </summary>
    [Test]
    public void EditorLaunchException_IncludesPathAndInnerMessage()
    {
        var inner = new System.ComponentModel.Win32Exception(5, "Access is denied");
        var ex = new EditorLaunchException("Neovim", @"C:\nvim\nvim.exe", inner);
        Assert.That(ex.UserMessage, Does.Contain(@"C:\nvim\nvim.exe"));
        Assert.That(ex.UserMessage, Does.Contain("Access is denied"));
        Assert.That(ex.InnerException, Is.SameAs(inner));
        Assert.That(ex.EditorName, Is.EqualTo("Neovim"));
        Assert.That(ex.AttemptedPath, Is.EqualTo(@"C:\nvim\nvim.exe"));
    }

    /// <summary>
    /// EditorCrashedException should include the exit code and path.
    /// </summary>
    [Test]
    public void EditorCrashedException_IncludesExitCodeAndPath()
    {
        var ex = new EditorCrashedException("Vim", "/usr/bin/vim", 1);
        Assert.That(ex.UserMessage, Does.Contain("exit code 1"));
        Assert.That(ex.UserMessage, Does.Contain("/usr/bin/vim"));
        Assert.That(ex.ExitCode, Is.EqualTo(1));
        Assert.That(ex.AttemptedPath, Is.EqualTo("/usr/bin/vim"));
    }

    /// <summary>
    /// BackendCommunicationException should include the detail text.
    /// </summary>
    [Test]
    public void BackendCommunicationException_IncludesDetail()
    {
        var ex = new BackendCommunicationException("Neovim", "Stream closed unexpectedly");
        Assert.That(ex.UserMessage, Does.Contain("Stream closed unexpectedly"));
        Assert.That(ex.UserMessage, Does.Contain("communication failed"));
        Assert.That(ex.EditorName, Is.EqualTo("Neovim"));
    }

    /// <summary>
    /// EditorClientFactory.Create should throw EditorNotFoundException for an empty path.
    /// </summary>
    [Test]
    public void EditorClientFactory_EmptyNeovimPath_ThrowsEditorNotFoundException()
    {
        var settings = new AppSettings
        {
            EditorType = EditorType.Neovim,
            NeovimPath = string.Empty,
        };

        var ex = Assert.Throws<EditorNotFoundException>(() =>
            EditorClientFactory.Create(settings, new AeroVim.Editor.Diagnostics.NullLogger()));
        Assert.That(ex!.UserMessage, Does.Contain("not configured"));
    }

    /// <summary>
    /// EditorClientFactory.Create should throw EditorNotFoundException for a nonexistent absolute path.
    /// </summary>
    [Test]
    public void EditorClientFactory_NonexistentPath_ThrowsEditorNotFoundException()
    {
        string fakePath = Path.Combine(Path.GetTempPath(), "totally_fake_nvim_abc123.exe");
        var settings = new AppSettings
        {
            EditorType = EditorType.Neovim,
            NeovimPath = fakePath,
        };

        var ex = Assert.Throws<EditorNotFoundException>(() =>
            EditorClientFactory.Create(settings, new AeroVim.Editor.Diagnostics.NullLogger()));
        Assert.That(ex!.UserMessage, Does.Contain("not found"));
    }

    /// <summary>
    /// EditorClientFactory.Create should throw EditorNotFoundException for empty Vim path.
    /// </summary>
    [Test]
    public void EditorClientFactory_EmptyVimPath_ThrowsEditorNotFoundException()
    {
        var settings = new AppSettings
        {
            EditorType = EditorType.Vim,
            VimPath = string.Empty,
        };

        var ex = Assert.Throws<EditorNotFoundException>(() =>
            EditorClientFactory.Create(settings, new AeroVim.Editor.Diagnostics.NullLogger()));
        Assert.That(ex!.UserMessage, Does.Contain("not configured"));
    }

    /// <summary>
    /// All EditorStartupException subclasses should be catchable as EditorStartupException.
    /// </summary>
    [Test]
    public void AllSubclasses_AreCatchableAsBase()
    {
        Assert.That(new EditorNotFoundException("Neovim", null), Is.InstanceOf<EditorStartupException>());
        Assert.That(new EditorLaunchException("Neovim", "/bin/nvim", new Exception()), Is.InstanceOf<EditorStartupException>());
        Assert.That(new EditorCrashedException("Neovim", "/bin/nvim", 1), Is.InstanceOf<EditorStartupException>());
        Assert.That(new BackendCommunicationException("Neovim", "detail"), Is.InstanceOf<EditorStartupException>());
    }
}
