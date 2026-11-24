using System.Reflection;
using RabbitMQ.Client;

namespace Ogooreck.RabbitMQ;

internal class ConsumerProxy: DispatchProxy
{
    public const string ProbeHeader = "catchUpProbe";

    private IAsyncBasicConsumer inner = null!;
    private ChannelState channel = null!;
    private HostState host = null!;
    private bool autoAck;

    public static IAsyncBasicConsumer Create(IAsyncBasicConsumer inner, ChannelState channel, HostState host,
        bool autoAck)
    {
        var proxy = Create<IAsyncBasicConsumer, ConsumerProxy>();
        var consumer = (ConsumerProxy)(object)proxy;
        consumer.inner = inner;
        consumer.channel = channel;
        consumer.host = host;
        consumer.autoAck = autoAck;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod!;
        return method.Name == nameof(IAsyncBasicConsumer.HandleBasicDeliverAsync)
            ? Delivered(method, args!)
            : Forwarding.Call(inner, method, args);
    }

    private async Task Delivered(MethodInfo method, object?[] arguments)
    {
        var deliveryTag = (ulong)arguments[1]!;
        var redelivered = (bool)arguments[2]!;
        var properties = (IReadOnlyBasicProperties)arguments[5]!;
        var message = MessageKey.Of((string)arguments[4]!, (ReadOnlyMemory<byte>)arguments[6]!);

        if (HostState.HeaderOf(properties, ProbeHeader) is { } probe)
        {
            host.ProbeArrived(probe, new ProbeArrival(channel, deliveryTag));
            channel.Done(deliveryTag);
            if (!autoAck)
                await channel.Inner.BasicAckAsync(deliveryTag, false);
            return;
        }

        if (!autoAck)
            channel.Unsettled(deliveryTag, message);
        host.HandlerStarted(message, redelivered);
        try
        {
            await (Task)Forwarding.Call(inner, method, arguments)!;
        }
        finally
        {
            channel.Done(deliveryTag);
            host.HandlerDone();
        }
    }
}
