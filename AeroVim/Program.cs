// <copyright file="Program.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim;

using System.Runtime.InteropServices;
using AeroVim.Diagnostics;
using Avalonia;

/// <summary>
/// The Main program.
/// </summary>
public static class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    [STAThread]
    public static void Main(string[] args)
    {
        string logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AeroVim",
            "logs");
        AppLogger.Initialize(new FileLogger(logDir));

        var logger = AppLogger.Instance;
        logger.Info("Startup", $"AeroVim starting — OS={RuntimeInformation.OSDescription}, Runtime={RuntimeInformation.FrameworkDescription}");

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            logger.Info("Startup", "AeroVim shutting down.");
            AppLogger.Shutdown();
        }
    }

    /// <summary>
    /// Builds the Avalonia application configuration.
    /// </summary>
    /// <returns>The configured AppBuilder.</returns>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
