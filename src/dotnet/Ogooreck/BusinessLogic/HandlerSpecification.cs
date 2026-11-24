namespace Ogooreck.BusinessLogic;
/// <summary>
/// A function of the state that returns what happened: the shape of the <c>When</c> step of a <see cref="HandlerSpecification{TEvent, TState}"/>.
/// The overloads of <c>When</c> convert plain functions into it, so you rarely write one.
/// </summary>
public delegate DecideResult<TEvent, TState> Handler<TEvent, TState>(TState state);

/// <summary>
/// A specification for code you call as a function of the state, such as an aggregate method or a service, with untyped events.
/// Create it with <c>Specification.For&lt;TState&gt;</c>.
/// </summary>
/// <example>
/// <code>
/// private readonly HandlerSpecification&lt;Incident&gt; Spec = Specification.For&lt;Incident&gt;();
/// </code>
/// </example>
public class HandlerSpecification<TState>
    : HandlerSpecification<object, TState>
{
    /// <summary>
    /// Wraps a decider over handlers. Prefer <c>Specification.For&lt;TState&gt;</c>.
    /// </summary>
    public HandlerSpecification(Decider<Handler<object, TState>, object, TState> decider) :
        base(decider)
    {
    }
}

/// <summary>
/// A specification for code you call as a function of the state, with typed events.
/// Its <c>When</c> takes functions of the state instead of commands. Create it with <c>Specification.For&lt;TEvent, TState&gt;</c>.
/// </summary>
public class HandlerSpecification<TEvent, TState>
    : DeciderSpecification<Handler<TEvent, TState>, TEvent, TState>
{
    /// <summary>
    /// Wraps a decider over handlers. Prefer <c>Specification.For&lt;TEvent, TState&gt;</c>.
    /// </summary>
    public HandlerSpecification(Decider<Handler<TEvent, TState>, TEvent, TState> decider) :
        base(decider)
    {
    }
}

/// <summary>
/// The <c>When</c> overloads of a <see cref="HandlerSpecification{TEvent, TState}"/>: each takes the function you call and turns it into a handler,
/// so the test says what is called, not how its result becomes events.
/// </summary>
public static class HandlerSpecificationExtensions
{
    /// <summary>
    /// Runs functions that take the state and return a new state, with no events.
    /// </summary>
    /// <example>
    /// <code>
    /// .When(incident => Handle(() => now, incident, new CategoriseIncident(incidentId, category, categorisedBy)))
    /// </code>
    /// </example>
    public static ThenDeciderSpecificationBuilder<TEvent, TState>
        When<TEvent, TState>(
            this WhenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState> when,
            params Func<TState, TEvent>[] whens
        ) =>
        when.When(whens.Select(WhenMapping<TEvent, TState>.ToHandler).ToArray());

    /// <summary>
    /// Runs functions that take the state and return the events they produced.
    /// </summary>
    /// <example>
    /// <code>
    /// .When(cart => cart.AddProduct(priceCalculator, productItem))
    /// </code>
    /// </example>
    public static ThenDeciderSpecificationBuilder<TEvent, TState>
        When<TEvent, TState>(
            this WhenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState> when,
            params Func<TState, TEvent[]>[] whens
        ) =>
        when.When(whens.Select(WhenMapping<TEvent, TState>.ToHandler).ToArray());

    /// <summary>
    /// Runs actions that take the state and return nothing, for code that mutates the state in place.
    /// </summary>
    /// <example>
    /// <code>
    /// .When(cart => cart.Confirm())
    /// </code>
    /// </example>
    public static ThenDeciderSpecificationBuilder<TEvent, TState>
        When<TEvent, TState>(
            this WhenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState> when,
            params Action<TState>[] whens
        ) =>
        when.When(whens.Select(WhenMapping<TEvent, TState>.ToHandler).ToArray());

