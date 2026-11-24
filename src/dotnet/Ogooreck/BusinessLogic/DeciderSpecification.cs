using Ogooreck.Assertions;


namespace Ogooreck.BusinessLogic;

/// <summary>
/// A Given / When / Then specification for a decider whose commands and events are untyped <c>object</c>s.
/// Create it with <c>Specification.For&lt;TState&gt;(...)</c> and keep it as a field of the test class.
/// </summary>
public class DeciderSpecification<TState>
    : DeciderSpecification<object, object, TState>
{
    /// <summary>
    /// Wraps a decider with untyped commands and events. Prefer <c>Specification.For&lt;TState&gt;</c>, which builds the decider from your functions.
    /// </summary>
    public DeciderSpecification(Decider<object, object, TState> decider) : base(decider) { }
}

/// <summary>
/// A Given / When / Then specification for a decider: given past events or a state, when commands run, then the new events, the state or an exception are what you expect.
/// Create it with <c>Specification.For</c> and keep it as a field of the test class.
/// </summary>
/// <example>
/// <code>
/// Spec.Given(BankAccountOpened(bankAccountId, now, 1))
///     .When(new RecordDeposit(amount, cashierId))
///     .Then(new DepositRecorded(bankAccountId, amount, cashierId, now, 2));
/// </code>
/// </example>
public class DeciderSpecification<TCommand, TEvent, TState>
{
    private readonly Decider<TCommand, TEvent, TState> decider;

    /// <summary>
    /// Wraps a decider. Prefer <c>Specification.For</c>, which builds the decider from your functions.
    /// </summary>
    public DeciderSpecification(Decider<TCommand, TEvent, TState> decider) =>
        this.decider = decider;

    /// <summary>
    /// Starts the scenario from the past events: they are applied to the initial state with the decider's <c>Evolve</c> function.
    /// </summary>
    /// <example>
    /// <code>
    /// Spec.Given(
    ///         BankAccountOpened(bankAccountId, now, 1),
    ///         BankAccountClosed(bankAccountId, now, 2))
    ///     .When(new RecordDeposit(amount, cashierId))
    ///     .ThenThrows&lt;InvalidOperationException&gt;();
    /// </code>
    /// </example>
    public WhenDeciderSpecificationBuilder<TCommand, TEvent, TState> Given(params TEvent[] events) =>
        Given(() =>
        {
            var currentState = decider.GetInitialState();

            return events.Aggregate(currentState, decider.Evolve);
        });

    /// <summary>
    /// Starts the scenario from a ready state, for state-based code that has no events to replay.
    /// </summary>
    /// <example>
    /// <code>
    /// Spec.Given(new Basket(["socks"]))
    ///     .When(new AddToBasket("shoes"))
    ///     .Then(new Basket(["shoes", "socks"]));
    /// </code>
    /// </example>
    public WhenDeciderSpecificationBuilder<TCommand, TEvent, TState> Given(TState currentState) =>
        Given(() => currentState);

    /// <summary>
    /// Starts the scenario from the initial state, with no past events: the first command of a new stream.
    /// </summary>
    /// <example>
    /// <code>
    /// Spec.Given()
    ///     .When(new OpenBankAccount(bankAccountId, accountNumber, clientId, currencyISOCode))
    ///     .Then(new BankAccountOpened(bankAccountId, accountNumber, clientId, currencyISOCode, now, 1));
    /// </code>
    /// </example>
    public WhenDeciderSpecificationBuilder<TCommand, TEvent, TState> Given() =>
        new(decider);

    /// <summary>
    /// Starts the scenario from a state created when the <c>When</c> step runs, so the state is built after any setup the test does first.
    /// </summary>
    /// <example>
    /// <code>
    /// Spec.Given(() => LoggedIncident(incidentId, now))
    ///     .When(incident => Handle(() => now, incident, new CategoriseIncident(incidentId, category, categorisedBy)))
    ///     .Then(loggedIncident with { Category = category });
    /// </code>
    /// </example>
    public WhenDeciderSpecificationBuilder<TCommand, TEvent, TState> Given(Func<TState> getCurrentState) =>
        new(decider, getCurrentState);
}

/// <summary>
/// The step after <c>Given</c>: it takes the commands to run against the given state. Get it from <c>DeciderSpecification.Given</c>.
/// </summary>
public class WhenDeciderSpecificationBuilder<TCommand, TEvent, TState>
{
    private readonly Decider<TCommand, TEvent, TState> decider;
    private readonly Func<TState>? getCurrentState;

    /// <summary>
    /// Creates the step. Call <c>Given</c> on a specification instead of creating it by hand.
    /// </summary>
    public WhenDeciderSpecificationBuilder(
        Decider<TCommand, TEvent, TState> decider,
        Func<TState>? getCurrentState = null
    )
    {
        this.decider = decider;
        this.getCurrentState = getCurrentState;
    }

