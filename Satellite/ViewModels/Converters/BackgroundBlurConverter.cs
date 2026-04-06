using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Satellite.ViewModels.Converters;

public class BackgroundBlurConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var y = value as double? ?? 1;
        var x = Math.Pow(y / 0.004, 1 / 2.2);
        return System.Convert.ToInt32(x);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var x = value as double? ?? 1;
        var y = Math.Pow(x, 2.2) * 0.004;
        return y;
    }
}