    /// <summary>
    /// Runs functions that need no state and return a new state, such as one that creates it.
    /// </summary>
    /// <example>
    /// <code>
    /// .When(() => Handle(() => now, new LogIncident(incidentId, customerId, contact, description, loggedBy)))
    /// </code>
    /// </example>
    public static ThenDeciderSpecificationBuilder<TEvent, TState>
        When<TEvent, TState>(
            this WhenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState> when,
            params Func<TState>[] whens
        ) =>
        when.When(whens.Select(WhenMapping<TEvent, TState>.ToHandler).ToArray());

    /// <summary>
    /// Runs functions that take the state and return the state they produced from it.
    /// </summary>
    /// <example>
    /// <code>
    /// .When(incident => incident with { Category = category })
    /// </code>
    /// </example>
    public static ThenDeciderSpecificationBuilder<TEvent, TState>
        When<TEvent, TState>(
            this WhenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState> when,
            params Func<TState, TState>[] whens
        ) =>
        when.When(whens.Select(WhenMapping<TEvent, TState>.ToHandler).ToArray());


    /// <summary>
    /// Runs functions that need no state and return an array of events, such as one that starts a new stream.
    /// </summary>
    /// <example>
    /// <code>
    /// .When(() => [new ShoppingCartOpened(shoppingCartId, clientId)])
    /// </code>
    /// </example>
    public static ThenDeciderSpecificationBuilder<TEvent, TState>
        When<TEvent, TState>(
            this WhenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState> when,
            params Func<TEvent[]>[] whens
        ) =>
        when.When(whens.Select(WhenMapping<TEvent, TState>.ToHandler).ToArray());

    /// <summary>
    /// Runs functions that need no state and return one event.
    /// </summary>
    /// <example>
    /// <code>
    /// .When(() => new ShoppingCartOpened(shoppingCartId, clientId))
    /// </code>
    /// </example>
    public static ThenDeciderSpecificationBuilder<TEvent, TState>
        When<TEvent, TState>(
            this WhenDeciderSpecificationBuilder<Handler<TEvent, TState>, TEvent, TState> when,
            params Func<TEvent>[] whens
        ) =>
        when.When(whens.Select(WhenMapping<TEvent, TState>.ToHandler).ToArray());
}

/// <summary>
/// Converts the functions you pass to <c>When</c> into handlers. The <c>When</c> extensions call it for you, so reach for it only to build a handler outside a specification.
/// </summary>
public static class WhenMapping<TEvent, TState>
{
    /// <summary>Converts a function of the state that returns a new state into a handler.</summary>
    public static Handler<TEvent, TState> ToHandler(Func<TState, TState> when) =>
        state => DecideResult.For<TEvent, TState>(when(state));

    /// <summary>Converts a function of the state that returns one event into a handler.</summary>
    public static Handler<TEvent, TState> ToHandler(Func<TState, TEvent> when) =>
        state => DecideResult.For<TEvent, TState>(when(state));

    /// <summary>Converts a function without arguments that returns a new state into a handler.</summary>
    public static Handler<TEvent, TState> ToHandler(Func<TState> when) =>
        _ => DecideResult.For<TEvent, TState>(when());

    /// <summary>Converts an action on the state into a handler that keeps the state as the action left it.</summary>
    public static Handler<TEvent, TState> ToHandler(Action<TState> when) =>
        state =>
        {
            when(state);
            return DecideResult.For<TEvent, TState>(state);
        };

    /// <summary>Converts a function of the state that returns events into a handler.</summary>
    public static Handler<TEvent, TState> ToHandler(Func<TState, TEvent[]> when) =>
        state => DecideResult.For<TEvent, TState>(when(state));

    /// <summary>Converts a function without arguments that returns events into a handler.</summary>
    public static Handler<TEvent, TState> ToHandler(Func<TEvent[]> when) =>
        _ => DecideResult.For<TEvent, TState>(when());

    /// <summary>Converts a function without arguments that returns one event into a handler.</summary>
    public static Handler<TEvent, TState> ToHandler(Func<TEvent> when) =>
        _ => DecideResult.For<TEvent, TState>(when());
}
