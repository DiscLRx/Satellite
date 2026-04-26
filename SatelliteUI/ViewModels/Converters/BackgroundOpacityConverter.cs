using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SatelliteUI.ViewModels.Converters;

public class BackgroundOpacityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var percent = (double)(value ?? 0);
        var rate = percent / 100;
        var a = System.Convert.ToInt32(Math.Round(rate * 255, 0));
        return new SolidColorBrush(new Color((byte)a, 0, 0, 0));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var colorBrush = value as SolidColorBrush;
        if (colorBrush is null)
        {
            return 0d;
        }

        var percent = colorBrush.Color.A / 255d * 100d;
        return Math.Round(percent, 2, MidpointRounding.AwayFromZero);
    }
}