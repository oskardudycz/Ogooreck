using System.Collections.Concurrent;
using Ogooreck.Sample.Wolverine.Carts.Expiring;
using Ogooreck.Sample.Wolverine.Events;
using Wolverine;

namespace Ogooreck.Sample.Wolverine.Carts;

public class CartStore(TimeProvider clock, IServiceProvider services)
{
    private static readonly TimeSpan ExpiresAfter = TimeSpan.FromDays(1);

    private readonly ConcurrentDictionary<Guid, List<string>> carts = new();
    private readonly ConcurrentDictionary<Guid, ITimer> expirations = new();

    public void Add(Guid cartId, string product)
    {
        var products = carts.GetOrAdd(cartId, _ => []);
        lock (products)
            products.Add(product);

        expirations.GetOrAdd(cartId,
            id => clock.CreateTimer(_ => Expire(id), null, ExpiresAfter, Timeout.InfiniteTimeSpan));
    }

    public IReadOnlyList<string> Products(Guid cartId)
    {
        if (!carts.TryGetValue(cartId, out var products))
            return [];
        lock (products)
            return [.. products];
    }

    public static OrderPlaced OrderFor(Guid cartId, IReadOnlyList<string> products) =>
        new(cartId) { Total = 5 * products.Count, Products = products };

    private void Expire(Guid cartId)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(new ExpireCart(cartId)).AsTask()
            .GetAwaiter().GetResult();
    }
}
