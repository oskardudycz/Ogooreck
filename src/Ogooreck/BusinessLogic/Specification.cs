using Ogooreck.Factories;

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
        Func<TState, TEvent, TState> evolve,
        Func<TState>? getInitialState = null
    ) where TState : notnull =>
        For(
            new Decider<TCommand, TEvent, TState>(
                decide,
                evolve,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static DeciderSpecification<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Func<TCommand, TState, TEvent[]> decide,
        Func<TState, TEvent, TState> evolve,
        Func<TState>? getInitialState = null
    ) where TState : notnull =>
        For(
            new Decider<TCommand, TEvent, TState>(
                (command, currentState) =>
                {
                    var newEvents = decide(command, currentState);

                    return new DecideResult<TEvent, TState>(newEvents);
                },
                evolve,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static DeciderSpecification<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Func<TCommand, TState, TEvent> decide,
        Func<TState, TEvent, TState> evolve,
        Func<TState>? getInitialState
    ) =>
        For(
            new Decider<TCommand, TEvent, TState>(
                (command, currentState) => DecideResult.For<TEvent, TState>(decide(command, currentState)),
                evolve,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static DeciderSpecification<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Func<TCommand, TState, TEvent[]> decide,
        Func<TState>? getInitialState = null
    ) where TState : notnull =>
        For(
            new Decider<TCommand, TEvent, TState>(
                (command, currentState) =>
                {
                    var newEvents = decide(command, currentState);

                    return new DecideResult<TEvent, TState>(newEvents);
                },
                (state, _) => state,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static DeciderSpecification<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Func<TCommand, TState, TEvent> decide,
        Func<TState>? getInitialState
    ) =>
        For(
            new Decider<TCommand, TEvent, TState>(
                (command, currentState) => DecideResult.For<TEvent, TState>(decide(command, currentState)),
                (state, _) => state,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static DeciderSpecification<TState> For<TState>(
        Func<object, TState, object> decide,
        Func<TState, object, TState> evolve,
        Func<TState>? getInitialState = null
    ) =>
        new(
            new Decider<object, object, TState>(
                (command, currentState) => DecideResult.For<TState>(decide(command, currentState)),
                evolve,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static DeciderSpecification<TState> For<TState>(
        Func<object, TState, object> decide,
        Func<TState>? getInitialState = null
    ) =>
        new(
            new Decider<object, object, TState>(
                (command, currentState) => DecideResult.For<TState>(decide(command, currentState)),
                (state, _) => state,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static DeciderSpecification<TState> For<TState>(
        Func<object, TState, DecideResult<object, TState>> decide,
        Func<TState, object, TState> evolve,
        Func<TState>? getInitialState = null
    ) =>
        new(
            new Decider<object, object, TState>(
                (command, currentState) => decide(command, currentState),
                evolve,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static DeciderSpecification<TState> For<TState>(
        Func<object, TState, DecideResult<object, TState>> decide,
        Func<TState>? getInitialState = null
    ) =>
        new(
            new Decider<object, object, TState>(
                (command, currentState) => decide(command, currentState),
                (state, _) => state,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static DeciderSpecification<TState> For<TState>(
        Func<object, TState, object[]> decide,
        Func<TState, object, TState> evolve,
        Func<TState>? getInitialState = null
    ) =>
        new(
            new Decider<object, object, TState>(
                (command, currentState) => DecideResult.For<TState>(decide(command, currentState)),
                evolve,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static HandlerSpecification<TState> For<TState>(
        Func<Handler<object, TState>, TState, DecideResult<object, TState>> decide,
        Func<TState, object, TState> evolve,
        Func<TState>? getInitialState = null
    ) =>
        new(
            new Decider<Handler<object, TState>, object, TState>(
                decide,
                evolve,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static HandlerSpecification<TState> For<TState>(
        Func<TState, object, TState> evolve,
        Func<TState>? getInitialState = null
    ) =>
        new(
            new Decider<Handler<object, TState>, object, TState>(
                (handler, currentState) => handler(currentState),
                evolve,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );


    public static HandlerSpecification<TState> For<TState>(
        Func<TState>? getInitialState = null
    ) =>
        new(
            new Decider<Handler<object, TState>, object, TState>(
                (handler, currentState) => handler(currentState),
                (state, _) => state,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );
}

internal record TestResult<TState, TEvent>(
    TState CurrentState,
    TEvent[] NewEvents
);
