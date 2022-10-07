using FluentAssertions;
using Ogooreck.Factories;

#pragma warning disable CS1591

namespace Ogooreck.BusinessLogic;

using static Specification;

public class DeciderSpecification<TState>
    : DeciderSpecification<object, object, TState>
{
    public DeciderSpecification(Decider<object, object, TState> decider): base(decider) { }
}

public class AggregateSpecification<TEvent, TState>
    : DeciderSpecification<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState>
{
    public AggregateSpecification(Decider<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState> decider):
        base(decider)
    {
    }
}

public class AggregateSpecification<TState>
    : AggregateSpecification<object, TState>
{
    public AggregateSpecification(Decider<Func<TState, DecideResult<object, TState>>, object, TState> decider):
        base(decider)
    {
    }
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
    private readonly Lazy<BusinessLogicResult<TState, TEvent>> getResult;

    public WhenDeciderSpecificationBuilder(
        Decider<TCommand, TEvent, TState> decider,
        Func<TState>? getCurrentState,
        TCommand[] commands
    )
    {
        this.decider = decider;
        this.commands = commands;
        this.getCurrentState = getCurrentState;
        getResult = new Lazy<BusinessLogicResult<TState, TEvent>>(Perform);
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

    private BusinessLogicResult<TState, TEvent> Perform()
    {
        var currentState = (getCurrentState ?? decider.GetInitialState)();
        var resultEvents = new List<TEvent>();

        foreach (var command in commands)
        {
            var (newEvents, state) = decider.Decide(command, currentState);
            resultEvents.AddRange(newEvents);

            currentState = state ?? newEvents.Aggregate(currentState, decider.Evolve);
        }

        return new BusinessLogicResult<TState, TEvent>(currentState, resultEvents.ToArray());
    }
}

public static class Specification
{
    public static DeciderSpecification<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Decider<TCommand, TEvent, TState> decider
    ) =>
        new(decider);

    public static DeciderSpecification<TCommand, TEvent, TState> For<TCommand, TEvent, TState>(
        Func<TCommand, TState, TEvent[]> decide,
        Func<TState, TEvent, TState> evolve,
        Func<TState>? getInitialState = null
    ) where TState : notnull =>
        For(
            new Decider<TCommand, TEvent, TState>(
                (command, currentState) =>
                {
                    var newEvents = decide(command, currentState);

                    return new DecideResult<TEvent, TState>(newEvents);
                },
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
                (command, currentState) => DecideResult<TEvent, TState>.For(decide(command, currentState)),
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
                (command, currentState) => DecideResult<TEvent, TState>.For(decide(command, currentState)),
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
                (command, currentState) => DecideResult<TEvent, TState>.For(decide(command, currentState)),
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
                (command, currentState) => DecideResult<object, TState>.For(decide(command, currentState)),
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
                (command, currentState) => DecideResult<object, TState>.For(decide(command, currentState)),
                evolve,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static AggregateSpecification<TState> For<TState>(
        Func<Func<TState, DecideResult<object, TState>>, TState, DecideResult<object, TState>> decide,
        Func<TState, object, TState> evolve,
        Func<TState>? getInitialState = null
    ) =>
        new(
            new Decider<Func<TState, DecideResult<object, TState>>, object, TState>(
                decide,
                evolve,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static WhenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState>
        When<TEvent, TState>(
            this GivenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState> given,
            Func<TState, TEvent> when
        ) =>
        given.When(state => DecideResult<TEvent, TState>.For(when(state)));

    public static WhenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState>
        When<TEvent, TState>(
            this GivenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState> given,
            Func<TState, TEvent[]> when
        ) =>
        given.When(state => DecideResult<TEvent, TState>.For(when(state)));

    public static WhenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState>
        When<TEvent, TState>(
            this GivenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState> given,
            Action<TState> when
        ) =>
        given.When(state =>
        {
            when(state);
            return DecideResult<TEvent, TState>.For(state);
        });

    public static WhenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState>
        When<TEvent, TState>(
            this GivenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState> given,
            Func<TState> when
        ) =>
        given.When(_ => DecideResult<TEvent, TState>.For(when()));

    public static WhenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState>
        When<TEvent, TState>(
            this GivenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState> given,
            Func<TEvent[]> when
        ) =>
        given.When(_ => DecideResult<TEvent, TState>.For(when()));

    public static WhenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState>
        When<TEvent, TState>(
            this GivenDeciderSpecificationBuilder<Func<TState, DecideResult<TEvent, TState>>, TEvent, TState> given,
            Func<TEvent> when
        ) =>
        given.When(_ => DecideResult<TEvent, TState>.For(when()));


    public static AggregateSpecification<TState> For<TState>(
        Func<TState, object, TState> evolve,
        Func<TState>? getInitialState = null
    ) =>
        new(
            new Decider<Func<TState, DecideResult<object, TState>>, object, TState>(
                (handler, currentState) => handler(currentState),
                evolve,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );


    public static AggregateSpecification<TState> For<TState>(
        Func<TState>? getInitialState = null
    ) =>
        new(
            new Decider<Func<TState, DecideResult<object, TState>>, object, TState>(
                (handler, currentState) => handler(currentState),
                (state, _) => state,
                getInitialState ?? ObjectFactory<TState>.GetDefaultOrUninitialized
            )
        );

    public static WhenDeciderSpecificationBuilder<Func<TState, TEvent[]>, TEvent, TState> When<TEvent, TState>(
        this GivenDeciderSpecificationBuilder<Func<TState, TEvent[]>, TEvent, TState> given,
        Func<TState, TEvent> when
    ) =>
        given.When(state => new[] { when(state) });

    public static WhenDeciderSpecificationBuilder<Func<TState, TEvent[]>, TEvent, TState> When<TEvent, TState>(
        this GivenDeciderSpecificationBuilder<Func<TState, TEvent[]>, TEvent, TState> given,
        Func<TEvent> when
    ) =>
        given.When(_ => new[] { when() });

    public static WhenDeciderSpecificationBuilder<Func<TState, TEvent[]>, TEvent, TState> When<TEvent, TState>(
        this GivenDeciderSpecificationBuilder<Func<TState, TEvent[]>, TEvent, TState> given,
        Func<TEvent[]> when
    ) =>
        given.When(_ => when());

    internal record BusinessLogicResult<TState, TEvent>(
        TState CurrentState,
        TEvent[] NewEvents
    );
}
