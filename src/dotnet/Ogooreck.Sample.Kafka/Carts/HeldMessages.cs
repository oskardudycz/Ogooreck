using System.Collections.Concurrent;

namespace Ogooreck.Sample.Kafka.Carts;

public static class HeldMessages
{
    private static readonly ConcurrentDictionary<Guid, TaskCompletionSource> Held = new();

    public static void Release(Guid cartId) => Of(cartId).TrySetResult();

    internal static async Task PublishWhenReleased(Guid cartId, CancellationToken stopping, Func<Task> publish)
    {
        try
        {
            await Of(cartId).Task.WaitAsync(stopping);
            await publish();
        }
        catch (OperationCanceledException) { }
    }

    private static TaskCompletionSource Of(Guid cartId) =>
        Held.GetOrAdd(cartId, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
}
