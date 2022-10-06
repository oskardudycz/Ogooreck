namespace Ogooreck.EventSourcing;

/// <summary>
/// Decider definition used to run Event-Sourced tests
/// See more in:
/// - https://event-driven.io/en/how_to_effectively_compose_your_business_logic/
/// - https://thinkbeforecoding.com/post/2021/12/17/functional-event-sourcing-decider
/// </summary>
/// <param name="Decide">Runs business logic on the current state returning newly observed facts</param>
/// <param name="Evolve">Returns new state from the current one and an event</param>
/// <param name="GetInitialState">Returns the initial (empty state of the entity)</param>
/// <typeparam name="TState"></typeparam>
public record Decider<TState>(
    Func<object, TState, object[]> Decide,
    Func<TState, object, TState> Evolve,
    Func<TState> GetInitialState
);
