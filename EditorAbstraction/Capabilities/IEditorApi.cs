// <copyright file="IEditorApi.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor.Capabilities;

/// <summary>
/// Capability interface for backends that expose a programmatic API beyond
/// basic input/command (e.g. Neovim's RPC API).
/// </summary>
public interface IEditorApi
{
    /// <summary>
    /// Set a global variable.
    /// </summary>
    /// <param name="name">Variable name.</param>
    /// <param name="value">Variable value.</param>
    void SetVariable(string name, string value);

    /// <summary>
    /// Write an error message to the editor's error output.
    /// </summary>
    /// <param name="message">The error message.</param>
    void WriteErrorMessage(string message);
}
