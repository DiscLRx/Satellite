using Data;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Server.Pages;

public class LocationPage : PageModel
{
    private readonly RuntimeData _runtimeData;
    public readonly List<Location> Locations;

    public LocationPage(RuntimeData runtimeData)
    {
        _runtimeData = runtimeData;
        Locations = _runtimeData.Instance.Locations.ToList();
    }

    public void OnGet()
    {
        
    }
}