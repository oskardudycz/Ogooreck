#pragma warning disable CS1591

namespace Ogooreck.BusinessLogic;

public record Decider<TCommand, TEvent, TState>(
    Func<TCommand, TState, TEvent[]> Decide,
    Func<TState, TEvent, TState> Evolve,
    Func<TState> GetInitialState
);
