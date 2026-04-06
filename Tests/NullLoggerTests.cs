// <copyright file="NullLoggerTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests;

using AeroVim.Editor.Diagnostics;
using NUnit.Framework;

/// <summary>
/// Tests <see cref="NullLogger"/> no-op behavior.
/// </summary>
public class NullLoggerTests
{
    /// <summary>
    /// The singleton instance should always be available.
    /// </summary>
    [Test]
    public void Instance_IsNotNull()
    {
        Assert.That(NullLogger.Instance, Is.Not.Null);
    }

    /// <summary>
    /// Calling log methods on the null logger should not throw.
    /// </summary>
    [Test]
    public void AllMethods_DoNotThrow()
    {
        IAppLogger logger = NullLogger.Instance;
        Assert.DoesNotThrow(() => logger.Log(LogLevel.Error, "C", "msg", new Exception("test")));
        Assert.DoesNotThrow(() => logger.Error("C", "msg"));
        Assert.DoesNotThrow(() => logger.Warning("C", "msg"));
        Assert.DoesNotThrow(() => logger.Info("C", "msg"));
        Assert.DoesNotThrow(() => logger.Debug("C", "msg"));
    }

    /// <summary>
    /// NullLogger should implement the IAppLogger interface.
    /// </summary>
    [Test]
    public void NullLogger_ImplementsIAppLogger()
    {
        Assert.That(NullLogger.Instance, Is.InstanceOf<IAppLogger>());
    }

    /// <summary>
    /// <see cref="AppLoggerExtensions.For{T}"/> should return a non-null
    /// <see cref="IComponentLogger"/> that does not throw.
    /// </summary>
    [Test]
    public void ForT_ReturnsComponentLogger_ThatDoesNotThrow()
    {
        IComponentLogger log = NullLogger.Instance.For<NullLoggerTests>();
        Assert.That(log, Is.Not.Null);
        Assert.DoesNotThrow(() => log.Error("err"));
        Assert.DoesNotThrow(() => log.Warning("warn"));
        Assert.DoesNotThrow(() => log.Info("info"));
        Assert.DoesNotThrow(() => log.Debug("debug"));
    }
}
