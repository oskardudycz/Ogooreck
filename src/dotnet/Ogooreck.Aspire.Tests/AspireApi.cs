using Ogooreck.API;

namespace Ogooreck.Aspire.Tests;

public sealed class AspireApi: ApiSpecification, IAsyncDisposable
{
    public static readonly OtlpReceiver Otlp = OtlpReceiver.Start();

    public static readonly AspireApp App = new(Otlp, withKafka: true);

    public AspireApi() : base(App.Api, spec => spec
        .Telemetry(Otlp)
        .Use(App.Kafka)
        .Deadline(TimeSpan.FromSeconds(30)))
    {
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await App.DisposeAsync();
        await Otlp.DisposeAsync();
    }
}
