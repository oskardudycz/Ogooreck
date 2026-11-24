using System.Reflection;
using RabbitMQ.Client;

namespace Ogooreck.RabbitMQ;

internal class ConnectionProxy: DispatchProxy
{
    private IConnection inner = null!;
    private HostState host = null!;

    public static IConnection Create(IConnection inner, HostState host)
    {
        var proxy = Create<IConnection, ConnectionProxy>();
        var connection = (ConnectionProxy)(object)proxy;
        connection.inner = inner;
        connection.host = host;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod!;
        var result = Forwarding.Call(inner, method, args);
        return method.Name == nameof(IConnection.CreateChannelAsync) ? Decorated((Task<IChannel>)result!) : result;
    }

    private async Task<IChannel> Decorated(Task<IChannel> created) => host.Decorate(await created);
}
