#pragma warning disable CS1591

namespace Ogooreck.BusinessLogic;

public record Decider<TState>(
    Func<object, TState, object[]> Decide,
    Func<TState, object, TState> Evolve,
    Func<TState> GetInitialState
): Decider<object, object, TState>(Decide, Evolve, GetInitialState);

public record Decider<TCommand, TEvent, TState>(
    Func<TCommand, TState, TEvent[]> Decide,
    Func<TState, TEvent, TState> Evolve,
    Func<TState> GetInitialState
);
