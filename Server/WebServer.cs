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
    public WebServer(Instance instance)
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

        builder.Services.AddSingleton<RuntimeData>(_ => new RuntimeData(instance));
        _app = builder.Build();
        _app.UseAuthMiddleware();
        _app.UseRouting();
        _app.MapControllers();
        _app.MapRazorPages();
    }

    public async Task StartAsync()
    {
        Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var tsc = new TaskCompletionSource<bool>();
        _app.Lifetime.ApplicationStarted.Register(() => tsc.SetResult(true));
        _ = _app.RunAsync();
        await tsc.Task;
    }

    public async Task StopAsync()
    {
        await _app.StopAsync();
    }
}