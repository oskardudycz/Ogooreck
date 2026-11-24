using System.Text.Json;

namespace Ogooreck.API;

internal sealed record StartedHost(HttpMessageHandler Handler, Uri BaseAddress, JsonSerializerOptions Json)
{
    public (HttpClient Client, StepHandler Handler) CreateStepClient(string traceparent, CancellationToken ct)
    {
        var handler = new StepHandler(traceparent, ct) { InnerHandler = Handler };
        return (new HttpClient(handler, disposeHandler: false) { BaseAddress = BaseAddress }, handler);
    }
}
