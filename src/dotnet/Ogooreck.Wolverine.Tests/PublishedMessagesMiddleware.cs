using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Ogooreck.Sample.Wolverine.Events;
using Wolverine;

namespace Ogooreck.Wolverine.Tests;

public static class PublishedMessagesMiddleware
{
    public static readonly ConcurrentQueue<Envelope> EveryPublishedMessage = new();

    public static void RegisterIn(IServiceCollection services) =>
        services.ConfigureWolverine(options =>
            options.Policies.AddMiddleware(typeof(PublishedMessagesMiddleware),
                chain => chain.MessageType.Namespace == typeof(OrderPlaced).Namespace));

    public static void Before(Envelope envelope) => EveryPublishedMessage.Enqueue(envelope);
}
