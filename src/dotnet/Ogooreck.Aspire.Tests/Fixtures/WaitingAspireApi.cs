using Ogooreck.API;

namespace Ogooreck.Aspire.Tests.Fixtures;

public sealed class WaitingAspireApi: ApiSpecification, IAsyncDisposable
{
    public static readonly OtlpReceiver Otlp = OtlpReceiver.Start(new OtlpReceiverOptions { WaitForMetricsExport = true });

    public static readonly AspireApp App = new(Otlp, withKafka: false);

    public WaitingAspireApi() : base(App.Api, spec => spec.Telemetry(Otlp))
    {
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await App.DisposeAsync();
        await Otlp.DisposeAsync();
    }
}
