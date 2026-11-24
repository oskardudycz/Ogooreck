using System.Reflection;

namespace Ogooreck.API;

/// <summary>Records the arguments of the calls an app makes to one of its services, without changing the service or the app.</summary>
/// <example>
/// <code>
/// var recorded = CallRecorder.Wrap(inner, "Send", args =&gt; sent.Add(args));
/// </code>
/// </example>
public static class CallRecorder
{
    /// <summary>
    /// Returns a proxy that forwards every call to <paramref name="inner"/> and calls <paramref name="record"/> with the arguments of each successful call of <paramref name="method"/>.
    /// For a method that returns a <see cref="Task"/>, <c>Task&lt;T&gt;</c>, <see cref="ValueTask"/> or <c>ValueTask&lt;T&gt;</c>, it records when the task completes successfully, and the caller awaits the same task once.
    /// A failing call records nothing and rethrows the original exception, not a <see cref="TargetInvocationException"/>. <typeparamref name="TService"/> must be an interface.
    /// </summary>
    /// <example>
    /// <code>
    /// services.Decorate&lt;IProducer&lt;string, string&gt;&gt;(inner =&gt;
    ///     CallRecorder.Wrap(inner, nameof(IProducer&lt;string, string&gt;.ProduceAsync), args =&gt; Published.Add(args)))
    /// </code>
    /// </example>
    public static TService Wrap<TService>(TService inner, string method, Action<object?[]> record)
        where TService : class
    {
        var proxy = DispatchProxy.Create<TService, RecordingProxy<TService>>();
        ((RecordingProxy<TService>)(object)proxy).Initialize(inner, method, record);
        return proxy;
    }

    private class RecordingProxy<TService>: DispatchProxy where TService : class
    {
        private TService inner = null!;
        private string method = null!;
        private Action<object?[]> record = null!;

        public void Initialize(TService inner, string method, Action<object?[]> record)
        {
            this.inner = inner;
            this.method = method;
            this.record = record;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var called = targetMethod!;
            var result = called.Invoke(inner, BindingFlags.DoNotWrapExceptions, null, args, null);

            if (called.Name != method)
                return result;

            var arguments = args!;
            var returned = called.ReturnType;
            var shape = returned.IsGenericType ? returned.GetGenericTypeDefinition() : returned;
            if (shape == typeof(Task))
                return AfterTask((Task)result!, arguments);
            if (shape == typeof(ValueTask))
                return AfterValueTask((ValueTask)result!, arguments);
            if (shape == typeof(Task<>))
                return Generic(nameof(AfterTaskOf), returned, result, arguments);
            if (shape == typeof(ValueTask<>))
                return Generic(nameof(AfterValueTaskOf), returned, result, arguments);

            record(arguments);
            return result;
        }

        private object? Generic(string name, Type returned, object? result, object?[] arguments) =>
            GetType().BaseType!
                .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(returned.GetGenericArguments()[0])
                .Invoke(this, [result, arguments]);

        private async Task AfterTask(Task task, object?[] arguments)
        {
            await task;
            record(arguments);
        }

        private async ValueTask AfterValueTask(ValueTask task, object?[] arguments)
        {
            await task;
            record(arguments);
        }

        private async Task<T> AfterTaskOf<T>(Task<T> task, object?[] arguments)
        {
            var value = await task;
            record(arguments);
            return value;
        }

        private async ValueTask<T> AfterValueTaskOf<T>(ValueTask<T> task, object?[] arguments)
        {
            var value = await task;
            record(arguments);
            return value;
        }
    }
}
