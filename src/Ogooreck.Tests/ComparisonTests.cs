using Ogooreck.Assertions;
using Ogooreck.BusinessLogic;
using Xunit;

namespace Ogooreck.Tests;

public class ComparisonTests
{
    private static ThenDeciderSpecificationBuilder<object, T> Result<T>(T state, params object[] events) =>
        new(new Lazy<TestResult<T, object>>(() => new(state, events)));

    [Fact]
    public void EventsMustBeInOrderWhileNestedCollectionsRemainEquivalent()
    {
        var result = Result(0, new Event("first", [1, 2]), new Event("second", [3]));
        result.Then(new Event("first", [2, 1]), new Event("second", [3]));
        var error = Assert.Throws<AssertionException>(() =>
            result.Then(new Event("second", [3]), new Event("first", [1, 2])));
        Assert.Contains("Event", error.Message);
        Assert.Contains("$[0].Name", error.Message);
    }

    [Fact]
    public void EventCountsAndDuplicatePositionsMustMatch()
    {
        Result(0).Then(Array.Empty<object>());
        var result = Result(0, new Event("a", []), new Event("a", []), new Event("b", []));
        Assert.Throws<AssertionException>(() => result.Then(new Event("a", []), new Event("b", [])));
        Assert.Throws<AssertionException>(() => result.Then(new Event("a", []), new Event("a", []), new Event("b", []), new Event("c", [])));
        Assert.Throws<AssertionException>(() => result.Then(new Event("a", []), new Event("b", []), new Event("a", [])));
        Assert.Throws<AssertionException>(() => Result(0).Then(new Event("a", [])));
    }

    [Fact]
    public void StateUsesDefaultUnorderedEquivalenceInsteadOfEventOrdering()
    {
        Result(new[] { 1, 1, 2 }).Then(new[] { 2, 1, 1 });
        Assert.Throws<AssertionException>(() => Result(new[] { 1, 1, 2 }).Then(new[] { 2, 2, 1 }));
        Assert.Throws<AssertionException>(() => Result(0, new Event("a", [1, 1, 2])).Then(new Event("a", [1, 2, 2])));
    }

    [Fact]
    public void DeclaredRootTypeControlsMemberSelection()
    {
        Base actual = new Derived { Id = 1, Detail = "actual" };
        Base expected = new Derived { Id = 1, Detail = "expected" };
        Result(actual).Then(expected);
        Assert.Throws<AssertionException>(() => Result<object>(actual).Then((object)expected));
    }

    [Fact]
    public void MissingExpectedExceptionFailsAndCallbackFailuresPropagate()
    {
        Assert.IsAssignableFrom<InvalidOperationException>(
            Assert.Throws<AssertionException>(() => Result(1).ThenThrows<InvalidOperationException>()));
        var thrown = new InvalidOperationException("expected");
        var result = new ThenDeciderSpecificationBuilder<object, int>(new(() => throw thrown));
        result.ThenThrows<InvalidOperationException>(e => Assert.Same(thrown, e));
        Assert.Throws<InvalidOperationException>(() => result.ThenThrows<ArgumentException>());
        Assert.Throws<ApplicationException>(() => result.ThenThrows<InvalidOperationException>(_ => throw new ApplicationException()));
    }

    public record Event(string Name, int[] Values);
    public class Base { public int Id { get; init; } }
    public class Derived : Base { public string? Detail { get; init; } }
}
