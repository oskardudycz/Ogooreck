using Ogooreck.Factories;

namespace Ogooreck.BusinessLogic;
#pragma warning disable CS1591

public delegate DecideResult<TEvent, TState> Handler<TState, TEvent>(TState state);

public class HandlerSpecification<TState>
    : HandlerSpecification<object, TState>
{
    public HandlerSpecification(Decider<Func<TState, DecideResult<object, TState>>, object, TState> decider):
        base(decider)
    {
    }
}

public class HandlerSpecification<TEvent, TState>
    : DeciderSpecification<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState>
{
    public HandlerSpecification(Decider<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState> decider):
        base(decider)
    {
    }
}

public static class HandlerSpecificationExtensions
{
    public static HandlerSpecification<TState> For<TState>(
        Func<TState, object, TState> evolve,
        Func<TState>? getInitialState = null
    ) =>
        new(
            new Decider<Func<TState, DecideResult<object, TState>>, object, TState>(
                (handler, currentState) => handler(currentState),
                evolve,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static HandlerSpecification<TState> For<TState>(
        Func<TState>? getInitialState = null
    ) =>
        new(
            new Decider<Func<TState, DecideResult<object, TState>>, object, TState>(
                (handler, currentState) => handler(currentState),
                (state, _) => state,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );
    public static WhenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState>
        When<TEvent, TState>(
            this GivenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState> given,
            Func<TState, TEvent> when
        ) =>
        given.When(state => DecideResult<TEvent, TState>.For(when(state)));

    public static WhenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState>
        When<TEvent, TState>(
            this GivenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState> given,
            Func<TState, TEvent[]> when
        ) =>
        given.When(state => DecideResult<TEvent, TState>.For(when(state)));

    public static WhenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState>
        When<TEvent, TState>(
            this GivenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState> given,
            Action<TState> when
        ) =>
        given.When(state =>
        {
            when(state);
            return DecideResult<TEvent, TState>.For(state);
        });

    public static WhenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState>
        When<TEvent, TState>(
            this GivenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState> given,
            Func<TState> when
        ) =>
        given.When(_ => DecideResult<TEvent, TState>.For(when()));

    public static WhenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState>
        When<TEvent, TState>(
            this GivenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState> given,
            Func<TEvent[]> when
        ) =>
        given.When(_ => DecideResult<TEvent, TState>.For(when()));

    public static WhenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState>
        When<TEvent, TState>(
            this GivenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState> given,
            Func<TEvent> when
        ) =>
        given.When(_ => DecideResult<TEvent, TState>.For(when()));
}
