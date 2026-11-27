using Ogooreck.API;

namespace Ogooreck.Aspire.Tests.Fixtures;

#region AspireSpecification

public sealed class AspireApi: ApiSpecification, IAsyncDisposable
{
    public static readonly OtlpReceiver Otlp = OtlpReceiver.Start(
        new OtlpReceiverOptions { ForwardTo = TestRunCollector.Endpoint });

    public static readonly AspireApp App = new(Otlp, withKafka: true);

    public AspireApi() : base(App.Api, spec => spec
        .Telemetry(Otlp)
        .Use(App.Kafka)
        .Deadline(TimeSpan.FromSeconds(30)))
    {
    }

    #endregion

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await App.DisposeAsync();
        await Otlp.DisposeAsync();
    }
}
