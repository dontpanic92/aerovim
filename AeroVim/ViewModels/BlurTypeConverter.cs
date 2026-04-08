// <copyright file="BlurTypeConverter.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.ViewModels;

using System.Globalization;
using Avalonia.Data.Converters;

/// <summary>
/// Converter that maps a blur type integer to a boolean for radio button
/// two-way binding. Each static instance corresponds to one blur type value.
/// </summary>
internal sealed class BlurTypeConverter : IValueConverter
{
    /// <summary>
    /// Converter instance for the Transparent blur type (3).
    /// </summary>
    public static readonly BlurTypeConverter Transparent = new(3);

    /// <summary>
    /// Converter instance for the Gaussian blur type (0).
    /// </summary>
    public static readonly BlurTypeConverter Gaussian = new(0);

    /// <summary>
    /// Converter instance for the Acrylic blur type (1).
    /// </summary>
    public static readonly BlurTypeConverter Acrylic = new(1);

    /// <summary>
    /// Converter instance for the Mica blur type (2).
    /// </summary>
    public static readonly BlurTypeConverter Mica = new(2);

    private readonly int targetValue;

    private BlurTypeConverter(int targetValue)
    {
        this.targetValue = targetValue;
    }

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int intVal && intVal == this.targetValue;
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? this.targetValue : Avalonia.Data.BindingOperations.DoNothing;
    }
}
