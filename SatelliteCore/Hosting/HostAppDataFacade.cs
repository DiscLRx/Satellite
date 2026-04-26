using SatelliteCore.Configuration;

namespace SatelliteCore.Hosting;

internal sealed class HostAppDataFacade : IHostAppDataFacade
{
    private readonly AppDataDocument _document;
    private readonly AppDataService _dataService;
    private readonly Lock _gate = new();

    public HostAppDataFacade(AppDataDocument document, AppDataService dataService)
    {
        _document = document;
        _dataService = dataService;
    }

    public AppDataDocument Document => _document;

    public bool ExistsSection<T>() where T : AppDataSectionBase, new()
    {
        lock (_gate)
            return _document.ExistsSection<T>();
    }

    public bool TryGetSection<T>(out T? section) where T : AppDataSectionBase, new()
    {
        lock (_gate)
            return _document.TryGetSection(out section);
    }

    public T GetOrCreateSection<T>() where T : AppDataSectionBase, new()
    {
        lock (_gate)
            return _document.GetOrCreateSection<T>();
    }

    public void SetSection<T>(T section) where T : AppDataSectionBase
    {
        lock (_gate)
            _document.SetSection(section);
    }

    public bool RemoveSection<T>() where T : AppDataSectionBase, new()
    {
        lock (_gate)
            return _document.RemoveSection<T>();
    }

    public Task SaveAsync(CancellationToken ct = default)
    {
        lock (_gate)
            _dataService.Save(_document);
        return Task.CompletedTask;
    }

    public Task SetAndSaveSection<T>(T section, CancellationToken ct = default)
        where T : AppDataSectionBase
    {
        lock (_gate)
        {
            _document.SetSection(section);
            _dataService.Save(_document);
        }
        return Task.CompletedTask;
    }
}