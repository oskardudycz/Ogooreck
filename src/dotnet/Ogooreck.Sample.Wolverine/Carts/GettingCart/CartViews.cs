using System.Collections.Concurrent;

namespace Ogooreck.Sample.Wolverine.Carts.GettingCart;

public record CartView(IReadOnlyList<string> Products);

public class CartViews
{
    private readonly ConcurrentDictionary<Guid, CartView> views = new();
    private readonly Lock gate = new();
    private int changing;
    private TaskCompletionSource upToDate = Completed();

    public CartView Get(Guid cartId) => views.GetValueOrDefault(cartId, new CartView([]));

    public void Changing()
    {
        lock (gate)
        {
            if (changing++ == 0)
                upToDate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void Apply(Guid cartId, string product)
    {
        views.AddOrUpdate(cartId, _ => new CartView([product]), (_, view) => new CartView([.. view.Products, product]));
        lock (gate)
        {
            if (--changing == 0)
                upToDate.TrySetResult();
        }
    }

    public Task WaitForNonStaleDataAsync(CancellationToken ct)
    {
        lock (gate)
            return upToDate.Task.WaitAsync(ct);
    }

    private static TaskCompletionSource Completed()
    {
        var completed = new TaskCompletionSource();
        completed.SetResult();
        return completed;
    }
}
