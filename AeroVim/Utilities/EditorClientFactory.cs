// <copyright file="EditorClientFactory.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Utilities;

using AeroVim.Editor;
using AeroVim.Settings;

/// <summary>
/// Creates editor backend clients based on application settings.
/// </summary>
public static class EditorClientFactory
{
    /// <summary>
    /// Create an editor client for the configured backend.
    /// </summary>
    /// <param name="settings">Application settings.</param>
    /// <returns>The created editor client.</returns>
    public static IEditorClient Create(AppSettings settings)
    {
        return settings.EditorType switch
        {
            EditorType.Vim => new VimClient.VimClient(settings.VimPath),
            EditorType.Neovim => new NeovimClient.NeovimClient(settings.NeovimPath),
            _ => throw new InvalidOperationException($"Unsupported editor type: {settings.EditorType}"),
        };
    }
}
