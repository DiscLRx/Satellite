namespace Server.Tools.Extensions;

public static class StringExtension
{
    public static string TrimSlash(this string text)
    {
        return text.Trim('/').Trim('\\');
    }
    public static string TrimStartSlash(this string text)
    {
        return text.TrimStart('/').TrimStart('\\');
    }
    public static string TrimEndSlash(this string text)
    {
        return text.TrimEnd('/').TrimEnd('\\');
    }
}