    /// <summary>
    /// Runs the commands in order, each against the state the previous one left, and collects their events.
    /// Nothing runs until a <c>Then</c> check reads the result, so a decide function that throws is checked by <c>ThenThrows</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// Spec.Given(BankAccountOpened(bankAccountId, now, 1))
    ///     .When(new RecordDeposit(amount, cashierId), new RecordDeposit(amount, cashierId))
    ///     .Then(
    ///         new DepositRecorded(bankAccountId, amount, cashierId, now, 2),
    ///         new DepositRecorded(bankAccountId, amount, cashierId, now, 3));
    /// </code>
    /// </example>
    public ThenDeciderSpecificationBuilder<TEvent, TState> When(params TCommand[] commands) =>
        new(RunTest(commands));

    private Lazy<TestResult<TState, TEvent>> RunTest(TCommand[] commands) =>
        new(() =>
        {
            var currentState = (getCurrentState ?? decider.GetInitialState)();
            var resultEvents = new List<TEvent>();

            foreach (var command in commands)
            {
                var (newEvents, state) = decider.Decide(command, currentState);
                resultEvents.AddRange(newEvents);

                currentState = state ?? newEvents.Aggregate(currentState, decider.Evolve);
            }

            return new TestResult<TState, TEvent>(currentState, resultEvents.ToArray());
        });
}

/// <summary>
/// The step after <c>When</c>: the checks of what the commands produced. Every <c>Then</c> returns it, so checks chain.
/// </summary>
public class ThenDeciderSpecificationBuilder<TEvent, TState>
{
    private readonly Lazy<TestResult<TState, TEvent>> getResult;

    /// <summary>
    /// Creates the step. Call <c>When</c> instead of creating it by hand.
    /// </summary>
    public ThenDeciderSpecificationBuilder(Lazy<TestResult<TState, TEvent>> getResult) =>
        this.getResult = getResult;

    /// <summary>
    /// Checks that the commands produced exactly these events, in this order. Events are compared by value, including nested objects.
    /// </summary>
    /// <example>
    /// <code>
    /// .Then(new IncidentLogged(incidentId, customerId, contact, description, loggedBy, now));
    /// </code>
    /// </example>
    public ThenDeciderSpecificationBuilder<TEvent, TState> Then(params TEvent[] expectedEvents)
    {
        var result = getResult.Value;
        Expect.OrderedEquivalent(result.NewEvents, expectedEvents, "Event");
        return this;
    }

    /// <summary>
    /// Checks that the state after the commands equals <paramref name="expectedState"/>. Nested collections are compared without order, so only their contents matter.
    /// </summary>
    /// <example>
    /// <code>
    /// .Then(new Basket(["shoes", "socks"]));
    /// </code>
    /// </example>
    public ThenDeciderSpecificationBuilder<TEvent, TState> Then(TState expectedState)
    {
        var result = getResult.Value;
        Expect.Equivalent(result.CurrentState, expectedState, "Expected state does not match the result");
        return this;
    }

    /// <summary>
    /// Runs your own checks on the produced events, for when comparing whole events is too strict.
    /// </summary>
    /// <example>
    /// <code>
    /// .Then(events => Assert.Single(events));
    /// </code>
    /// </example>
    public ThenDeciderSpecificationBuilder<TEvent, TState> Then(params Action<TEvent[]>[] eventsAssertions)
    {
        var result = getResult.Value;

        foreach (var then in eventsAssertions)
        {
            then(result.NewEvents);
        }

        return this;
    }

    /// <summary>
    /// Runs your own checks on the state after the commands.
    /// </summary>
    /// <example>
    /// <code>
    /// .Then(state => Assert.Equal(ShoppingCartStatus.Confirmed, state.Status));
    /// </code>
    /// </example>
    public ThenDeciderSpecificationBuilder<TEvent, TState> Then(params Action<TState>[] stateAssertions)
    {
        var result = getResult.Value;

        foreach (var then in stateAssertions)
        {
            then(result.CurrentState);
        }

        return this;
    }

    /// <summary>
    /// Runs your own checks on the state and the produced events together.
    /// </summary>
    /// <example>
    /// <code>
    /// .Then((state, _) => Assert.Equal(ShoppingCartStatus.Pending, state.Status));
    /// </code>
    /// </example>
    public ThenDeciderSpecificationBuilder<TEvent, TState> Then(params Action<TState, TEvent[]>[] assertions)
    {
        var result = getResult.Value;

        foreach (var then in assertions)
        {
            then(result.CurrentState, result.NewEvents);
        }

        return this;
    }

    /// <summary>
    /// Checks that running the commands throws <typeparamref name="TException"/>, and optionally checks the exception with <paramref name="assert"/>.
    /// It fails when nothing is thrown. Other exception types propagate, so the test fails with the real cause.
    /// </summary>
    /// <example>
    /// <code>
    /// .ThenThrows&lt;InvalidOperationException&gt;(exception => Assert.Equal("Account is closed!", exception.Message));
    /// </code>
    /// </example>
    public ThenDeciderSpecificationBuilder<TEvent, TState> ThenThrows<TException>(Action<TException>? assert = null)
        where TException : Exception
    {
        try
        {
            var _ = getResult.Value;
        }
        catch (TException e)
        {
            assert?.Invoke(e);
            return this;
        }

        throw new AssertionException($"Expected {typeof(TException).Name}, but no exception was thrown.");
    }
}
