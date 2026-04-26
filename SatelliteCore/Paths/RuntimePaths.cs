namespace SatelliteCore.Paths;

public class RuntimePaths(string baseDirectory)
{
    public string DataFilePath { get; } = Path.GetFullPath("appdata.json", baseDirectory);
    public string InstancesCustomRoot { get; } = Path.GetFullPath("Custom/Instances", baseDirectory);

    public string GetInstanceCustomRoot(int port) =>
        Path.Combine(InstancesCustomRoot, port.ToString());

    public string GetInstanceBackgroundHorizontal(int port) =>
        Path.Combine(GetInstanceCustomRoot(port), "Background", "Horizontal");

    public string GetInstanceBackgroundVertical(int port) =>
        Path.Combine(GetInstanceCustomRoot(port), "Background", "Vertical");
}
