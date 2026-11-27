using Google.Protobuf;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ogooreck.API;

/// <summary>
/// A telemetry source for an app that runs out of process, such as an Aspire app. It hosts an OTLP/HTTP protobuf receiver
/// (<c>POST /v1/traces</c> and <c>POST /v1/metrics</c>) on a random loopback port. Create it first, give <see cref="Endpoint"/> to the app,
/// then pass it to <c>Telemetry(...)</c>. It belongs to you: dispose it yourself, so one receiver can serve several specifications.
/// </summary>
/// <remarks>
/// It reads span ids, names and string, int, double and bool attributes, and the data points of sums, gauges and histograms (the count of a histogram point).
/// Attribute values are written as Java's <c>String.valueOf</c> does. Sums and histograms are delta or cumulative as their aggregation temporality says; gauges are cumulative.
/// Measurements carry no trace id, so <c>ExpectMetric</c> counts them by tags only; filter by a tag unique to the test.
/// It accepts only <c>application/x-protobuf</c>, so set <c>OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf</c> on the app.
/// </remarks>
/// <example>
/// <code>
/// static readonly OtlpReceiver Otlp = OtlpReceiver.Start();
///
/// // pass Otlp.Endpoint to the app as OTEL_EXPORTER_OTLP_ENDPOINT, then:
/// await using var api = ApiSpecification.For(async () => app.GetEndpoint("api")).Telemetry(Otlp);
/// </code>
/// </example>
public sealed class OtlpReceiver: ITelemetrySource
{
    private const string ProtobufContentType = "application/x-protobuf";

    private static readonly HttpClient Forwarding = new();

    private readonly WebApplication app;
    private readonly Uri? forwardTo;
    private readonly string? serviceName;
    private readonly TimeProvider clock;
    private readonly object gate = new();
    private readonly List<(Recorded<RecordedSpan> Spans, Recorded<RecordedMeasurement> Measurements)> sinks = [];
    private readonly Dictionary<string, Sender> senders = [];
    private TaskCompletionSource nextMetricsExport = NewExport();

