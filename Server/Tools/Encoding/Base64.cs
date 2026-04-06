using Microsoft.AspNetCore.WebUtilities;

namespace Server.Tools.Encoding;

using SysEncoding = System.Text.Encoding;

public class Base64
{
    #region base64

    public static string ToBase64(byte[] bytes)
    {
        return Convert.ToBase64String(bytes);
    }

    public static string ToBase64(string text)
    {
        var textBytes = SysEncoding.UTF8.GetBytes(text);
        return ToBase64(textBytes);
    }

    public static byte[] FromBase64ToBytes(string base64)
    {
        return Convert.FromBase64String(base64);
    }

    public static string FromBase64ToString(string base64)
    {
        var originalBytes = FromBase64ToBytes(base64);
        return SysEncoding.UTF8.GetString(originalBytes);
    }

    #endregion

    #region base64 url

    public static string ToBase64Url(byte[] bytes)
    {
        return WebEncoders.Base64UrlEncode(bytes);
    }

    public static string ToBase64Url(string text)
    {
        var textBytes = SysEncoding.UTF8.GetBytes(text);
        return ToBase64Url(textBytes);
    }

    public static byte[] FromBase64UrlToBytes(string base64Url)
    {
        return WebEncoders.Base64UrlDecode(base64Url);
    }

    public static string FromBase64UrlToString(string base64Url)
    {
        var originalBytes = FromBase64UrlToBytes(base64Url);
        return SysEncoding.UTF8.GetString(originalBytes);
    }

    #endregion
}