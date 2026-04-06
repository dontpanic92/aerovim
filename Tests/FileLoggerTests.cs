// <copyright file="FileLoggerTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests;

using AeroVim.Diagnostics;
using AeroVim.Editor.Diagnostics;
using NUnit.Framework;

/// <summary>
/// Tests <see cref="FileLogger"/> file creation, message formatting,
/// rotation, and thread safety.
/// </summary>
public class FileLoggerTests
{
    private string testDir = null!;

    /// <summary>
    /// Creates a unique temporary directory for each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.testDir = Path.Combine(Path.GetTempPath(), "aerovim_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.testDir);
    }

    /// <summary>
    /// Cleans up the temporary directory after each test.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(this.testDir, recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup.
        }
    }

    /// <summary>
    /// Constructing a FileLogger should create the log directory and file.
    /// </summary>
    [Test]
    public void Constructor_CreatesLogFile()
    {
        string logDir = Path.Combine(this.testDir, "logs");
        using var logger = new FileLogger(logDir);

        Assert.That(Directory.Exists(logDir), Is.True);
        Assert.That(logger.LogFilePath, Is.EqualTo(Path.Combine(logDir, "aerovim.log")));
    }

    /// <summary>
    /// Log messages should contain timestamp, level tag, component, and message.
    /// </summary>
    [Test]
    public void Log_WritesFormattedLine()
    {
        string logDir = Path.Combine(this.testDir, "logs");
        using var logger = new FileLogger(logDir);
        IAppLogger log = logger;

        log.Info("TestComponent", "Hello world");
        logger.Dispose();

        string content = File.ReadAllText(logger.LogFilePath);
        Assert.That(content, Does.Contain("[INF]"));
        Assert.That(content, Does.Contain("[TestComponent]"));
        Assert.That(content, Does.Contain("Hello world"));
    }

    /// <summary>
    /// Error-level messages should include the exception details.
    /// </summary>
    [Test]
    public void Log_Error_IncludesException()
    {
        string logDir = Path.Combine(this.testDir, "logs");
        using var logger = new FileLogger(logDir);
        IAppLogger log = logger;

        var ex = new InvalidOperationException("test failure");
        log.Error("RPC", "Something broke", ex);
        logger.Dispose();

        string content = File.ReadAllText(logger.LogFilePath);
        Assert.That(content, Does.Contain("[ERR]"));
        Assert.That(content, Does.Contain("[RPC]"));
        Assert.That(content, Does.Contain("Something broke"));
        Assert.That(content, Does.Contain("test failure"));
        Assert.That(content, Does.Contain("InvalidOperationException"));
    }

    /// <summary>
    /// All four log levels should produce their expected tags.
    /// </summary>
    [Test]
    public void Log_AllLevels_ProduceExpectedTags()
    {
        string logDir = Path.Combine(this.testDir, "logs");
        using var logger = new FileLogger(logDir);
        IAppLogger log = logger;

        log.Debug("C", "d");
        log.Info("C", "i");
        log.Warning("C", "w");
        log.Error("C", "e");
        logger.Dispose();

        string content = File.ReadAllText(logger.LogFilePath);
        Assert.That(content, Does.Contain("[DBG]"));
        Assert.That(content, Does.Contain("[INF]"));
        Assert.That(content, Does.Contain("[WRN]"));
        Assert.That(content, Does.Contain("[ERR]"));
    }

    /// <summary>
    /// Writing after dispose should not throw.
    /// </summary>
    [Test]
    public void Log_AfterDispose_DoesNotThrow()
    {
        string logDir = Path.Combine(this.testDir, "logs");
        var logger = new FileLogger(logDir);
        IAppLogger log = logger;
        logger.Dispose();

        Assert.DoesNotThrow(() => log.Info("C", "after dispose"));
    }

    /// <summary>
    /// When the log file exceeds 5 MB the logger should rotate to a backup.
    /// </summary>
    [Test]
    public void Log_Rotation_CreatesOldLogFile()
    {
        string logDir = Path.Combine(this.testDir, "logs");
        using var logger = new FileLogger(logDir);
        IAppLogger log = logger;
        string logFile = logger.LogFilePath;
        string oldLogFile = Path.Combine(logDir, "aerovim.old.log");

        // Write enough data to exceed the 5 MB threshold.
        string largeMessage = new string('X', 10_000);
        for (int i = 0; i < 600; i++)
        {
            log.Info("Rotation", largeMessage);
        }

        logger.Dispose();

        Assert.That(File.Exists(oldLogFile), Is.True, "Rotation should create aerovim.old.log");
        Assert.That(File.Exists(logFile), Is.True, "A new log file should exist after rotation");

        long oldSize = new FileInfo(oldLogFile).Length;
        Assert.That(oldSize, Is.GreaterThan(1_000_000), "Old log should contain the bulk of the data");
    }

    /// <summary>
    /// Concurrent logging from multiple threads should not corrupt the file.
    /// </summary>
    [Test]
    public void Log_ConcurrentWrites_DoesNotCorrupt()
    {
        string logDir = Path.Combine(this.testDir, "logs");
        using var logger = new FileLogger(logDir);
        IAppLogger log = logger;

        var tasks = new Task[8];
        for (int t = 0; t < tasks.Length; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < 200; i++)
                {
                    log.Info($"Thread{threadId}", $"Message {i}");
                }
            });
        }

        Task.WaitAll(tasks);
        logger.Dispose();

        string[] lines = File.ReadAllLines(logger.LogFilePath);

        // We wrote 8 * 200 = 1600 lines (though some may be in rotated file).
        // Verify each surviving line looks well-formed.
        foreach (string line in lines)
        {
            Assert.That(line, Does.Contain("[INF]"), $"Corrupted line: {line}");
            Assert.That(line, Does.Contain("[Thread"), $"Corrupted line: {line}");
        }
    }
}
