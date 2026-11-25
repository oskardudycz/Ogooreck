using System.Reflection;
using RabbitMQ.Client;

namespace Ogooreck.RabbitMQ;

internal class ChannelProxy: DispatchProxy
{
    private IChannel inner = null!;
    private ChannelState channel = null!;
    private HostState host = null!;

    public static IChannel Create(IChannel inner, ChannelState channel, HostState host)
    {
        var proxy = Create<IChannel, ChannelProxy>();
        var state = (ChannelProxy)(object)proxy;
        state.inner = inner;
        state.channel = channel;
        state.host = host;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod!;
        var arguments = args!;
        switch (method.Name)
        {
            case nameof(IChannel.BasicConsumeAsync):
                var queue = (string)arguments[0]!;
                var autoAck = (bool)arguments[1]!;
                arguments[6] = ConsumerProxy.Create((IAsyncBasicConsumer)arguments[6]!, channel, host, autoAck);
                return Consuming((Task<string>)Forwarding.Call(inner, method, arguments)!, queue);
            case nameof(IChannel.BasicAckAsync):
                return Settling(method, arguments, (bool)arguments[1]!, false);
            case nameof(IChannel.BasicNackAsync):
                return Settling(method, arguments, (bool)arguments[1]!, (bool)arguments[2]!);
            case nameof(IChannel.BasicRejectAsync):
                return Settling(method, arguments, false, (bool)arguments[1]!);
            default:
                return Forwarding.Call(inner, method, arguments);
        }
    }

    private async Task<string> Consuming(Task<string> consuming, string queue)
    {
        var consumerTag = await consuming;
        host.Consuming(queue);
        return consumerTag;
    }

    private async ValueTask Settling(MethodInfo method, object?[] arguments, bool multiple, bool requeue)
    {
        var settled = channel.Settle((ulong)arguments[0]!, multiple);
        var requeued = requeue ? settled : [];
        host.RequeuePending(requeued);
        try
        {
            await (ValueTask)Forwarding.Call(inner, method, arguments)!;
        }
        catch
        {
            host.RequeueFailed(requeued);
            throw;
        }
    }
}
