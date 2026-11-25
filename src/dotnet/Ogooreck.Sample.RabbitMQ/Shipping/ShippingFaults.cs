using System.Collections.Concurrent;

namespace Ogooreck.Sample.RabbitMQ.Shipping;

public sealed class ShippingFaults
{
    private readonly ConcurrentDictionary<Guid, byte> nackingFirstTry = new();
    private readonly ConcurrentDictionary<Guid, byte> rejectingFirstTry = new();
    private readonly ConcurrentDictionary<Guid, byte> blocking = new();
    private readonly ConcurrentDictionary<Guid, byte> missingExchange = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> held = new();
    private readonly TaskCompletionSource handlerBlocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentDictionary<Guid, byte> tried = new();

    public void NackTheFirstTryOf(Guid orderId) => nackingFirstTry[orderId] = 0;

    public void RejectTheFirstTryOf(Guid orderId) => rejectingFirstTry[orderId] = 0;

    public void BlockTheHandlerOf(Guid orderId) => blocking[orderId] = 0;

    public void PublishToAMissingExchangeFor(Guid orderId) => missingExchange[orderId] = 0;

    public Task HandlerBlocked => handlerBlocked.Task;

    public void BlockedTheHandler() => handlerBlocked.TrySetResult();

    public void HoldShippingOf(Guid orderId) =>
        held[orderId] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public void ReleaseShippingOf(Guid orderId)
    {
        if (held.TryGetValue(orderId, out var holding))
            holding.TrySetResult();
    }

    public bool Blocks(Guid orderId) => blocking.ContainsKey(orderId);

    public bool PublishesToAMissingExchange(Guid orderId) => missingExchange.ContainsKey(orderId);

    public Task ShippingOf(Guid orderId) => held.TryGetValue(orderId, out var holding) ? holding.Task : Task.CompletedTask;

    public FirstTry? FirstTryOf(Guid orderId) =>
        !tried.TryAdd(orderId, 0) ? null
        : nackingFirstTry.ContainsKey(orderId) ? FirstTry.Nack
        : rejectingFirstTry.ContainsKey(orderId) ? FirstTry.Reject
        : null;
}

public enum FirstTry
{
    Nack,
    Reject
}
