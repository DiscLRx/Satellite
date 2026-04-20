using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Satellite.ViewModels.Converters;

public class BackgroundBlurConverter : IValueConverter
{
    // Convert: PanelBlur (0–100) → Slider display value
    // Round to 2 decimal places to ensure ConvertBack(Convert(x)) == x, preventing slider jitter.
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var panelBlur = value as double? ?? 0;
        return Math.Round(panelBlur, 2, MidpointRounding.AwayFromZero);
    }

    // ConvertBack: Slider value → PanelBlur (0–100)
    // Round to 2 decimal places to eliminate floating-point drift in the two-way binding loop.
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var sliderValue = value as double? ?? 0;
        return Math.Round(sliderValue, 2, MidpointRounding.AwayFromZero);
    }
}