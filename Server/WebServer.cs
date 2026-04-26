using System;
using System.Net;
using System.Reflection;
using Data;
using Microsoft.AspNetCore.Http.Features;
using Server.Middleware;
using Server.Pages;

namespace Server;

public class WebServer
{
    private readonly WebApplication _app;

    public WebServer(Instance instance, Action saveChange)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddEmptyConfiguration();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = null;
            options.ListenAnyIP(instance.Port);
        });
        builder.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = long.MaxValue;
            options.MultipartHeadersLengthLimit = int.MaxValue;
            options.ValueLengthLimit = int.MaxValue;
        });
        builder.Services.AddControllers().AddApplicationPart(GetType().Assembly);
        builder.Services.AddRazorPages().AddApplicationPart(GetType().Assembly);

        builder.Services.AddSingleton<RuntimeData>(_ => new RuntimeData(instance, saveChange));
        _app = builder.Build();
        _app.UseAuthMiddleware();
        _app.UseRouting();
        _app.MapControllers();
        _app.MapRazorPages();
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        await _app.StartAsync(ct);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        await _app.StopAsync(ct);
    }
}