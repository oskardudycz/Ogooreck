namespace Ogooreck.BusinessLogic;
#pragma warning disable CS1591

public static class Specification
{
    public static DeciderSpecification<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Decider<TCommand, TEvent, TState> decider
    ) =>
        new(decider);

    public static DeciderSpecification<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Func<TCommand, TState, DecideResult<TEvent, TState>> decide,
        Func<TState, TEvent, TState>? evolve = null,
        Func<TState>? getInitialState = null
    ) where TState : notnull =>
        For(Decider.For(decide, evolve, getInitialState));

    public static DeciderSpecification<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Func<TCommand, TState, TEvent[]> decide,
        Func<TState, TEvent, TState>? evolve = null,
        Func<TState>? getInitialState = null
    ) where TState : notnull =>
        For(Decider.For(decide, evolve, getInitialState));

    public static DeciderSpecification<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Func<TCommand, TState, TEvent> decide,
        Func<TState, TEvent, TState>? evolve = null,
        Func<TState>? getInitialState = null
    ) =>
        For(Decider.For(decide, evolve, getInitialState));

    public static DeciderSpecification<TState> For<TState>(
        Func<object, TState, object> decide,
        Func<TState, object, TState>? evolve = null,
        Func<TState>? getInitialState = null
    ) =>
        new(Decider.For(decide, evolve, getInitialState));

    public static DeciderSpecification<TState> For<TState>(
        Func<object, TState, object[]> decide,
        Func<TState, object, TState>? evolve = null,
        Func<TState>? getInitialState = null
    ) =>
        new(Decider.For(decide, evolve, getInitialState));

    public static DeciderSpecification<TState> For<TState>(
        Func<object, TState, DecideResult<object, TState>> decide,
        Func<TState, object, TState>? evolve = null,
        Func<TState>? getInitialState = null
    ) =>
        new(Decider.For(decide, evolve, getInitialState));

    public static HandlerSpecification<TEvent, TState> For<TEvent, TState>(
        Func<Handler<TEvent, TState>, TState, DecideResult<TEvent, TState>> decide,
        Func<TState, TEvent, TState> evolve,
        Func<TState>? getInitialState = null
    ) =>
        new(
            Decider.For(
                decide,
                evolve,
                getInitialState
            )
        );

    public static HandlerSpecification<TEvent, TState> For<TEvent, TState>(
        Func<TState, TEvent, TState>? evolve = null,
        Func<TState>? getInitialState = null
    ) =>
        new(
            Decider.For<Handler<TEvent, TState>, TEvent, TState>(
                decide: (handler, currentState) => handler(currentState),
                evolve,
                getInitialState
            )
        );

    public static HandlerSpecification<TState> For<TState>(
        Func<Handler<object, TState>, TState, DecideResult<object, TState>> decide,
        Func<TState, object, TState> evolve,
        Func<TState>? getInitialState = null
    ) =>
        new(
            Decider.For(
                decide,
                evolve,
                getInitialState
            )
        );

    public static HandlerSpecification<TState> For<TState>(
        Func<TState, object, TState>? evolve = null,
        Func<TState>? getInitialState = null
    ) =>
        new(
            Decider.For<Handler<object, TState>, object, TState>(
                decide: (handler, currentState) => handler(currentState),
                evolve,
                getInitialState
            )
        );
}

public record TestResult<TState, TEvent>(
    TState CurrentState,
    TEvent[] NewEvents
);
