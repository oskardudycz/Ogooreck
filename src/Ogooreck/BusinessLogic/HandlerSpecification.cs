using Ogooreck.Factories;

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
    public static WhenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState>
        When<TEvent, TState>(
            this GivenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState> given,
            Func<TState, TEvent> when
        ) =>
        given.When(state => DecideResult.For<TEvent, TState>(when(state)));

    public static WhenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState>
        When<TEvent, TState>(
            this GivenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState> given,
            Func<TState, TEvent[]> when
        ) =>
        given.When(state => DecideResult.For<TEvent, TState>(when(state)));

    public static WhenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState>
        When<TEvent, TState>(
            this GivenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState> given,
            Action<TState> when
        ) =>
        given.When(state =>
        {
            when(state);
            return DecideResult.For<TEvent, TState>(state);
        });

    public static WhenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState>
        When<TEvent, TState>(
            this GivenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState> given,
            Func<TState> when
        ) =>
        given.When(_ => DecideResult.For<TEvent, TState>(when()));

    public static WhenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState>
        When<TEvent, TState>(
            this GivenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState> given,
            Func<TState, TState> when
        ) =>
        given.When(state => DecideResult.For<TEvent, TState>(when(state)));

    public static WhenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState>
        When<TEvent, TState>(
            this GivenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState> given,
            Func<TEvent[]> when
        ) =>
        given.When(_ => DecideResult.For<TEvent, TState>(when()));

    public static WhenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState>
        When<TEvent, TState>(
            this GivenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState> given,
            Func<TEvent> when
        ) =>
        given.When(_ => DecideResult.For<TEvent, TState>(when()));
}
