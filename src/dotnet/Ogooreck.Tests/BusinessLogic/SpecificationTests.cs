using Ogooreck.Assertions;
using Ogooreck.BusinessLogic;
using Xunit;

namespace Ogooreck.Tests.BusinessLogic;

public class SpecificationTests
{
    public record Counter(int Value);

    public record Increment(int By);

    public record Incremented(int By);

    private static Counter Evolve(Counter counter, Incremented incremented) => counter with { Value = counter.Value + incremented.By };

    private static Counter EvolveUntyped(Counter counter, object @event) => Evolve(counter, (Incremented)@event);

    private static Incremented IncrementedBy(Increment command, Counter counter) => new(command.By);

    private static Incremented[] IncrementedTwiceBy(Increment command, Counter counter) => [new(command.By), new(command.By)];

    private static DecideResult<Incremented, Counter> IncrementedWithState(Increment command, Counter counter) =>
        DecideResult.For(new Counter(counter.Value + command.By), new Incremented(command.By));

    [Fact]
    public void Specification_ForADecider_RunsItsDecideAndEvolveFunctions()
    {
        var decider = Decider.For<Increment, Incremented, Counter>(IncrementedBy, Evolve, () => new Counter(10));

        Specification.For(decider)
            .Given()
            .When(new Increment(2))
            .Then(new Incremented(2))
            .Then(new Counter(12));
    }

    [Fact]
    public void Specification_ForADecideFunctionReturningAResult_TakesTheStateFromTheResult()
    {
        Specification.For<Increment, Incremented, Counter>(IncrementedWithState, null, () => new Counter(1))
            .Given()
            .When(new Increment(2))
            .Then(new Counter(3));
    }

    [Fact]
    public void Specification_ForADecideFunctionReturningOneEvent_EvolvesTheStateFromTheInitialStateFunction()
    {
        Specification.For<Increment, Incremented, Counter>(IncrementedBy, Evolve, () => new Counter(5))
            .Given()
            .When(new Increment(2), new Increment(3))
            .Then(new Incremented(2), new Incremented(3))
            .Then(new Counter(10));
    }

    [Fact]
    public void Specification_ForADecideFunctionReturningOneEvent_EvolvesTheStateFromTheInitialState()
    {
        Specification.For<Increment, Incremented, Counter>(IncrementedBy, Evolve, new Counter(5))
            .Given()
            .When(new Increment(2))
            .Then(new Counter(7));
    }

    [Fact]
    public void Specification_ForADecideFunctionReturningOneEventWithoutAnInitialState_StartsFromTheDefaultState()
    {
        Specification.For<Increment, Incremented, Counter>(IncrementedBy, Evolve, (Counter?)null)
            .Given()
            .When(new Increment(2))
            .Then(new Counter(2));
    }

    [Fact]
    public void Specification_ForADecideFunctionReturningEvents_EvolvesTheStateFromTheInitialStateFunction()
    {
        Specification.For<Increment, Incremented, Counter>(IncrementedTwiceBy, Evolve, () => new Counter(1))
            .Given()
            .When(new Increment(2))
            .Then(new Incremented(2), new Incremented(2))
            .Then(new Counter(5));
    }

    [Fact]
    public void Specification_ForADecideFunctionReturningEvents_EvolvesTheStateFromTheInitialState()
    {
        Specification.For<Increment, Incremented, Counter>(IncrementedTwiceBy, Evolve, new Counter(1))
            .Given()
            .When(new Increment(2))
            .Then(new Counter(5));
    }

    [Fact]
    public void Specification_ForADecideFunctionReturningEventsWithoutAnInitialState_StartsFromTheDefaultState()
    {
        Specification.For<Increment, Incremented, Counter>(IncrementedTwiceBy, Evolve, (Counter?)null)
            .Given()
            .When(new Increment(2))
            .Then(new Counter(4));
    }

    [Fact]
    public void Specification_ForAnUntypedDecideFunctionReturningOneEvent_ComparesTheNewEvent()
    {
        Specification.For<Counter>((command, counter) => new Incremented(((Increment)command).By), EvolveUntyped, () => new Counter(0))
            .Given()
            .When(new Increment(3))
            .Then(new Incremented(3))
            .Then(new Counter(3));
    }

    [Fact]
    public void Specification_ForAnUntypedDecideFunctionReturningEvents_ComparesTheNewEvents()
    {
        Specification.For<Counter>(
                (object command, Counter counter) => new object[] { new Incremented(1), new Incremented(2) },
                EvolveUntyped,
                () => new Counter(0))
            .Given()
            .When(new Increment(0))
            .Then(new Incremented(1), new Incremented(2))
            .Then(new Counter(3));
    }

