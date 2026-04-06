using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Data;
using Satellite.Service;

namespace Satellite.ViewModels.Converters;

public class InstanceLockedConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool isLocked) return "🔓";
        return isLocked ? "🔒" : "🔓";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            "🔒" => true,
            "🔓" => false,
            _ => false
        };
    }
}