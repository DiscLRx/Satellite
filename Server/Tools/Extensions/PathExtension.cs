namespace Server.Tools.Extensions;

public static class PathExtension
{
    public static bool SafeCombine(string basePath, string relativePath, out string fullPath)
    {
        try
        {
            basePath = Path.GetFullPath(basePath).TrimEndSlash().Replace('\\', '/') + '/';
            relativePath = relativePath.TrimSlash().Replace('\\', '/');
            fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath))
                .TrimEndSlash()
                .Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                fullPath += '/';
            }
        }
        catch
        {
            fullPath = string.Empty;
            return false;
        }

        if (fullPath.StartsWith(basePath, StringComparison.Ordinal))
        {
            fullPath = fullPath.TrimEndSlash();
            return true;
        }
        fullPath = string.Empty;
        return false;
    }
}
