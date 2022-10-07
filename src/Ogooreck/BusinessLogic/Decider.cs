#pragma warning disable CS1591

namespace Ogooreck.BusinessLogic;

public record Decider<TCommand, TEvent, TState>(
    Func<TCommand, TState, DecideResult<TEvent, TState>> Decide,
    Func<TState, TEvent, TState> Evolve,
    Func<TState> GetInitialState
);

public record DecideResult<TEvent, TState>(
    TEvent[] NewEvents,
    TState? CurrentState = default
);

public record DecideResult
{
    public static DecideResult<TEvent, TState> For<TEvent, TState>(params TEvent[] newEvents) => new(newEvents);

    public static DecideResult<TEvent, TState> For<TEvent, TState>(TState currentState) => new(Array.Empty<TEvent>(), currentState);

    public static DecideResult<TEvent, TState> For<TEvent, TState>(TState currentState, params TEvent[] newEvents) => new(newEvents, currentState);

    public static DecideResult<object, TState> For<TState>(params object[] newEvents) => new(newEvents);

    public static DecideResult<object, TState> For<TState>(TState currentState) => new(Array.Empty<object>(), currentState);
}
