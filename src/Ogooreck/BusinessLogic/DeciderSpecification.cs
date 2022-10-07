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

    public GivenDeciderSpecificationBuilder<TCommand, TEvent, TState> Given(params TEvent[] events) =>
        Given(() =>
        {
            var currentState = decider.GetInitialState();

            return events.Aggregate(currentState, decider.Evolve);
        });

    public GivenDeciderSpecificationBuilder<TCommand, TEvent, TState> Given(TState currentState) =>
        Given(() => currentState);

    public GivenDeciderSpecificationBuilder<TCommand, TEvent, TState> Given() =>
        new(decider);

    public GivenDeciderSpecificationBuilder<TCommand, TEvent, TState> Given(Func<TState> getCurrentState) =>
        new(decider, getCurrentState);
}

public class GivenDeciderSpecificationBuilder<TCommand, TEvent, TState>
{
    private readonly Decider<TCommand, TEvent, TState> decider;
    private readonly Func<TState>? getCurrentState;

    public GivenDeciderSpecificationBuilder(
        Decider<TCommand, TEvent, TState> decider,
        Func<TState>? getCurrentState = null
    )
    {
        this.decider = decider;
        this.getCurrentState = getCurrentState;
    }

    public WhenDeciderSpecificationBuilder<TCommand, TEvent, TState> When(params TCommand[] commands) =>
        new(decider, getCurrentState, commands);
}

public class WhenDeciderSpecificationBuilder<TCommand, TEvent, TState>
{
    private readonly Decider<TCommand, TEvent, TState> decider;
    private readonly TCommand[] commands;
    private readonly Func<TState>? getCurrentState;
    private readonly Lazy<TestResult<TState, TEvent>> getResult;

    public WhenDeciderSpecificationBuilder(
        Decider<TCommand, TEvent, TState> decider,
        Func<TState>? getCurrentState,
        TCommand[] commands
    )
    {
        this.decider = decider;
        this.commands = commands;
        this.getCurrentState = getCurrentState;
        getResult = new Lazy<TestResult<TState, TEvent>>(Perform);
    }

    public void Then(params TEvent[] expectedEvents)
    {
        var result = getResult.Value;
        result.NewEvents.Should().BeEquivalentTo(expectedEvents);
    }

    public void Then(TState expectedState)
    {
        var result = getResult.Value;
        result.CurrentState.Should().BeEquivalentTo(expectedState);
    }

    public void Then(params Action<TEvent[]>[] thens)
    {
        var result = getResult.Value;

        foreach (var then in thens)
        {
            then(result.NewEvents);
        }
    }

    public void Then(params Action<TState>[] thens)
    {
        var result = getResult.Value;

        foreach (var then in thens)
        {
            then(result.CurrentState);
        }
    }

    public void Then(params Action<TState, TEvent[]>[] thens)
    {
        var result = getResult.Value;

        foreach (var then in thens)
        {
            then(result.CurrentState, result.NewEvents);
        }
    }

    public void ThenThrows<TException>(Action<TException>? assert = null) where TException : Exception
    {
        try
        {
            var _ = getResult.Value;
        }
        catch (TException e)
        {
            assert?.Invoke(e);
        }
    }

    private TestResult<TState, TEvent> Perform()
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
    }
}
