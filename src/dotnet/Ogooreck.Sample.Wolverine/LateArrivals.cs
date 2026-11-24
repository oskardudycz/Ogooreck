using System.Collections.Concurrent;
using Ogooreck.Sample.Wolverine.Events;
using Wolverine;

namespace Ogooreck.Sample.Wolverine;

public class LateArrivals(IServiceProvider services)
{
    private readonly ConcurrentDictionary<Guid, (TaskCompletionSource Gate, Task Published)> held = new();

    public void Hold(Guid cartId)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var published = Task.Run(async () =>
        {
            await gate.Task;
            using var scope = services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(new LateArrived(cartId));
        });
        held[cartId] = (gate, published);
    }

    public Task Release(Guid cartId)
    {
        if (!held.TryRemove(cartId, out var arrival))
            return Task.CompletedTask;
        arrival.Gate.SetResult();
        return arrival.Published;
    }
}
