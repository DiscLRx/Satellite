namespace Server.Tools.Extensions;

public static class StringExtension
{
    public static string TrimSlash(this string text)
    {
        return text.Trim('/').Trim('\\');
    }
}