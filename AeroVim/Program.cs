// <copyright file="Program.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim;

using System.Runtime.InteropServices;
using AeroVim.Diagnostics;
using Avalonia;
using Velopack;

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
        // Velopack must be the first call — it handles install/update hooks
        // and exits immediately when invoked by the updater process.
        VelopackApp.Build().Run();

        string logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AeroVim",
            "logs");
        AppLogger.Initialize(new FileLogger(logDir));

        var log = AppLogger.For("Startup");
        log.Info($"AeroVim starting — OS={RuntimeInformation.OSDescription}, Runtime={RuntimeInformation.FrameworkDescription}");

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            log.Info("AeroVim shutting down.");
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