    [Fact]
    public void Specification_ForAnUntypedDecideFunctionReturningAResult_ComparesTheNewState()
    {
        Specification.For<Counter>(
                (object command, Counter counter) =>
                    DecideResult.For(new Counter(counter.Value + ((Increment)command).By)),
                null,
                () => new Counter(4))
            .Given()
            .When(new Increment(3))
            .Then(new Counter(7));
    }

    [Fact]
    public void DecideResult_ForUntypedEvents_CarriesTheEventsWithoutAState()
    {
        Specification.For<Counter>(
                (object command, Counter counter) => DecideResult.For<Counter>(new Incremented(2)),
                EvolveUntyped,
                () => new Counter(0))
            .Given()
            .When(new Increment(0))
            .Then(new Incremented(2))
            .Then(new Counter(2));
    }

    [Fact]
    public void Specification_WithoutAnEvolveFunction_KeepsTheStateWhenEventsAreProduced()
    {
        Specification.For<Increment, Incremented, Counter>(IncrementedBy, null, () => new Counter(5))
            .Given()
            .When(new Increment(2))
            .Then(new Incremented(2))
            .Then(new Counter(5));
    }

    [Fact]
    public void Then_ChecksTheEventsWithYourOwnAssertion()
    {
        var result = Specification.For<Increment, Incremented, Counter>(IncrementedBy, Evolve, () => new Counter(0))
            .Given()
            .When(new Increment(2));

        var checkedEvents = 0;
        result.Then(events => checkedEvents = events.Length, events => checkedEvents += events.Length);

        Assert.Equal(2, checkedEvents);
    }

    [Fact]
    public void Then_ChecksTheStateWithYourOwnAssertion()
    {
        var result = Specification.For<Increment, Incremented, Counter>(IncrementedBy, Evolve, () => new Counter(0))
            .Given()
            .When(new Increment(2));

        var observed = new List<int>();
        result.Then((Action<Counter>)(counter => observed.Add(counter.Value)), counter => observed.Add(counter.Value * 10));

        Assert.Equal([2, 20], observed);
    }

    [Fact]
    public void Then_ChecksTheStateAndTheEventsTogether()
    {
        var result = Specification.For<Increment, Incremented, Counter>(IncrementedBy, Evolve, () => new Counter(0))
            .Given()
            .When(new Increment(2));

        var observed = new List<string>();
        result.Then((Action<Counter, Incremented[]>)((counter, events) => observed.Add($"{counter.Value}:{events.Length}")));

        Assert.Equal(["2:1"], observed);
    }

    [Fact]
    public void Given_Events_EvolvesTheInitialStateFromThem()
    {
        Specification.For<Increment, Incremented, Counter>(IncrementedBy, Evolve, () => new Counter(1))
            .Given(new Incremented(2), new Incremented(3))
            .When(new Increment(4))
            .Then(new Counter(10));
    }

    [Fact]
    public void Given_AState_StartsFromIt()
    {
        Specification.For<Increment, Incremented, Counter>(IncrementedBy, Evolve, () => new Counter(1))
            .Given(new Counter(20))
            .When(new Increment(4))
            .Then(new Counter(24));
    }

    [Fact]
    public void Given_AStateFunction_StartsFromWhatItReturnsWhenTheWhenStepRuns()
    {
        var start = 1;
        var result = Specification.For<Increment, Incremented, Counter>(IncrementedBy, Evolve, () => new Counter(0))
            .Given(() => new Counter(start))
            .When(new Increment(4));

        start = 100;

        result.Then(new Counter(104));
    }

    [Fact]
    public void ThenThrows_FailsWhenNothingIsThrown()
    {
        var result = Specification.For<Increment, Incremented, Counter>(IncrementedBy, Evolve, () => new Counter(0))
            .Given()
            .When(new Increment(1));

        var error = Assert.Throws<AssertionException>(() => result.ThenThrows<InvalidOperationException>());

        Assert.Equal("Expected InvalidOperationException, but no exception was thrown.", error.Message);
    }

    [Fact]
    public void ThenThrows_PassesTheExceptionToYourAssertion()
    {
        var result = Specification.For<Increment, Incremented, Counter>(
                (Func<Increment, Counter, Incremented>)((command, counter) => throw new InvalidOperationException("closed")), Evolve, () => new Counter(0))
            .Given()
            .When(new Increment(1));

        string? message = null;
        result.ThenThrows<InvalidOperationException>(exception => message = exception.Message);

        Assert.Equal("closed", message);
    }

