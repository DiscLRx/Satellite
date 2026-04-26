using Data;
using Server.Tools.Encoding;
using Server.Tools.Extensions;

namespace Server.Tools;

public sealed record LocationResolvedResult(
    Location Location,
    string LocationName,
    string LocationRoot,
    string RelativePath,
    string FullPath
);

public static class LocationPathResolver
{
    public static bool TryResolve(
        IReadOnlyCollection<Location> locations,
        string locationNameBase64,
        string? relativePathBase64,
        out LocationResolvedResult? resolved
    )
    {
        resolved = null;
        if (string.IsNullOrWhiteSpace(locationNameBase64))
        {
            return false;
        }

        string locationName;
        string relativePath;
        try
        {
            locationName = Base64.FromBase64UrlToString(locationNameBase64);
            relativePath = string.IsNullOrWhiteSpace(relativePathBase64) ? "" : Base64.FromBase64UrlToString(relativePathBase64).TrimSlash();
        }
        catch (FormatException)
        {
            return false;
        }

        var location = locations.SingleOrDefault(loc => loc.Name == locationName);
        if (location is null)
        {
            return false;
        }

        var locationRoot = Path.GetFullPath(location.Path);
        if (
            !PathExtension.SafeCombine(locationRoot, relativePath, out var fullPath)
            || (!Directory.Exists(fullPath) && !File.Exists(fullPath))
        )
        {
            return false;
        }

        resolved = new LocationResolvedResult(
            location,
            locationName,
            locationRoot,
            relativePath,
            fullPath
        );
        return true;
    }
}