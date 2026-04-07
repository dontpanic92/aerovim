// <copyright file="NeovimRpcClient.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient;

using System.Diagnostics;
using AeroVim.Editor.Diagnostics;

/// <summary>
/// Lowlevel neovim client that is responsible for communicate with Neovim.
/// </summary>
/// <typeparam name="TRedrawEvent">The base redraw event.</typeparam>
public class NeovimRpcClient<TRedrawEvent> : IDisposable
{
    private readonly MsgPackRpc msgPackRpc;
    private readonly IRedrawEventFactory<TRedrawEvent> factory;
    private readonly RedrawEventParser<TRedrawEvent> redrawEventParser;
    private readonly IComponentLogger log;
    private readonly Process process;
    private bool disposedValue = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="NeovimRpcClient{TRedrawEventFactory}"/> class.
    /// </summary>
    /// <param name="path">The path to neovim executable.</param>
    /// <param name="factory">The painter used for drawing UI.</param>
    /// <param name="logger">Application logger.</param>
    /// <param name="workingDirectory">Optional working directory for Neovim.</param>
    /// <param name="fileArgs">Optional file paths to open on startup.</param>
    public NeovimRpcClient(string path, IRedrawEventFactory<TRedrawEvent> factory, IAppLogger logger, string? workingDirectory = null, IReadOnlyList<string>? fileArgs = null)
    {
        this.factory = factory;
        this.log = logger.For<NeovimRpcClient<TRedrawEvent>>();
        this.redrawEventParser = new RedrawEventParser<TRedrawEvent>(factory, logger);

        string arguments = @"--headless --embed --cmd ""let g:gui_aerovim = 1""";
        if (fileArgs is not null)
        {
            foreach (var file in fileArgs)
            {
                arguments += " " + QuoteArgument(file);
            }
        }

        this.process = new Process
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = path,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
                Arguments = arguments,
            },

            EnableRaisingEvents = true,
        };
        this.process.Exited += this.Process_Exited;
        this.process.Start();

        this.msgPackRpc = new MsgPackRpc(
            this.process.StandardInput.BaseStream,
            this.process.StandardOutput.BaseStream,
            this.NotificationDispatcher,
            logger);
        this.msgPackRpc.RpcErrorOccurred += this.OnRpcError;

        this.UI = new API.UI(this.msgPackRpc);
        this.Global = new API.Global(this.msgPackRpc);
    }

    /// <summary>
    /// A delegate type that indicates Neovim exits.
    /// </summary>
    /// <param name="code">The exit code.</param>
    public delegate void NeovimExitedEventHandler(int code);

    /// <summary>
    /// A delegate type that indicates Neovim needs redrawing.
    /// </summary>
    /// <param name="events">The list of redraw event.</param>
    public delegate void RedrawHandler(IList<TRedrawEvent> events);

    /// <summary>
    /// Gets or sets the callback functions that will be called when Neovim crashs.
    /// </summary>
    public NeovimExitedEventHandler? NeovimExited { get; set; }

    /// <summary>
    /// Gets or sets the handler invoked when an RPC call fails with an error
    /// response or a transport exception. Useful for diagnostics.
    /// </summary>
    public MsgPackRpc.RpcErrorHandler? RpcErrorOccurred { get; set; }

    /// <summary>
    /// Gets the apis of UI part.
    /// </summary>
    public API.UI UI { get; }

    /// <summary>
    /// Gets the apis of Global.
    /// </summary>
    public API.Global Global { get; }

    /// <summary>
    /// Gets or sets the Redraw handlers.
    /// </summary>
    public RedrawHandler? Redraw { get; set; }

    /// <summary>
    /// Dispose.
    /// </summary>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Dispose.
    /// </summary>
    /// <param name="disposing">Is Dispose called.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
                this.msgPackRpc?.Dispose();
            }

            this.disposedValue = true;
        }
    }

    private static string QuoteArgument(string arg)
    {
        if (arg.Length > 0 && !arg.Contains(' ') && !arg.Contains('"'))
        {
            return arg;
        }

        return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private void NotificationDispatcher(string name, IList<MsgPack.MessagePackObject> rawEvents)
    {
        if (name != "redraw")
        {
            this.log.Warning($"Unexpected notification received: {name}");
            return;
        }

        try
        {
            this.Redraw?.Invoke(this.redrawEventParser.Parse(rawEvents));
        }
        catch (Exception ex)
        {
            this.log.Error($"Exception while processing '{name}' notification.", ex);
        }
    }

    private void OnRpcError(string method, string errorDescription)
    {
        this.log.Warning($"RPC error on '{method}': {errorDescription}");
        this.RpcErrorOccurred?.Invoke(method, errorDescription);
    }

    private void Process_Exited(object? sender, EventArgs e)
    {
        this.NeovimExited?.Invoke(this.process.ExitCode);
    }
}
