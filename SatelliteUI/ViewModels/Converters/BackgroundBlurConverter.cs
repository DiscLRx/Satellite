using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SatelliteUI.ViewModels.Converters;

public class BackgroundBlurConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var panelBlur = value as double? ?? 0;
        return Math.Round(panelBlur, 2, MidpointRounding.AwayFromZero);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var sliderValue = value as double? ?? 0;
        return Math.Round(sliderValue, 2, MidpointRounding.AwayFromZero);
    }
}