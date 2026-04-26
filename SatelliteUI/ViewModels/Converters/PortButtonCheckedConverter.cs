using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SatelliteUI.ViewModels.Converters;

public class PortButtonCheckedConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var btnPort = values[0] as int? ?? 0;
        var currentInstancePort = values[1] as int? ?? 0;
        return btnPort == currentInstancePort;
    }
}