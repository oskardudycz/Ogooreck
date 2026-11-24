namespace Ogooreck.API;

internal sealed class StepHandler(string traceparent, CancellationToken stepCancellation): DelegatingHandler
{
    private const string Traceparent = "traceparent";

    public HttpRequestMessage? LastRequest { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        request.Headers.Remove(Traceparent);
        request.Headers.TryAddWithoutValidation(Traceparent, traceparent);

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stepCancellation);
        return await base.SendAsync(request, cancellation.Token);
    }
}
