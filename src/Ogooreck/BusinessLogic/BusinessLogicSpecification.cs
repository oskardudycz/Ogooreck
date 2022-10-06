using FluentAssertions;
using Ogooreck.Factories;

#pragma warning disable CS1591

namespace Ogooreck.BusinessLogic;

/// <summary>
///
/// </summary>
public static class BusinessLogicSpecification
{
    ///////////////////
    ////   GIVEN   ////
    ///////////////////

    public static GivenBusinessLogicSpecificationBuilder<TState, object> Given<TState>() =>
        Given<TState, object>((state, _) => state);

    public static GivenBusinessLogicSpecificationBuilder<TState, object> Given<TState>(
        Func<TState, object, TState> evolve
    ) =>
        Given<TState, object>(evolve);

    public static GivenBusinessLogicSpecificationBuilder<TState, object> Given<TState>(
        Func<TState, object, TState> evolve,
        Func<IEnumerable<object>> events
    ) =>
        Given<TState, object>(evolve, events);

    public static GivenBusinessLogicSpecificationBuilder<TState, object> Given<TState>(
        Func<TState, object, TState> evolve,
        Func<object> events
    ) =>
        Given<TState, object>(evolve, () => new[] { events() });

    public static GivenBusinessLogicSpecificationBuilder<TState, TEvent> Given<TState, TEvent>(
        Func<TState, TEvent, TState> evolve
    ) =>
        Given(evolve, Array.Empty<TEvent>);

    public static GivenBusinessLogicSpecificationBuilder<TState, TEvent> Given<TState, TEvent>(
        Func<TState, TEvent, TState> evolve,
        Func<IEnumerable<TEvent>> events
    ) =>
        new(evolve, ObjectFactory<TState>.GetDefaultOrUninitialized, events);

    public static GivenBusinessLogicSpecificationBuilder<TState, TEvent> Given<TState, TEvent>(
        Func<TState, TEvent, TState> evolve,
        Func<TState> getInitialState
    ) =>
        new(evolve, getInitialState, Array.Empty<TEvent>);


    public static GivenBusinessLogicSpecificationBuilder<TState, TEvent> Given<TState, TEvent>(
        Func<TState, TEvent, TState> evolve,
        Func<TState> getInitialState,
        Func<IEnumerable<TEvent>> events
    ) =>
        new(evolve, getInitialState, events);


    /////////////////////
    ////   BUILDER   ////
    /////////////////////

    public class GivenBusinessLogicSpecificationBuilder<TState, TEvent>
    {
        private readonly Func<TState, TEvent, TState> evolve;
        private readonly Func<TState> getInitialState;
        private readonly Func<IEnumerable<TEvent>> getEvents;

        public GivenBusinessLogicSpecificationBuilder(
            Func<TState, TEvent, TState> evolve,
            Func<TState> getInitialState,
            Func<IEnumerable<TEvent>> getEvents
        )
        {
            this.getInitialState = getInitialState;
            this.evolve = evolve;
            this.getEvents = getEvents;
        }

        public WhenBusinessLogicSpecificationBuilder<TState, TEvent> When(
            params Func<TState, BusinessLogicThenResult<TState, TEvent>>[] whens
        ) =>
            new(evolve, getInitialState, getEvents, whens);


        /// <summary>
        /// Define the business logic to be run by the test
        /// </summary>
        /// <param name="whens"></param>
        /// <returns></returns>
        public WhenBusinessLogicSpecificationBuilder<TState, TEvent> When(
            params Func<TState, TState>[] whens
        ) =>
            new(evolve, getInitialState, getEvents, whens);

        public WhenBusinessLogicSpecificationBuilder<TState, TEvent> When(
            params Func<TState, TEvent[]>[] whens
        ) =>
            new(evolve, getInitialState, getEvents, whens);

        public WhenBusinessLogicSpecificationBuilder<TState, TEvent> When(
            params Func<TState, TEvent>[] whens
        ) =>
            new(evolve, getInitialState, getEvents, whens);

        public WhenBusinessLogicSpecificationBuilder<TState, TEvent> When(
            params Func<TState, (TState, TEvent[])>[] whens
        ) =>
            new(evolve, getInitialState, getEvents, whens);

        public WhenBusinessLogicSpecificationBuilder<TState, TEvent> When(
            params Func<TState, (TState, TEvent)>[] whens
        ) =>
            new(evolve, getInitialState, getEvents, whens);
    }

    public class WhenBusinessLogicSpecificationBuilder<TState, TEvent>
    {
        private readonly Func<TState, TEvent, TState> evolve;
        private readonly Func<TState> getInitialState;
        private readonly Func<TState, BusinessLogicThenResult<TState, TEvent>>[] whens;
        private readonly Func<IEnumerable<TEvent>> getEvents;
        private readonly Lazy<BusinessLogicThenResult<TState, TEvent>> getResult;

        public WhenBusinessLogicSpecificationBuilder(
            Func<TState, TEvent, TState> evolve,
            Func<TState> getInitialState,
            Func<IEnumerable<TEvent>> getEvents,
            params Func<TState, TEvent[]>[] whens
        ): this(evolve, getInitialState, getEvents, whens.Select(when => ToWhen(when, evolve)).ToArray())
        {
        }

        public WhenBusinessLogicSpecificationBuilder(
            Func<TState, TEvent, TState> evolve,
            Func<TState> getInitialState,
            Func<IEnumerable<TEvent>> getEvents,
            params Func<TState, TEvent>[] whens
        ): this(evolve, getInitialState, getEvents, whens.Select(when => ToWhen(when, evolve)).ToArray())
        {
        }

