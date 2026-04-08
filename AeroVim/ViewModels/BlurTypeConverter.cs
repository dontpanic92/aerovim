// <copyright file="BlurTypeConverter.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.ViewModels;

using System.Globalization;
using AeroVim.Settings;
using Avalonia.Data.Converters;

/// <summary>
/// Converter that maps a <see cref="BlurType"/> enum value to a boolean for
/// radio button two-way binding. Each static instance corresponds to one
/// blur type value.
/// </summary>
internal sealed class BlurTypeConverter : IValueConverter
{
    /// <summary>
    /// Converter instance for the Transparent blur type.
    /// </summary>
    public static readonly BlurTypeConverter Transparent = new(BlurType.Transparent);

    /// <summary>
    /// Converter instance for the Gaussian blur type.
    /// </summary>
    public static readonly BlurTypeConverter Gaussian = new(BlurType.Gaussian);

    /// <summary>
    /// Converter instance for the Acrylic blur type.
    /// </summary>
    public static readonly BlurTypeConverter Acrylic = new(BlurType.Acrylic);

    /// <summary>
    /// Converter instance for the Mica blur type.
    /// </summary>
    public static readonly BlurTypeConverter Mica = new(BlurType.Mica);

    private readonly BlurType targetValue;

    private BlurTypeConverter(BlurType targetValue)
    {
        this.targetValue = targetValue;
    }

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is BlurType blurType && blurType == this.targetValue;
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? this.targetValue : Avalonia.Data.BindingOperations.DoNothing;
    }
}
