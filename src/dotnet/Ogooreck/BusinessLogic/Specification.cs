namespace Ogooreck.BusinessLogic;

/// <summary>
/// Starts a Given / When / Then scenario for business logic written as functions. Pick the overload by the shape of your code:
/// a decide function taking a command and the state (<c>For&lt;TState&gt;</c> with untyped commands and events, or the typed overloads),
/// or a handler specification, where the <c>When</c> step runs a function of the state instead of sending a command.
/// Each overload wraps your functions in a <see cref="Decider{TCommand, TEvent, TState}"/> and returns a specification you keep as a field of the test class.
/// </summary>
public static class Specification
{
    /// <summary>
    /// Specifies an existing decider, for when you already have one as a value.
    /// </summary>
    /// <example>
    /// <code>
    /// var spec = Specification.For(Decider.For&lt;DepositMoney, MoneyDeposited, Account&gt;((command, account) => new MoneyDeposited(command.Amount)));
    /// </code>
    /// </example>
    public static DeciderSpecification<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Decider<TCommand, TEvent, TState> decider
    ) =>
        new(decider);

    /// <summary>
    /// Specifies a typed decide function that returns a <see cref="DecideResult{TEvent, TState}"/>, for when the code computes events and state together.
    /// Give <paramref name="evolve"/> to fold events into the state, and <paramref name="getInitialState"/> to choose the state when a scenario starts with an empty <c>Given()</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// var spec = Specification.For&lt;AddToBasket, object, Basket&gt;(
    ///     (command, basket) => DecideResult.For&lt;object, Basket&gt;(basket with { Products = [.. basket.Products, command.Product] }));
    /// </code>
    /// </example>
    public static DeciderSpecification<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Func<TCommand, TState, DecideResult<TEvent, TState>> decide,
        Func<TState, TEvent, TState>? evolve = null,
        Func<TState>? getInitialState = null
    ) where TState : notnull =>
        For(Decider.For(decide, evolve, getInitialState));

    /// <summary>
    /// Specifies a typed decide function that returns an array of events, the usual shape for event-sourced code.
    /// </summary>
    /// <example>
    /// <code>
    /// var spec = Specification.For&lt;CloseCart, CartEvent, Cart&gt;(
    ///     (command, cart) => [new CartClosed(cart.Id)], Cart.Evolve);
    /// </code>
    /// </example>
    public static DeciderSpecification<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Func<TCommand, TState, TEvent[]> decide,
        Func<TState, TEvent, TState>? evolve = null,
        Func<TState>? getInitialState = null
    ) where TState : notnull =>
        For(Decider.For(decide, evolve, getInitialState));

    /// <summary>
    /// Specifies a typed decide function that returns an array of events, starting scenarios from <paramref name="initialState"/> instead of a factory function.
    /// </summary>
    /// <example>
    /// <code>
    /// var spec = Specification.For&lt;CloseCart, CartEvent, Cart&gt;(
    ///     (command, cart) => [new CartClosed(cart.Id)], Cart.Evolve, Cart.Empty);
    /// </code>
    /// </example>
    public static DeciderSpecification<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Func<TCommand, TState, TEvent[]> decide,
        Func<TState, TEvent, TState>? evolve = null,
        TState? initialState = default
    ) where TState : notnull =>
        For(Decider.For(decide, evolve, (!Equals(initialState, default) ? () => initialState : default)));


    /// <summary>
    /// Specifies a typed decide function that returns exactly one event.
    /// </summary>
    /// <example>
    /// <code>
    /// var spec = Specification.For&lt;DepositMoney, MoneyDeposited, Account&gt;(
    ///     (command, account) => new MoneyDeposited(command.Amount), Account.Evolve);
    /// </code>
    /// </example>
    public static DeciderSpecification<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Func<TCommand, TState, TEvent> decide,
        Func<TState, TEvent, TState>? evolve = null,
        Func<TState>? getInitialState = null
    ) =>
        For(Decider.For(decide, evolve, getInitialState));

    /// <summary>
    /// Specifies a typed decide function that returns exactly one event, starting scenarios from <paramref name="initialState"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// var spec = Specification.For&lt;DepositMoney, MoneyDeposited, Account&gt;(
    ///     (command, account) => new MoneyDeposited(command.Amount), Account.Evolve, Account.Empty);
    /// </code>
    /// </example>
    public static DeciderSpecification<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Func<TCommand, TState, TEvent> decide,
        Func<TState, TEvent, TState>? evolve = null,
        TState? initialState = default
    ) =>
        For(Decider.For(decide, evolve, (!Equals(initialState, default) ? () => initialState : default)));

    /// <summary>
    /// Specifies a decide function over untyped commands and events (<c>object</c>), so one function can handle a whole union of commands.
    /// Reach for it when your commands and events don't share a base type.
    /// </summary>
    /// <example>
    /// <code>
    /// private readonly DeciderSpecification&lt;BankAccount&gt; Spec =
    ///     Specification.For&lt;BankAccount&gt;(
    ///         (command, bankAccount) => BankAccountDecider.Handle(() => now, command, bankAccount),
    ///         BankAccount.Evolve);
    /// </code>
    /// </example>
    public static DeciderSpecification<TState> For<TState>(
        Func<object, TState, object> decide,
        Func<TState, object, TState>? evolve = null,
        Func<TState>? getInitialState = null
    ) =>
        new(Decider.For(decide, evolve, getInitialState));
    /// <summary>
    /// Specifies an untyped decide function that returns an array of events.
    /// </summary>
    /// <example>
    /// <code>
    /// var spec = Specification.For&lt;Cart&gt;((command, cart) => new object[] { new CartClosed(cart.Id) }, Cart.Evolve);
    /// </code>
    /// </example>
    public static DeciderSpecification<TState> For<TState>(
        Func<object, TState, object[]> decide,
        Func<TState, object, TState>? evolve = null,
        Func<TState>? getInitialState = null
    ) =>
        new(Decider.For(decide, evolve, getInitialState));

    /// <summary>
    /// Specifies an untyped decide function that returns a <see cref="DecideResult{TEvent, TState}"/>, for state-based code that returns the next state itself.
    /// </summary>
    /// <example>
    /// <code>
    /// private readonly DeciderSpecification&lt;Incident&gt; Spec = Specification.For(decide);
    /// </code>
    /// </example>
    public static DeciderSpecification<TState> For<TState>(
        Func<object, TState, DecideResult<object, TState>> decide,
        Func<TState, object, TState>? evolve = null,
        Func<TState>? getInitialState = null
    ) =>
        new(Decider.For(decide, evolve, getInitialState));

    /// <summary>
    /// Specifies code that is not driven by commands: the <c>When</c> step gives functions of the state, and these return a <see cref="DecideResult{TEvent, TState}"/>.
    /// Use it for aggregates and services where you call a method instead of dispatching a command.
    /// </summary>
    /// <example>
    /// <code>
    /// private readonly HandlerSpecification&lt;ShoppingCart&gt; Spec =
    ///     Specification.For&lt;ShoppingCart&gt;(Handle, ShoppingCart.Evolve);
    /// </code>
    /// </example>
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

    /// <summary>
    /// Specifies typed handler code where the <c>When</c> step gives functions that return events or a new state, and the state is evolved with <paramref name="evolve"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// Spec.Given(ShoppingCartOpened(shoppingCartId))
    ///     .When(cart => cart.AddProduct(priceCalculator, productItem))
    ///     .Then(new ProductItemAddedToShoppingCart(shoppingCartId, pricedProductItem));
    /// </code>
    /// </example>
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

    /// <summary>
    /// Specifies handler code over untyped events, where the <c>When</c> step runs a function of the state and the function itself returns a <see cref="DecideResult{TEvent, TState}"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// private readonly HandlerSpecification&lt;ShoppingCart&gt; Spec = Specification.For&lt;ShoppingCart&gt;(Handle, ShoppingCart.Evolve);
    /// </code>
    /// </example>
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

    /// <summary>
    /// Specifies state-based or event-sourced functions over untyped events: the <c>When</c> step runs a function of the state, and the specification evolves the state by <paramref name="evolve"/> or keeps it when none is given.
    /// </summary>
    /// <example>
    /// <code>
    /// private readonly HandlerSpecification&lt;Incident&gt; Spec = Specification.For&lt;Incident&gt;();
    /// </code>
    /// </example>
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

/// <summary>
/// The outcome of a <c>When</c> step: the state after all commands and the events they produced, in order.
/// It is what the <c>Then</c> checks compare against.
/// </summary>
/// <param name="CurrentState">The state after the last command.</param>
/// <param name="NewEvents">The events of all commands, in order.</param>
public record TestResult<TState, TEvent>(
    TState CurrentState,
    TEvent[] NewEvents
);
