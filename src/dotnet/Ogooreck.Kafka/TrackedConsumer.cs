using System.Collections.Concurrent;
using System.Reflection;
using Confluent.Kafka;

namespace Ogooreck.Kafka;

internal sealed class TrackedConsumer<TKey, TValue>(IConsumer<TKey, TValue> inner)
{
    private int inside;

    public IConsumer<TKey, TValue> Inner { get; } = inner;

    public Dictionary<TopicPartition, long> Baseline { get; } = [];

    private readonly IAdminClient admin = new DependentAdminClientBuilder(inner.Handle).Build();

    private readonly ConcurrentDictionary<Task, byte> requests = new();

    public string? MemberId { get; private set; }

    public void RememberMemberId() => MemberId = Inner.MemberId;

    public bool InsideConsume => Volatile.Read(ref inside) > 0;

    public IReadOnlyList<string> Topics =>
        [.. Inner.Subscription.Concat(Inner.Assignment.Select(partition => partition.Topic)).Distinct()];

    public void Entering()
    {
        RememberMemberId();
        Interlocked.Increment(ref inside);
    }

    public void Leaving() => Interlocked.Decrement(ref inside);

    public Task<T> Ask<T>(Func<IAdminClient, Task<T>> request)
    {
        var asked = request(admin);
        requests[asked] = 0;
        asked.ContinueWith(done => requests.TryRemove(done, out _), TaskScheduler.Default);
        return asked;
    }

    private void Dispose()
    {
        Task.WhenAll(requests.Keys).ContinueWith(_ => { }, TaskScheduler.Default).Wait();
        admin.Dispose();
    }

    public static IConsumer<TKey, TValue> Decorate(IConsumer<TKey, TValue> consumer,
        ConcurrentDictionary<TrackedConsumer<TKey, TValue>, byte> everyConsumer,
        ConcurrentQueue<TrackedConsumer<TKey, TValue>> ownConsumers)
    {
        var tracked = new TrackedConsumer<TKey, TValue>(consumer);
        everyConsumer[tracked] = 0;
        ownConsumers.Enqueue(tracked);
        var proxy = DispatchProxy.Create<IConsumer<TKey, TValue>, TrackingProxy>();
        ((TrackingProxy)(object)proxy).Initialize(tracked, everyConsumer);
        return proxy;
    }

    private class TrackingProxy: DispatchProxy
    {
        private TrackedConsumer<TKey, TValue> tracked = null!;
        private ConcurrentDictionary<TrackedConsumer<TKey, TValue>, byte> everyConsumer = null!;

        public void Initialize(TrackedConsumer<TKey, TValue> tracked,
            ConcurrentDictionary<TrackedConsumer<TKey, TValue>, byte> everyConsumer)
        {
            this.tracked = tracked;
            this.everyConsumer = everyConsumer;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var called = targetMethod!;
            if (called.Name == nameof(IConsumer<TKey, TValue>.Consume))
            {
                tracked.Entering();
                try
                {
                    return called.Invoke(tracked.Inner, BindingFlags.DoNotWrapExceptions, null, args, null);
                }
                finally
                {
                    tracked.Leaving();
                }
            }

            if (called.Name == nameof(IDisposable.Dispose))
            {
                everyConsumer.TryRemove(tracked, out _);
                tracked.Dispose();
            }
            return called.Invoke(tracked.Inner, BindingFlags.DoNotWrapExceptions, null, args, null);
        }
    }
}
