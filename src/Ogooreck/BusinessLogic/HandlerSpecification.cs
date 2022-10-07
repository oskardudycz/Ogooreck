namespace Ogooreck.BusinessLogic;
#pragma warning disable CS1591
public delegate DecideResult<TEvent, TState> Handler<TEvent, TState>(TState state);

public class HandlerSpecification<TState>
    : HandlerSpecification<object, TState>
{
    public HandlerSpecification(Decider<Handler<object, TState>, object, TState> decider):
        base(decider)
    {
    }
}

public class HandlerSpecification<TEvent, TState>
    : DeciderSpecification<Handler<TEvent, TState>, TEvent, TState>
{
    public HandlerSpecification(Decider<Handler<TEvent, TState>, TEvent, TState> decider):
        base(decider)
    {
    }
}

public static class HandlerSpecificationExtensions
{
    public static ThenDeciderSpecificationBuilder<TEvent, TState>
        When<TEvent, TState>(
            this WhenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState> when,
            params Func<TState, TEvent>[] whens
        ) =>
        when.When(whens.Select(WhenMapping<TEvent, TState>.ToHandler).ToArray());

    public static ThenDeciderSpecificationBuilder<TEvent, TState>
        When<TEvent, TState>(
            this WhenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState> when,
            params Func<TState, TEvent[]>[] whens
        ) =>
        when.When(whens.Select(WhenMapping<TEvent, TState>.ToHandler).ToArray());

    public static ThenDeciderSpecificationBuilder<TEvent, TState>
        When<TEvent, TState>(
            this WhenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState> when,
            params Action<TState>[] whens
        ) =>
        when.When(whens.Select(WhenMapping<TEvent, TState>.ToHandler).ToArray());

    public static ThenDeciderSpecificationBuilder<TEvent, TState>
        When<TEvent, TState>(
            this WhenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState> when,
            params Func<TState>[] whens
        ) =>
        when.When(whens.Select(WhenMapping<TEvent, TState>.ToHandler).ToArray());

    public static ThenDeciderSpecificationBuilder<TEvent, TState>
        When<TEvent, TState>(
            this WhenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState> when,
            params Func<TState, TState>[] whens
        ) =>
        when.When(whens.Select(WhenMapping<TEvent, TState>.ToHandler).ToArray());


    public static ThenDeciderSpecificationBuilder<TEvent, TState>
        When<TEvent, TState>(
            this WhenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState> when,
            params Func<TEvent[]>[] whens
        ) =>
        when.When(whens.Select(WhenMapping<TEvent, TState>.ToHandler).ToArray());

    public static ThenDeciderSpecificationBuilder<TEvent, TState>
        When<TEvent, TState>(
            this WhenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState> when,
            params Func<TEvent>[] whens
        ) =>
        when.When(whens.Select(WhenMapping<TEvent, TState>.ToHandler).ToArray());
}

public static class WhenMapping<TEvent, TState>
{
    public static Handler<TEvent, TState> ToHandler(Func<TState, TState> when) =>
        state => DecideResult.For<TEvent, TState>(when(state));

    public static Handler<TEvent, TState> ToHandler(Func<TState, TEvent> when) =>
        state => DecideResult.For<TEvent, TState>(when(state));

    public static Handler<TEvent, TState> ToHandler(Func<TState> when) =>
        _ => DecideResult.For<TEvent, TState>(when());

    public static Handler<TEvent, TState> ToHandler(Action<TState> when) =>
        state =>
        {
            when(state);
            return DecideResult.For<TEvent, TState>(state);
        };

    public static Handler<TEvent, TState> ToHandler(Func<TState, TEvent[]> when) =>
        state => DecideResult.For<TEvent, TState>(when(state));

    public static Handler<TEvent, TState> ToHandler(Func<TEvent[]> when) =>
        _ => DecideResult.For<TEvent, TState>(when());

    public static Handler<TEvent, TState> ToHandler(Func<TEvent> when) =>
        _ => DecideResult.For<TEvent, TState>(when());
}