        public WhenBusinessLogicSpecificationBuilder(
            Func<TState, TEvent, TState> evolve,
            Func<TState> getInitialState,
            Func<IEnumerable<TEvent>> getEvents,
            params Func<TState, (TState, TEvent[])>[] whens
        ): this(evolve, getInitialState, getEvents, whens.Select(ToWhen).ToArray())
        {
        }

        public WhenBusinessLogicSpecificationBuilder(
            Func<TState, TEvent, TState> evolve,
            Func<TState> getInitialState,
            Func<IEnumerable<TEvent>> getEvents,
            params Func<TState, (TState, TEvent)>[] whens
        ): this(evolve, getInitialState, getEvents, whens.Select(ToWhen).ToArray())
        {
        }

        public WhenBusinessLogicSpecificationBuilder(
            Func<TState, TEvent, TState> evolve,
            Func<TState> getInitialState,
            Func<IEnumerable<TEvent>> getEvents,
            params Func<TState, TState>[] whens
        ): this(evolve, getInitialState, getEvents, whens.Select(ToWhen).ToArray())
        {
        }

        public WhenBusinessLogicSpecificationBuilder(
            Func<TState, TEvent, TState> evolve,
            Func<TState> getInitialState,
            Func<IEnumerable<TEvent>> getEvents,
            Func<TState, BusinessLogicThenResult<TState, TEvent>>[] whens
        )
        {
            this.evolve = evolve;
            this.getInitialState = getInitialState;
            this.whens = whens;
            this.getEvents = getEvents;

            getResult = new Lazy<BusinessLogicThenResult<TState, TEvent>>(Perform);
        }

        public void Then(Action<TEvent[]> then)
        {
            then(getResult.Value.NewEvents);
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

        public void Then(params Action<BusinessLogicThenResult<TState, TEvent>>[] thens)
        {
            var result = getResult.Value;

            foreach (var then in thens)
            {
                then(result);
            }
        }

        private BusinessLogicThenResult<TState, TEvent> Perform()
        {
            var events = getEvents().ToList();
            var currentState = events.Aggregate(getInitialState(), evolve);
            var resultEvents = new List<TEvent>();

            foreach (var when in whens)
            {
                var (newState, newEvents) = when(currentState);
                resultEvents.AddRange(newEvents);

                currentState = newState;
            }

            return new BusinessLogicThenResult<TState, TEvent>(currentState, resultEvents.ToArray());
        }

        private static Func<TState, BusinessLogicThenResult<TState, TEvent>> ToWhen(
            Func<TState, TState> when
        ) =>
            currentState =>
            {
                var newState = when(currentState);
                return new BusinessLogicThenResult<TState, TEvent>(newState, Array.Empty<TEvent>());
            };

        private static Func<TState, BusinessLogicThenResult<TState, TEvent>> ToWhen(
            Func<TState, TEvent> when,
            Func<TState, TEvent, TState> evolve
        ) =>
            currentState =>
            {
                var newEvent = when(currentState);
                var newState = evolve(currentState, newEvent);
                return new BusinessLogicThenResult<TState, TEvent>(newState, new[] { newEvent });
            };

        private static Func<TState, BusinessLogicThenResult<TState, TEvent>> ToWhen(
            Func<TState, TEvent[]> when,
            Func<TState, TEvent, TState> evolve
        ) =>
            currentState =>
            {
                var newEvents = when(currentState);
                var newState = newEvents.Aggregate(currentState, evolve);
                return new BusinessLogicThenResult<TState, TEvent>(newState, newEvents);
            };

        private static Func<TState, BusinessLogicThenResult<TState, TEvent>> ToWhen(
            Func<TState, (TState, TEvent[])> when
        ) =>
            currentState =>
            {
                var updated = when(currentState);
                return new BusinessLogicThenResult<TState, TEvent>(updated.Item1, updated.Item2);
            };

        private static Func<TState, BusinessLogicThenResult<TState, TEvent>> ToWhen(
            Func<TState, (TState, TEvent)> when
        ) =>
            currentState =>
            {
                var updated = when(currentState);
                return new BusinessLogicThenResult<TState, TEvent>(updated.Item1, new[] { updated.Item2 });
            };
    }

    public record BusinessLogicThenResult<TState, TEvent>(
        TState CurrentState,
        TEvent[] NewEvents
    );


    public static Action<object[]> EVENT(
        object expectedEvent
    ) =>
        EVENT<object>(expectedEvent);

    public static Action<TEvent[]> EVENT<TEvent>(
        object expectedEvent
    ) =>
        events =>
        {
            events.Should().HaveCount(1);
            events.Single().Should().BeEquivalentTo(expectedEvent);
        };

    public static Action<object[]> EVENTS(
        params object[] expectedEvents
    ) =>
        EVENTS<object>(expectedEvents);

    public static Action<TEvent[]> EVENTS<TEvent>(
        params object[] expectedEvents
    ) =>
        events => events.Should().BeEquivalentTo(expectedEvents);

    public static Action<BusinessLogicThenResult<TState, TEvent>> STATE<TState, TEvent>(TState state) =>
        result => result.CurrentState.Should().BeEquivalentTo(state);

    public static Action<object[]> EVENT_OF_TYPE<TEvent>() =>
        events =>
        {
            events.Should().HaveCount(1);
            events.Single().Should().BeOfType<TEvent>();
        };
}
