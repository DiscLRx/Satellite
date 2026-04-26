using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Data;

namespace SatelliteCore.Configuration;

public class AppDataDocument
{
    public ObservableCollection<Instance> Instances { get; set; } = [];

[JsonExtensionData]
    public Dictionary<string, JsonElement> Sections { get; set; } = [];

    public bool ExistsSection<T>() where T : AppDataSectionBase, new() =>
        Sections.ContainsKey(ResolveSectionName(typeof(T)));

    public bool TryGetSection<T>(out T? section) where T : AppDataSectionBase, new()
    {
        var sectionName = ResolveSectionName(typeof(T));
        if (!Sections.TryGetValue(sectionName, out var node))
        {
            section = default;
            return false;
        }

        section = node.Deserialize<T>();
        return section is not null;
    }

    public T GetOrCreateSection<T>() where T : AppDataSectionBase, new()
    {
        if (TryGetSection<T>(out var existing) && existing is not null)
            return existing;
        var created = new T();
        SetSection(created);
        return created;
    }

    public void SetSection<T>(T section) where T : AppDataSectionBase
    {
        var sectionName = ResolveSectionName(typeof(T));
        var json = JsonSerializer.SerializeToElement(section);
        Sections[sectionName] = json;
    }

    public bool RemoveSection<T>() where T : AppDataSectionBase, new() =>
        Sections.Remove(ResolveSectionName(typeof(T)));

    private static string ResolveSectionName(Type type)
    {
        var field = type.GetField("SectionName", BindingFlags.Public | BindingFlags.Static);
        return field?.GetValue(null) as string ?? type.Name;
    }
}
