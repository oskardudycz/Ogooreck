using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ogooreck.Tests.API.Hosts.Fixtures;

public sealed class EchoServer: IAsyncDisposable
{
    private readonly WebApplication app;

    private EchoServer(WebApplication app, Uri address)
    {
        this.app = app;
        Address = address;
    }

    public Uri Address { get; }

    public static EchoServer Start()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.MapGet("/echo", (HttpContext context) => Results.Json(new
        {
            port = context.Connection.LocalPort,
            traceparent = context.Request.Headers["traceparent"].ToString()
        }));
        app.Start();
        var address = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
            .Addresses.Single());
        return new EchoServer(app, address);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();
}
