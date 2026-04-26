using SatelliteCore.Configuration;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SatelliteUI.Configuration;

public class SatelliteUIData : AppDataSectionBase, INotifyPropertyChanged
{
    public const string SectionName = "SatelliteUI";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private bool _isAutoStart = false;
    public bool IsAutoStart { get => _isAutoStart; set => SetField(ref _isAutoStart, value); }

    private double _panelOpacity = 40;
    public double PanelOpacity { get => _panelOpacity; set => SetField(ref _panelOpacity, value); }

    private double _panelBlur = 10;
    public double PanelBlur { get => _panelBlur; set => SetField(ref _panelBlur, value); }

    private bool _minimizeToTray = false;
    public bool MinimizeToTray { get => _minimizeToTray; set => SetField(ref _minimizeToTray, value); }
}
