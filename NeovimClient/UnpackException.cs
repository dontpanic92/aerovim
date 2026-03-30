// <copyright file="UnpackException.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace MsgPack;

/// <summary>
/// Compatibility exception wrapper.
/// </summary>
public sealed class UnpackException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnpackException"/> class.
    /// </summary>
    /// <param name="message">Exception message.</param>
    /// <param name="innerException">Inner exception.</param>
    public UnpackException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
