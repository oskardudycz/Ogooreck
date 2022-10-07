using FluentAssertions;

#pragma warning disable CS1591

namespace Ogooreck.BusinessLogic;

public class DeciderSpecification<TState>
    : DeciderSpecification<object, object, TState>
{
    public DeciderSpecification(Decider<object, object, TState> decider): base(decider) { }
}

public class DeciderSpecification<TCommand, TEvent, TState>
{
    private readonly Decider<TCommand, TEvent, TState> decider;

    public DeciderSpecification(Decider<TCommand, TEvent, TState> decider) =>
        this.decider = decider;

    public WhenDeciderSpecificationBuilder<TCommand, TEvent, TState> Given(params TEvent[] events) =>
        Given(() =>
        {
            var currentState = decider.GetInitialState();

            return events.Aggregate(currentState, decider.Evolve);
        });

    public WhenDeciderSpecificationBuilder<TCommand, TEvent, TState> Given(TState currentState) =>
        Given(() => currentState);

    public WhenDeciderSpecificationBuilder<TCommand, TEvent, TState> Given() =>
        new(decider);

    public WhenDeciderSpecificationBuilder<TCommand, TEvent, TState> Given(Func<TState> getCurrentState) =>
        new(decider, getCurrentState);
}

public class WhenDeciderSpecificationBuilder<TCommand, TEvent, TState>
{
    private readonly Decider<TCommand, TEvent, TState> decider;
    private readonly Func<TState>? getCurrentState;

    public WhenDeciderSpecificationBuilder(
        Decider<TCommand, TEvent, TState> decider,
        Func<TState>? getCurrentState = null
    )
    {
        this.decider = decider;
        this.getCurrentState = getCurrentState;
    }

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

public class ThenDeciderSpecificationBuilder<TEvent, TState>
{
    private readonly Lazy<TestResult<TState, TEvent>> getResult;

    public ThenDeciderSpecificationBuilder(Lazy<TestResult<TState, TEvent>> getResult) =>
        this.getResult = getResult;

    public ThenDeciderSpecificationBuilder<TEvent, TState> Then(params TEvent[] expectedEvents)
    {
        var result = getResult.Value;
        result.NewEvents.Should().BeEquivalentTo(expectedEvents);
        return this;
    }

    public ThenDeciderSpecificationBuilder<TEvent, TState> Then(TState expectedState)
    {
        var result = getResult.Value;
        result.CurrentState.Should().BeEquivalentTo(expectedState);
        return this;
    }

    public ThenDeciderSpecificationBuilder<TEvent, TState> Then(params Action<TEvent[]>[] eventsAssertions)
    {
        var result = getResult.Value;

        foreach (var then in eventsAssertions)
        {
            then(result.NewEvents);
        }

        return this;
    }

    public ThenDeciderSpecificationBuilder<TEvent, TState> Then(params Action<TState>[] stateAssertions)
    {
        var result = getResult.Value;

        foreach (var then in stateAssertions)
        {
            then(result.CurrentState);
        }

        return this;
    }

    public ThenDeciderSpecificationBuilder<TEvent, TState> Then(params Action<TState, TEvent[]>[] assertions)
    {
        var result = getResult.Value;

        foreach (var then in assertions)
        {
            then(result.CurrentState, result.NewEvents);
        }

        return this;
    }

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
        }

        return this;
    }
}
