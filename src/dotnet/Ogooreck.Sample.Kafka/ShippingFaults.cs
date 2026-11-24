using System.Collections.Concurrent;

namespace Ogooreck.Sample.Kafka;

public sealed class ShippingFaults
{
    private readonly ConcurrentDictionary<Guid, byte> failingFirstTry = new();

    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> held = new();

    private readonly TaskCompletionSource handlerBlocked = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task HandlerBlocked => handlerBlocked.Task;

    public void BlockedTheHandler() => handlerBlocked.TrySetResult();

    public void FailFirstTryOf(Guid cartId) => failingFirstTry[cartId] = 0;

    public void HoldShippingOf(Guid cartId) =>
        held[cartId] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public void ReleaseShippingOf(Guid cartId)
    {
        if (held.TryGetValue(cartId, out var holding))
            holding.TrySetResult();
    }

    public Task ShippingOf(Guid cartId) => held.TryGetValue(cartId, out var holding) ? holding.Task : Task.CompletedTask;

    public bool Fails(Guid cartId, int attempt) => attempt == 1 && failingFirstTry.ContainsKey(cartId);
}
