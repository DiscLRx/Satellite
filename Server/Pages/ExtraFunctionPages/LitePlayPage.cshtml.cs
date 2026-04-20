using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Server.Tools;

namespace Server.Pages.ExtraFunctionPages;

public class LitePlayPage(RuntimeData runtimeData) : PageModel
{
    private readonly RuntimeData _runtimeData = runtimeData;

    public enum PlayMode
    {
        Sequence,
        Shuffle,
        Repeat,
    }

    public string LocationName { get; private set; } = "";
    public string BasePath { get; private set; } = "";
    public string LocationBase64 { get; private set; } = "";
    public string PathBase64 { get; private set; } = "";
    public string LocationVideoFilterScriptsJson { get; private set; } = "{}";
    public PlayMode InitialPlayMode { get; private set; } = PlayMode.Sequence;

    public IActionResult OnGet(string locationNameBase64, string relativePathBase64 = "")
    {
        if (
            !LocationPathResolver.TryResolve(
                _runtimeData.Instance.Locations,
                locationNameBase64,
                relativePathBase64,
                out var resolvedPath
            )
        )
        {
            return BadRequest();
        }

        var resolved = resolvedPath!;

        LocationName = resolved.LocationName;
        BasePath = resolved.RelativePath;
        LocationBase64 = locationNameBase64;
        PathBase64 = relativePathBase64;
        SetLocationVideoFilterScripts();

        return Page();
    }

    private void SetLocationVideoFilterScripts()
    {
        var scripts = _runtimeData.Instance.VideoFilterScript ?? [];
        LocationVideoFilterScriptsJson = JsonSerializer.Serialize(scripts);
    }
}
