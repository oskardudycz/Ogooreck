using System.Reflection;

namespace Ogooreck.RabbitMQ;

internal static class Forwarding
{
    public static object? Call(object inner, MethodInfo method, object?[]? arguments) =>
        method.Invoke(inner, BindingFlags.DoNotWrapExceptions, null, arguments, null);
}