    private OtlpReceiver(WebApplication app, OtlpReceiverOptions options, TimeProvider clock)
    {
        this.app = app;
        forwardTo = options.ForwardTo;
        serviceName = options.ServiceName;
        this.clock = clock;
        WaitsForMetricsExport = options.WaitForMetricsExport;
        MapExports();
        app.Start();
        Endpoint = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses
            .Single());
    }

    internal bool WaitsForMetricsExport { get; }

    /// <summary>The base address to give to the app as <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>. The exporter appends <c>v1/traces</c> and <c>v1/metrics</c>.</summary>
    /// <example>
    /// <code>
    /// environment["OTEL_EXPORTER_OTLP_ENDPOINT"] = Otlp.Endpoint.ToString();
    /// </code>
    /// </example>
    public Uri Endpoint { get; }

    /// <summary>Starts a receiver that records what the app exports. <see cref="ITelemetrySource.Collect"/> returns at once.</summary>
    /// <example>
    /// <code>
    /// static readonly OtlpReceiver Otlp = OtlpReceiver.Start();
    /// </code>
    /// </example>
    public static OtlpReceiver Start() => Start(new OtlpReceiverOptions());

    /// <summary>Starts a receiver with the given options, for example <see cref="OtlpReceiverOptions.WaitForMetricsExport"/>.</summary>
    /// <example>
    /// <code>
    /// OtlpReceiver.Start(new OtlpReceiverOptions { WaitForMetricsExport = true })
    /// </code>
    /// </example>
    public static OtlpReceiver Start(OtlpReceiverOptions options) => Start(options, TimeProvider.System);

    internal static OtlpReceiver Start(OtlpReceiverOptions options, TimeProvider clock)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        return new OtlpReceiver(app, options, clock);
    }

    /// <inheritdoc />
    /// <example>
    /// <code>
    /// For(async () =&gt; app.GetEndpoint("api")).Telemetry(Otlp) // attaches Otlp on the first step
    /// </code>
    /// </example>
    public void Attach(Recorded<RecordedSpan> spans, Recorded<RecordedMeasurement> measurements)
    {
        lock (gate)
            sinks.Add((spans, measurements));
    }

    /// <summary>
    /// Returns at once, or with <see cref="OtlpReceiverOptions.WaitForMetricsExport"/> waits for two metrics exports from each process that is exporting.
    /// The export that is in flight when it is called may have been read before the app's latest increment, so only the second one is certain to carry it.
    /// A process is told apart by its <c>service.instance.id</c>, or its <c>service.name</c> without one. It is exporting when it sent two exports and the last one
    /// came within twice the time between them; a process that stays silent longer is no longer waited for, and one seen only once isn't waited for.
    /// </summary>
    /// <example>
    /// <code>
    /// Otlp.Collect(ct) // called by the specification before the baseline and before each ExpectMetric
    /// </code>
    /// </example>
    public async Task Collect(CancellationToken ct)
    {
        if (!WaitsForMetricsExport)
            return;

        Dictionary<string, long> before;
        lock (gate)
            before = senders.ToDictionary(sender => sender.Key, sender => sender.Value.Exports);

        while (true)
        {
            Task next;
            TimeSpan? untilSilent;
            lock (gate)
            {
                next = nextMetricsExport.Task;
                var now = clock.GetUtcNow();
                var delivered = senders.Where(sender => sender.Value.Exports - before.GetValueOrDefault(sender.Key) >= 2)
                    .Select(sender => sender.Key).ToHashSet();
                var awaited = senders.Where(sender => !delivered.Contains(sender.Key))
                    .Select(sender => sender.Value.SilentAt - now)
                    .Where(remaining => remaining > TimeSpan.Zero).ToList();
                if (delivered.Count > 0 && awaited.Count == 0)
                    return;
                untilSilent = awaited.Count > 0 ? awaited.Min() : null;
            }

            try
            {
                await next.WaitAsync(untilSilent ?? Timeout.InfiniteTimeSpan, clock, ct);
            }
            catch (TimeoutException)
            {
            }
        }
    }

    /// <summary>Stops the receiver. Exports sent afterwards are refused.</summary>
    /// <example>
    /// <code>
    /// await Otlp.DisposeAsync();
    /// </code>
    /// </example>
    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static async Task Forward(Uri endpoint, byte[] export, CancellationToken ct)
    {
        var content = new ByteArrayContent(export);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(ProtobufContentType);
        try
        {
            using var _ = await Forwarding.PostAsync(endpoint, content, ct);
        }
        catch (HttpRequestException)
        {
        }
    }

    private static TaskCompletionSource NewExport() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void MapExports()
    {
        app.MapPost("/v1/traces", (HttpRequest request) => Receive(request, bytes =>
        {
            var spans = OtlpDecoder.Spans(bytes);
            foreach (var sink in Sinks())
                foreach (var span in spans)
                    sink.Spans.Add(span);
        }));
        app.MapPost("/v1/metrics", (HttpRequest request) => Receive(request, bytes =>
        {
            var measurements = OtlpDecoder.Measurements(bytes);
            foreach (var sink in Sinks())
                foreach (var measurement in measurements)
                    sink.Measurements.Add(measurement);
            CountExport(OtlpDecoder.Senders(bytes));
            SignalMetricsExport();
        }));
    }

    private void CountExport(List<(string? Service, string? Instance)> exporting)
    {
        var now = clock.GetUtcNow();
        lock (gate)
            foreach (var (service, instance) in exporting.Where(each => serviceName is null || each.Service == serviceName))
            {
                var key = instance ?? service ?? "";
                if (!senders.TryGetValue(key, out var sender))
                    senders[key] = sender = new Sender();
                sender.Exported(now);
            }
    }

    private sealed class Sender
    {
        private DateTimeOffset? last;

        public long Exports { get; private set; }

        public DateTimeOffset SilentAt { get; private set; } = DateTimeOffset.MinValue;

        public void Exported(DateTimeOffset at)
        {
            if (last is { } previous)
                SilentAt = at + 2 * (at - previous);
            last = at;
            Exports++;
        }
    }

    private (Recorded<RecordedSpan> Spans, Recorded<RecordedMeasurement> Measurements)[] Sinks()
    {
        lock (gate)
            return sinks.ToArray();
    }

    private void SignalMetricsExport()
    {
        TaskCompletionSource arrived;
        lock (gate)
        {
            arrived = nextMetricsExport;
            nextMetricsExport = NewExport();
        }

        arrived.SetResult();
    }

    private async Task<IResult> Receive(HttpRequest request, Action<byte[]> record)
    {
        if (request.ContentType?.StartsWith(ProtobufContentType, StringComparison.OrdinalIgnoreCase) != true)
            return Results.Text(
                $"The OTLP receiver accepts only {ProtobufContentType}, but the request has content type {request.ContentType ?? "none"}. Set OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf on the app.",
                statusCode: StatusCodes.Status415UnsupportedMediaType);

        using var body = new MemoryStream();
        await request.Body.CopyToAsync(body, request.HttpContext.RequestAborted);
        try
        {
            record(body.ToArray());
        }
        catch (InvalidProtocolBufferException exception)
        {
            return Results.Text($"The request body is not a valid protobuf message: {exception.Message}",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (forwardTo is { } target)
            await Forward(new Uri(target, request.Path.ToString().TrimStart('/')), body.ToArray(),
                request.HttpContext.RequestAborted);

        return Results.Bytes([], ProtobufContentType);
    }
}
