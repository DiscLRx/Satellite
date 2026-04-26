namespace SatelliteCore.Paths;

public static class SatelliteRuntimeRoot
{
    public static string GetRootDirectory()
    {
        var root = AppContext.BaseDirectory;
        Directory.CreateDirectory(root);
        return root;
    }
}
