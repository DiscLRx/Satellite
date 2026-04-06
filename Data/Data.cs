using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using DynamicData;
using ReactiveUI;

namespace Data;

public class InstanceCustom(
    string backgroundCustomPathHorizontal,
    string backgroundCustomPathVertical
) : ReactiveObject
{
    public string BackgroundCustomPathHorizontal { get; set; } = backgroundCustomPathHorizontal;
    public string BackgroundCustomPathVertical { get; set; } = backgroundCustomPathVertical;
}

public class Instance : ReactiveObject
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
        Locations.AddRange(locations ?? []);
        IsLocked = isLocked;
        IsRunning = isRunning;
        InstanceCustom = instanceCustom;
    }

    public int Port
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ObservableCollection<Location> Locations
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public bool IsLocked
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = false;

    public string Password
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public ObservableCollection<string> WhiteList
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public bool IsRunning
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = false;

    [JsonIgnore]
    public InstanceCustom? InstanceCustom
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }
}

public class Location : ReactiveObject
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
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string Path
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;
}

public class AppData : ReactiveObject
{
    public ObservableCollection<Instance> Instances
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public bool IsAutoStart
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = false;

    public double PanelOpacity
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 0;

    public double PanelBlur
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 0;

    public bool MinimizeToTray
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = false;
}
