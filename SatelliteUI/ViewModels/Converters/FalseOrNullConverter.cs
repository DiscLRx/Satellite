using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SatelliteUI.ViewModels.Converters;

public class FalseOrNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var boolValue = value as bool?;
        if (boolValue is null) return true;
        return !boolValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}