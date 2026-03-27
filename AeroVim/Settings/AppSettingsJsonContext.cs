// <copyright file="AppSettingsJsonContext.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Settings
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Source-generated JSON metadata for <see cref="AppSettings"/>.
    /// </summary>
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(AppSettings))]
    internal sealed partial class AppSettingsJsonContext : JsonSerializerContext
    {
    }
}
