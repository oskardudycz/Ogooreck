using Ogooreck.Assertions;

namespace Ogooreck.API;

/// <summary>One item of Given or When: a request, an attempt, an action or a publish step.</summary>
/// <example>
/// <code>
/// static TestStep PlaceOrder(Guid cartId) =&gt;
///     Request(client =&gt; client.PostAsync($"/carts/{cartId}/orders", null));
/// </code>
/// </example>
public abstract class TestStep
{
    private protected TestStep() { }

    internal virtual bool MayFail => false;

    internal abstract string Kind { get; }

    internal abstract Task<HttpResponseMessage?> Run(HttpClient client, StepContext context, CancellationToken ct);
}

internal sealed class RequestStep(Func<HttpClient, StepContext, Task<HttpResponseMessage>> send): TestStep
{
    internal override string Kind => "request";

    internal override async Task<HttpResponseMessage?> Run(HttpClient client, StepContext context, CancellationToken ct)
    {
        var response = await send(client, context);
        await response.Content.LoadIntoBufferAsync(ct);
        return response;
    }
}

internal sealed class AttemptStep(TestStep step): TestStep
{
    internal override bool MayFail => true;

    internal override string Kind => step.Kind;

    internal override Task<HttpResponseMessage?> Run(HttpClient client, StepContext context, CancellationToken ct) =>
        step.Run(client, context, ct);
}

internal sealed class ActionStep(Func<StepContext, Task> run): TestStep
{
    internal override string Kind => "action";

    internal override async Task<HttpResponseMessage?> Run(HttpClient client, StepContext context, CancellationToken ct)
    {
        using var activity = StepActivity.Start(Kind, context.Traceparent);
        await run(context);
        return null;
    }
}

internal sealed class PublishStep(object[] messages): TestStep
{
    internal override string Kind => "publish";

    internal override async Task<HttpResponseMessage?> Run(HttpClient client, StepContext context, CancellationToken ct)
    {
        var publish = context.Publishing
                      ?? throw new AssertionException(
                          $"{context.Step}: Publish(...) needs a publish function, but none is configured; configure it with Publishing((request, ct) => ...).");
        using var activity = StepActivity.Start(Kind, context.Traceparent);
        await publish(new PublishRequest(messages, context.Traceparent), ct);
        return null;
    }
}

internal sealed class EventuallyStep(RequestStep request): TestStep
{
    internal override string Kind => "eventually request";

    internal override Task<HttpResponseMessage?> Run(HttpClient client, StepContext context, CancellationToken ct) =>
        request.Run(client, context, ct);
}
