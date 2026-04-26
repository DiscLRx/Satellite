using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using Data;

namespace SatelliteUI.ViewModels.Converters;

public class CurrentLocationsConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var instances = values[0] as ObservableCollection<Instance>;
        var currentPort = values[1] as int? ?? 0;
        var currentInstance = instances?.SingleOrDefault(inst => inst.Port == currentPort);
        return currentInstance?.Locations ?? [];
    }
}