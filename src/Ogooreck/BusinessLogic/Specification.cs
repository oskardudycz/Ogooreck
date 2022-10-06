using FluentAssertions;
using Ogooreck.Factories;

#pragma warning disable CS1591

namespace Ogooreck.BusinessLogic;

using static Specification;

public class DeciderSpecification<TState>: DeciderSpecification<object, object, TState>
{
    public DeciderSpecification(Decider<object, object, TState> decider): base(decider) { }
}

public class AggregateSpecification<TEvent, TState>: DeciderSpecification<Action<TState>, TEvent, TState>
{
    public AggregateSpecification(Decider<Action<TState>, TEvent, TState> decider): base(decider) { }
}

public class AggregateSpecification<TState>: AggregateSpecification<object, TState>
{
    public AggregateSpecification(Decider<Action<TState>, object, TState> decider): base(decider) { }
}

public class
    HandlerSpecification<TEvent, TState>: DeciderSpecification<Func<TState, DecideResult<TEvent>>, TEvent, TState>
{
    public HandlerSpecification(Decider<Func<TState, DecideResult<TEvent>>, TEvent, TState> decider): base(decider) { }
}

public class HandlerSpecification<TState>: HandlerSpecification<object, TState>
{
    public HandlerSpecification(Decider<Func<TState, DecideResult<object>>, object, TState> decider): base(decider) { }
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
    private readonly Lazy<BusinessLogicThenResult<TState, TEvent>> getResult;

    public WhenDeciderSpecificationBuilder(
        Decider<TCommand, TEvent, TState> decider,
        Func<TState>? getCurrentState,
        TCommand[] commands
    )
    {
        this.decider = decider;
        this.commands = commands;
        this.getCurrentState = getCurrentState;
        getResult = new Lazy<BusinessLogicThenResult<TState, TEvent>>(Perform);
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

    private BusinessLogicThenResult<TState, TEvent> Perform()
    {
        var currentState = (getCurrentState ?? decider.GetInitialState)();
        var resultEvents = new List<TEvent>();

        foreach (var command in commands)
        {
            var newEvents = decider.Decide(command, currentState);
            resultEvents.AddRange(newEvents);

            currentState = newEvents.Aggregate(currentState, decider.Evolve);
        }

        return new BusinessLogicThenResult<TState, TEvent>(currentState, resultEvents.ToArray());
    }
}

public class Specification
{
    public static DeciderSpecification<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Decider<TCommand, TEvent, TState> decider
    ) =>
        new(decider);

    public static DeciderSpecification<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Func<TCommand, TState, TEvent[]> decide,
        Func<TState, TEvent, TState> evolve,
        Func<TState>? getInitialState = null
    ) =>
        For(
            new Decider<TCommand, TEvent, TState>(
                (command, currentState) => decide(command, currentState),
                evolve,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static DeciderSpecification<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Func<TCommand, TState, TEvent> decide,
        Func<TState, TEvent, TState> evolve,
        Func<TState>? getInitialState
    ) =>
        For(
            new Decider<TCommand, TEvent, TState>(
                (command, currentState) => new[] { decide(command, currentState) },
                evolve,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static DeciderSpecification<Action<TState>, TEvent, TState> For<TEvent, TState>(
        Func<Action<TState>, TState, TEvent> decide,
        Func<TState, TEvent, TState> evolve,
        Func<TState>? getInitialState
    ) =>
        For(
            new Decider<Action<TState>, TEvent, TState>(
                (command, currentState) => new[] { decide(command, currentState) },
                evolve,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static DeciderSpecification<Action<TState>, TEvent, TState> For<TEvent, TState>(
        Func<Action<TState>, TState, TEvent[]> decide,
        Func<TState, TEvent, TState> evolve,
        Func<TState>? getInitialState
    ) =>
        For(
            new Decider<Action<TState>, TEvent, TState>(
                (command, currentState) => decide(command, currentState),
                evolve,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static DeciderSpecification<TState> For<TState>(
        Func<object, TState, object> decide,
        Func<TState, object, TState> evolve,
        Func<TState>? getInitialState = null
    ) =>
        new(
            new Decider<object, object, TState>(
                (command, state) => new[] { decide(command, state) },
                evolve,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static DeciderSpecification<TState> For<TState>(
        Func<object, TState, object[]> decide,
        Func<TState, object, TState> evolve,
        Func<TState>? getInitialState = null
    ) =>
        new(
            new Decider<object, object, TState>(
                decide,
                evolve,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static AggregateSpecification<TState> For<TState>(
        Func<Action<TState>, TState, object> decide,
        Func<TState, object, TState> evolve,
        Func<TState>? getInitialState = null
    ) =>
        new(
            new Decider<Action<TState>, object, TState>(
                (command, state) => new[] { decide(command, state) },
                evolve,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static AggregateSpecification<TState> For<TState>(
        Func<Action<TState>, TState, object[]> decide,
        Func<TState, object, TState> evolve,
        Func<TState>? getInitialState = null
    ) =>
        new(
            new Decider<Action<TState>, object, TState>(
                decide,
                evolve,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );


    public static HandlerSpecification<TState> For<TState>(
        Func<TState, object, TState> evolve,
        Func<TState>? getInitialState = null
    ) =>
        new(
            new Decider<Func<TState, DecideResult<object>>, object, TState>(
                (handler, state) => handler(state),
                evolve,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    internal record BusinessLogicThenResult<TState, TEvent>(
        TState CurrentState,
        TEvent[] NewEvents
    );

    public record DecideResult<TEvent>(
        TEvent[] Events
    )
    {
        public static implicit operator DecideResult<TEvent>(TEvent @event) => new(new[] { @event });

        public static implicit operator DecideResult<TEvent>(TEvent[] events) => new(events);

        public static implicit operator TEvent[](DecideResult<TEvent> result) => result.Events;
    }
}