    [Fact]
    public void Specification_ForAHandlerWithADecideFunction_RunsTheHandlerAgainstTheState()
    {
        Specification.For<Incremented, Counter>(
                (handler, counter) => handler(counter),
                Evolve,
                () => new Counter(1))
            .Given()
            .When(counter => new Incremented(counter.Value + 1))
            .Then(new Incremented(2))
            .Then(new Counter(3));
    }

    [Fact]
    public void Specification_ForAHandlerOfUntypedEvents_RunsTheHandlerAgainstTheState()
    {
        Specification.For<Counter>(
                (handler, counter) => handler(counter),
                EvolveUntyped,
                () => new Counter(1))
            .Given()
            .When(counter => new Incremented(counter.Value + 1))
            .Then(new Incremented(2))
            .Then(new Counter(3));
    }

    [Fact]
    public void Specification_ForTypedEventsOnly_EvolvesTheStateFromWhatTheWhenStepReturns()
    {
        Specification.For<Incremented, Counter>(Evolve, () => new Counter(1))
            .Given(new Counter(5))
            .When(() => new Incremented(2))
            .Then(new Counter(7));
    }

    public static IEnumerable<object[]> WhenFunctions()
    {
        yield return [(Func<HandlerSpecification<Incremented, Counter>, object>)(spec =>
            spec.Given(new Counter(1)).When((Func<Counter, Counter>)(counter => counter with { Value = counter.Value + 1 })).Then(new Counter(2)))];
        yield return [(Func<HandlerSpecification<Incremented, Counter>, object>)(spec =>
            spec.Given(new Counter(1)).When((Func<Counter, Incremented>)(counter => new Incremented(counter.Value))).Then(new Incremented(1)))];
        yield return [(Func<HandlerSpecification<Incremented, Counter>, object>)(spec =>
            spec.Given(new Counter(1)).When((Func<Counter, Incremented[]>)(counter => [new Incremented(counter.Value), new Incremented(2)])).Then(new Incremented(1), new Incremented(2)))];
        yield return [(Func<HandlerSpecification<Incremented, Counter>, object>)(spec =>
            spec.Given(new Counter(1)).When((Func<Counter>)(() => new Counter(9))).Then(new Counter(9)))];
        yield return [(Func<HandlerSpecification<Incremented, Counter>, object>)(spec =>
            spec.Given(new Counter(1)).When((Func<Incremented[]>)(() => [new Incremented(1), new Incremented(2)])).Then(new Counter(4)))];
        yield return [(Func<HandlerSpecification<Incremented, Counter>, object>)(spec =>
            spec.Given(new Counter(1)).When((Func<Incremented>)(() => new Incremented(3))).Then(new Counter(4)))];
        yield return [(Func<HandlerSpecification<Incremented, Counter>, object>)(spec =>
            spec.Given(new Counter(1)).When((Action<Counter>)(counter => Assert.Equal(1, counter.Value))).Then(new Counter(1)))];
    }

    [Theory]
    [MemberData(nameof(WhenFunctions))]
    public void When_RunsEachKindOfFunctionAgainstTheGivenState(Func<HandlerSpecification<Incremented, Counter>, object> scenario) =>
        scenario(Specification.For<Incremented, Counter>(Evolve));

    [Fact]
    public void Given_NoEventsAndNoStateFunction_StartsFromTheDefaultOfTheStateType()
    {
        Specification.For<Incremented, Counter>(Evolve)
            .Given()
            .When(() => new Incremented(2))
            .Then(new Counter(2));
    }

    [Fact]
    public void InitialState_OfAString_IsEmpty()
    {
        Specification.For<string>(
                (command, state) => $"{state}{command}",
                (state, @event) => (string)@event,
                null)
            .Given()
            .When("a")
            .Then("a");
    }

    [Fact]
    public void InitialState_OfAValueType_IsItsDefault()
    {
        Specification.For<int>(
                (object command, int state) => DecideResult.For(state + (int)command),
                null,
                null)
            .Given()
            .When(3)
            .Then(3);
    }

    public sealed class WithPrivateConstructor
    {
        private WithPrivateConstructor() => Value = 7;

        public int Value { get; }
    }

    [Fact]
    public void InitialState_OfAClassWithAPrivateConstructor_IsCreatedByIt()
    {
        Specification.For<WithPrivateConstructor>(
                (command, state) => state.Value,
                (state, @event) => state,
                null)
            .Given()
            .When("anything")
            .Then(7);
    }
}
