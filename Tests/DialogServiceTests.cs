// <copyright file="DialogServiceTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests;

using AeroVim.Services;
using Avalonia.Platform.Storage;
using NUnit.Framework;

/// <summary>
/// Tests dialog service helpers around file picker result handling.
/// </summary>
public class DialogServiceTests
{
    /// <summary>
    /// Canceling the picker should be treated as no selection when the backend returns null.
    /// </summary>
    [Test]
    public void GetSelectedFilePath_WithNullResult_ReturnsNull()
    {
        Assert.That(DialogService.GetSelectedFilePath(null), Is.Null);
    }

    /// <summary>
    /// Canceling the picker should be treated as no selection when the backend returns an empty list.
    /// </summary>
    [Test]
    public void GetSelectedFilePath_WithEmptyResult_ReturnsNull()
    {
        Assert.That(DialogService.GetSelectedFilePath(Array.Empty<IStorageFile>()), Is.Null);
    }
}
