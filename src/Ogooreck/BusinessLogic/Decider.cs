using Ogooreck.Factories;

#pragma warning disable CS1591

namespace Ogooreck.BusinessLogic;

public record Decider<TCommand, TEvent, TState>(
    Func<TCommand, TState, DecideResult<TEvent, TState>> Decide,
    Func<TState, TEvent, TState> Evolve,
    Func<TState> GetInitialState
);

public static class Decider
{
    public static Decider<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Func<TCommand, TState, DecideResult<TEvent, TState>> decide,
        Func<TState, TEvent, TState>? evolve = null,
        Func<TState>? getInitialState = null
    ) =>
        new(
            decide,
            (state, @event) => evolve != null ? evolve(state, @event) : state,
            getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
        );

    public static Decider<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Func<TCommand, TState, TEvent> decide,
        Func<TState, TEvent, TState>? evolve = null,
        Func<TState>? getInitialState = null
    ) =>
        For<TCommand, TEvent, TState>(
            (command, currentState) => new[] { decide(command, currentState) },
            evolve,
            getInitialState
        );

    public static Decider<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Func<TCommand, TState, TEvent[]> decide,
        Func<TState, TEvent, TState>? evolve = null,
        Func<TState>? getInitialState = null
    ) =>
        For<TCommand, TEvent, TState>(
            (command, currentState) => DecideResult.For<TEvent, TState>(decide(command, currentState)),
            evolve,
            getInitialState
        );
}

public record DecideResult<TEvent, TState>(
    TEvent[] NewEvents,
    TState? NewState = default
);

public static class DecideResult
{
    public static DecideResult<TEvent, TState> For<TEvent, TState>(params TEvent[] newEvents) => new(newEvents);

    public static DecideResult<TEvent, TState> For<TEvent, TState>(TState currentState) =>
        new(Array.Empty<TEvent>(), currentState);

    public static DecideResult<TEvent, TState> For<TEvent, TState>(TState currentState, params TEvent[] newEvents) =>
        new(newEvents, currentState);

    public static DecideResult<object, TState> For<TState>(params object[] newEvents) => new(newEvents);

    public static DecideResult<object, TState> For<TState>(TState currentState) =>
        new(Array.Empty<object>(), currentState);
}
