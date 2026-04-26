using SatelliteCore.Configuration;

namespace SatelliteCore.Hosting;

public interface IHostAppDataFacade
{
    AppDataDocument Document { get; }

    bool ExistsSection<T>() where T : AppDataSectionBase, new();
    bool TryGetSection<T>(out T? section) where T : AppDataSectionBase, new();
    T GetOrCreateSection<T>() where T : AppDataSectionBase, new();
    void SetSection<T>(T section) where T : AppDataSectionBase;
    bool RemoveSection<T>() where T : AppDataSectionBase, new();

    Task SaveAsync(CancellationToken ct = default);
    Task SetAndSaveSection<T>(T section, CancellationToken ct = default)
        where T : AppDataSectionBase;
}