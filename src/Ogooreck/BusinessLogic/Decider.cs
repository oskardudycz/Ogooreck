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
)
{
    public static DecideResult<TEvent, TState> For(params TEvent[] newEvents) => new(newEvents);

    public static DecideResult<TEvent, TState> For(TState currentState) => new(Array.Empty<TEvent>(), currentState);
}
