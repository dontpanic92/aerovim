// <copyright file="EditorClientFactory.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Utilities;

using System.ComponentModel;
using AeroVim.Editor;
using AeroVim.Editor.Diagnostics;
using AeroVim.Services;

/// <summary>
/// Creates editor backend clients based on application settings.
/// </summary>
public static class EditorClientFactory
{
    /// <summary>
    /// Create an editor client for the configured backend.
    /// Validates the executable path before attempting to start the backend
    /// and wraps platform-level failures in typed
    /// <see cref="EditorStartupException"/> subclasses.
    /// </summary>
    /// <param name="settings">Application settings.</param>
    /// <param name="logger">Logger for the backend to use.</param>
    /// <param name="fileArgs">Optional file paths to open on startup.</param>
    /// <returns>The created editor client.</returns>
    /// <exception cref="EditorNotFoundException">The editor path is missing or does not exist.</exception>
    /// <exception cref="EditorLaunchException">The process could not be started.</exception>
    public static IEditorClient Create(AppSettings settings, IAppLogger logger, IReadOnlyList<string>? fileArgs = null)
    {
        string editorName = settings.EditorType == EditorType.Vim ? "Vim" : "Neovim";
        string path = settings.EditorType == EditorType.Vim ? settings.VimPath : settings.NeovimPath;

        string? validationError = EditorPathDetector.ValidateEditorPath(settings.EditorType, path);
        if (validationError is not null)
        {
            throw new EditorNotFoundException(editorName, path);
        }

        try
        {
            return settings.EditorType switch
            {
                EditorType.Vim => new VimClient.VimClient(settings.VimPath, logger, initialBackgroundColor: settings.BackgroundColor, fileArgs: fileArgs),
                EditorType.Neovim => new NeovimClient.NeovimClient(settings.NeovimPath, logger, fileArgs: fileArgs, enableExternalCmdline: settings.EnableExternalUI, enableExternalPopupmenu: settings.EnableExternalUI),
                _ => throw new InvalidOperationException($"Unsupported editor type: {settings.EditorType}"),
            };
        }
        catch (Win32Exception ex)
        {
            throw new EditorLaunchException(editorName, path, ex);
        }
        catch (FileNotFoundException ex)
        {
            throw new EditorNotFoundException(editorName, path, ex);
        }
    }
}
