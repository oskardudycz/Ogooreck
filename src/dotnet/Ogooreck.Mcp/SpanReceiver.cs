using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ogooreck.Mcp;

public sealed class SpanReceiver: IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly CollectedSpans spans;

    private SpanReceiver(WebApplication app, CollectedSpans spans)
    {
        this.app = app;
        this.spans = spans;
        app.MapPost("/v1/traces", Receive);
        app.MapPost("/v1/metrics", () => Results.Bytes([], "application/x-protobuf"));
        app.Start();
        Endpoint = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses
            .Single());
    }

    public Uri Endpoint { get; }

    public static SpanReceiver Start(CollectedSpans spans)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        return new SpanReceiver(builder.Build(), spans);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private async Task<IResult> Receive(HttpRequest request)
    {
        using var body = new MemoryStream();
        await request.Body.CopyToAsync(body, request.HttpContext.RequestAborted);
        spans.Add(OtlpSpans.Decode(body.ToArray()));
        return Results.Bytes([], "application/x-protobuf");
    }
}
