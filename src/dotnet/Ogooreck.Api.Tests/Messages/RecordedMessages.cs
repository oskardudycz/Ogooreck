using Ogooreck.API;
using Ogooreck.Testing;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests.Messages;

public record OrderPlaced(Guid CartId);

public record CartClosed(Guid CartId);

public record ShipOrder(Guid CartId);

public record Dispatched(Guid CartId);

public sealed class RecordedMessages(ApiSpecification<Program> app)
{
    public Recorded<Observed> Items { get; } = new();

    public TestStep Adding(Func<string, IEnumerable<Observed>> items) =>
        Action(ctx =>
        {
            foreach (var item in items(ctx.Traceparent))
                Items.Add(item);
            return Task.CompletedTask;
        });

    public TestStep Adding(params object[] messages) =>
        Adding(traceparent => messages.Select(message => new Observed(message, traceparent)));

    public TestStep AddingAfterTheFirstCheck(params object[] messages) =>
        Adding(_ => [], traceparent =>
        {
            foreach (var message in messages)
                Items.Add(new Observed(message, traceparent));
        });

    public TestStep Adding(Func<string, IEnumerable<Observed>> items, Action<string> afterTheFirstCheck) =>
        Action(ctx =>
        {
            var traceparent = ctx.Traceparent;
            foreach (var item in items(traceparent))
                Items.Add(item);
            Items.Add(new Observed(new FirstCheck(() => afterTheFirstCheck(traceparent)), null));
            return Task.CompletedTask;
        });

    public TestStep CancellingAtTheFirstCheck(CancellationTokenSource cancellation) =>
        Adding(_ => [], _ => cancellation.Cancel());

    public Observed Read(Observed observed)
    {
        if (observed.Message is FirstCheck check)
            check.Run();
        return observed;
    }

    public ApiSpecification<Program> Spec(bool withCatchUp = false, TimeSpan? deadline = null)
    {
        return app.With(setup =>
        {
            if (deadline is { } bound)
                setup.Clock(new PassingClock()).Deadline(bound);
            return withCatchUp
                ? setup.Recording(Items, Read, _ => Task.CompletedTask)
                : setup.Recording(Items, Read);
        });
    }

    public ApiSpecification<Program> AppendingSpec(bool withCatchUp = false) =>
        app.With(setup => withCatchUp
            ? setup.Appending(Items, Read, _ => Task.CompletedTask)
            : setup.Appending(Items, Read));

    private sealed class FirstCheck(Action action)
    {
        private int ran;

        public void Run()
        {
            if (Interlocked.Exchange(ref ran, 1) == 0)
                action();
        }
    }
}
