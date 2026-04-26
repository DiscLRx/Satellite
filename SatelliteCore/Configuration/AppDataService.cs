using System.Text.Json;

namespace SatelliteCore.Configuration;

public class AppDataService
{
    private readonly string _filePath;
    private readonly Lock _ioLock = new();
    private const string TemplateResourceName = "SatelliteCore.appdata.template.json";

    public AppDataService(string filePath)
    {
        _filePath = filePath;
    }

    public AppDataDocument Load()
    {
        lock (_ioLock)
        {
            EnsureFileExists();
            var json = File.ReadAllText(_filePath);
            var document = Deserialize(json);

            var changed = false;
            if (!document.ExistsSection<SatelliteUiDefaults>())
            {
                document.SetSection(new SatelliteUiDefaults());
                changed = true;
            }

            if (!document.ExistsSection<SatelliteFlexDefaults>())
            {
                document.SetSection(new SatelliteFlexDefaults());
                changed = true;
            }

            if (changed)
                Save(document);

            return document;
        }
    }

    public void Save(AppDataDocument document)
    {
        lock (_ioLock)
        {
            EnsureDirectoryExists();
            var json = Serialize(document);
            var tmp = _filePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _filePath, overwrite: true);
        }
    }

    private void EnsureFileExists()
    {
        EnsureDirectoryExists();
        if (File.Exists(_filePath))
            return;

        using var stream = typeof(AppDataService).Assembly.GetManifestResourceStream(TemplateResourceName)
            ?? throw new FileNotFoundException(
                $"Embedded resource '{TemplateResourceName}' was not found.");

        using var reader = new StreamReader(stream);
        File.WriteAllText(_filePath, reader.ReadToEnd());
    }

    private void EnsureDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }

    private static AppDataDocument Deserialize(string json)
        => JsonSerializer.Deserialize<AppDataDocument>(json)
            ?? throw new InvalidOperationException("appdata.json deserialized to null.");

    private static string Serialize(AppDataDocument document)
        => JsonSerializer.Serialize(document);

    private sealed class SatelliteUiDefaults : AppDataSectionBase
    {
        public const string SectionName = "SatelliteUI";
        public bool IsAutoStart { get; set; } = false;
        public double PanelOpacity { get; set; } = 40;
        public double PanelBlur { get; set; } = 10;
        public bool MinimizeToTray { get; set; } = false;
    }

    private sealed class SatelliteFlexDefaults : AppDataSectionBase
    {
        public const string SectionName = "SatelliteFlex";
        public int IpcTimeoutMs { get; set; } = 5000;
        public int InstanceStartTimeoutMs { get; set; } = 15000;
    }
}
