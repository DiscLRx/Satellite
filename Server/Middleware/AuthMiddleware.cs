using System.Net;

namespace Server.Middleware;

public class AuthMiddleware(RequestDelegate next, RuntimeData runtimeData)
{
    private readonly RequestDelegate _next = next;
    private readonly RuntimeData _runtimeData = runtimeData;

    public async Task Invoke(HttpContext httpContext)
    {
        if (IsAnonymousAllowedPath(httpContext.Request.Path))
        {
            await _next(httpContext);
            return;
        }

        if (IsInWhiteList(httpContext.Connection.RemoteIpAddress))
        {
            await _next(httpContext);
            return;
        }

        if (_runtimeData.Instance.IsLocked)
        {
            var requestKey = GetKeyFromCookies(httpContext);
            if (_runtimeData.Instance.Password != requestKey)
            {
                var originalPath = httpContext.Request.Path;
                httpContext.Request.Path = $"/auth";
                httpContext.Request.QueryString = new QueryString($"?original={originalPath}");
                await _next(httpContext);
                return;
            }
        }

        await _next(httpContext);
    }

    private static bool IsAnonymousAllowedPath(PathString path)
    {
        return path.StartsWithSegments("/a");
    }

    private bool IsInWhiteList(IPAddress? remoteIp)
    {
        if (remoteIp is null)
        {
            return false;
        }

        var whiteList = _runtimeData.Instance.WhiteList;

        if (remoteIp.IsIPv4MappedToIPv6)
        {
            remoteIp = remoteIp.MapToIPv4();
        }

        var isInWhiteList = whiteList.Any(ip =>
        {
            var canParse = IPAddress.TryParse(ip, out var whiteIp);
            if (canParse && whiteIp is not null)
            {
                if (whiteIp.IsIPv4MappedToIPv6)
                {
                    whiteIp = whiteIp.MapToIPv4();
                }

                return whiteIp.Equals(remoteIp);
            }

            return false;
        });
        return isInWhiteList;
    }

    private static string? GetKeyFromCookies(HttpContext httpContext)
    {
        return httpContext.Request.Cookies.TryGetValue("Auth-Key", out var key) ? key : null;
    }
}

public static class AuthExtensions
{
    public static IApplicationBuilder UseAuthMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuthMiddleware>();
    }
}