using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Data;

public class InstanceCustom(
    string backgroundCustomPathHorizontal,
    string backgroundCustomPathVertical
) : ObservableObject
{
    public string BackgroundCustomPathHorizontal { get; set; } = backgroundCustomPathHorizontal;
    public string BackgroundCustomPathVertical { get; set; } = backgroundCustomPathVertical;
}

public class Instance : ObservableObject
{
    public Instance() { }

    public Instance(
        int port,
        bool isLocked,
        ObservableCollection<Location>? locations = null,
        bool isRunning = false,
        InstanceCustom? instanceCustom = null
    )
    {
        Port = port;
        foreach (var loc in locations ?? []) Locations.Add(loc);
        IsLocked = isLocked;
        IsRunning = isRunning;
        InstanceCustom = instanceCustom;
    }

    public int Port
    {
        get;
        set => SetProperty(ref field, value);
    }

    public ObservableCollection<Location> Locations
    {
        get;
        set => SetProperty(ref field, value);
    } = [];

    public bool IsLocked
    {
        get;
        set => SetProperty(ref field, value);
    } = false;

    public string Password
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public ObservableCollection<string> WhiteList
    {
        get;
        set => SetProperty(ref field, value);
    } = [];

    public bool IsRunning
    {
        get;
        set => SetProperty(ref field, value);
    } = false;

    public ConcurrentDictionary<string, string>? VideoFilterScript
    {
        get;
        set => SetProperty(ref field, value);
    }

    [JsonIgnore]
    public InstanceCustom? InstanceCustom
    {
        get;
        set => SetProperty(ref field, value);
    }
}

public class Location : ObservableObject
{
    public Location() { }

    public Location(string name, string path)
    {
        Name = name;
        Path = path;
    }

    public string Name
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string Path
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;
}

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
