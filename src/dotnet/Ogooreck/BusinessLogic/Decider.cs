using Ogooreck.Factories;

namespace Ogooreck.BusinessLogic;

/// <summary>
/// The business logic under test, as three functions: <c>Decide</c> turns a command and the current state into new events,
/// <c>Evolve</c> applies an event to a state, and <c>GetInitialState</c> gives the state before any event.
/// You rarely build one by hand: pass your functions to <see cref="Specification"/>.<c>For</c>, which wraps them in a decider.
/// </summary>
/// <param name="Decide">Decides what happens for a command, given the current state.</param>
/// <param name="Evolve">Applies one event to a state and returns the next state.</param>
/// <param name="GetInitialState">Creates the state a scenario starts from when it gives no <c>Given</c> events.</param>
public record Decider<TCommand, TEvent, TState>(
    Func<TCommand, TState, DecideResult<TEvent, TState>> Decide,
    Func<TState, TEvent, TState> Evolve,
    Func<TState> GetInitialState
);

/// <summary>
/// Builds <see cref="Decider{TCommand, TEvent, TState}"/> instances from plain functions, so the decide function can return
/// a single event, an array of events, or a <see cref="DecideResult{TEvent, TState}"/>.
/// </summary>
public static class Decider
{
    /// <summary>
    /// Wraps a decide function that returns a <see cref="DecideResult{TEvent, TState}"/>, for when the decision also produces the next state.
    /// Without <paramref name="evolve"/> the state stays the same; without <paramref name="getInitialState"/> the initial state is the type's default,
    /// or an uninitialized instance when the type has no default constructor.
    /// </summary>
    /// <example>
    /// <code>
    /// var decider = Decider.For&lt;object, object, Basket&gt;(
    ///     (command, basket) => DecideResult.For&lt;object, Basket&gt;(basket with { Products = [.. basket.Products, "shoes"] }));
    /// </code>
    /// </example>
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

    /// <summary>
    /// Wraps a decide function that returns exactly one event, the common shape for a command that either succeeds with one fact or throws.
    /// </summary>
    /// <example>
    /// <code>
    /// var decider = Decider.For&lt;DepositMoney, MoneyDeposited, Account&gt;(
    ///     (command, account) => new MoneyDeposited(command.Amount), Account.Evolve);
    /// </code>
    /// </example>
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

    /// <summary>
    /// Wraps a decide function that returns an array of events, for a command that records several facts or none.
    /// </summary>
    /// <example>
    /// <code>
    /// var decider = Decider.For&lt;CloseCart, CartEvent, Cart&gt;(
    ///     (command, cart) => [new CartClosed(cart.Id), new ReceiptIssued(cart.Id)], Cart.Evolve);
    /// </code>
    /// </example>
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

/// <summary>
/// What a decide function returns: the new events, and optionally the new state.
/// Give the state when the code under test computes it itself, as state-based code does. Leave it out, and the specification evolves the state from the events.
/// </summary>
/// <param name="NewEvents">The events the command produced, in order.</param>
/// <param name="NewState">The state after the command, or <c>default</c> to derive it from <paramref name="NewEvents"/>.</param>
public record DecideResult<TEvent, TState>(
    TEvent[] NewEvents,
    TState? NewState = default
);

/// <summary>
/// Creates <see cref="DecideResult{TEvent, TState}"/> values, so a decide function reads as the outcome it describes.
/// </summary>
public static class DecideResult
{
    /// <summary>
    /// A result with only events. The specification evolves the state from them.
    /// </summary>
    /// <example>
    /// <code>
    /// DecideResult.For&lt;ProductAdded, Cart&gt;(new ProductAdded(cartId, "socks"))
    /// </code>
    /// </example>
    public static DecideResult<TEvent, TState> For<TEvent, TState>(params TEvent[] newEvents) => new(newEvents);

    /// <summary>
    /// A result with a new state and no events, for state-based code that changes a state without publishing anything.
    /// </summary>
    /// <example>
    /// <code>
    /// DecideResult.For&lt;object, Basket&gt;(basket with { Products = [.. basket.Products, add.Product] })
    /// </code>
    /// </example>
    public static DecideResult<TEvent, TState> For<TEvent, TState>(TState currentState) =>
        new(Array.Empty<TEvent>(), currentState);

    /// <summary>
    /// A result with both the new state and the events that led to it.
    /// </summary>
    /// <example>
    /// <code>
    /// DecideResult.For(updatedAggregate, updatedAggregate.DequeueUncommittedEvents())
    /// </code>
    /// </example>
    public static DecideResult<TEvent, TState> For<TEvent, TState>(TState currentState, params TEvent[] newEvents) =>
        new(newEvents, currentState);

    /// <summary>
    /// A result with only events, where events are untyped <c>object</c>s, as when one union of events is not defined.
    /// </summary>
    /// <example>
    /// <code>
    /// DecideResult.For&lt;Incident&gt;(new IncidentLogged(incidentId, customerId, contact, description, loggedBy, now))
    /// </code>
    /// </example>
    public static DecideResult<object, TState> For<TState>(params object[] newEvents) => new(newEvents);

    /// <summary>
    /// A result with a new state and no events, where events are untyped <c>object</c>s.
    /// </summary>
    /// <example>
    /// <code>
    /// DecideResult.For(Handle(() => now, logIncident))
    /// </code>
    /// </example>
    public static DecideResult<object, TState> For<TState>(TState currentState) =>
        new(Array.Empty<object>(), currentState);
}
