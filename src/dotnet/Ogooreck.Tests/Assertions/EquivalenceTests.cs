using System.Collections;
using Ogooreck.Assertions;
using Ogooreck.BusinessLogic;
using Xunit;

namespace Ogooreck.Tests.Assertions;

public class EquivalenceTests
{
    private static void AssertEquivalent<T>(T actual, T expected) =>
        new ThenDeciderSpecificationBuilder<object, T>(new(() => new TestResult<T, object>(actual, []))).Then(expected);

    private static string FailureOf<T>(T actual, T expected) =>
        Assert.Throws<AssertionException>(() => AssertEquivalent(actual, expected)).Message;

    [Fact]
    public void Equivalent_FailsWhenAnObjectIsComparedWithANonGenericDictionary() =>
        Assert.Contains("expected <System.Collections.Hashtable>",
            FailureOf<object>(new object(), new Hashtable { ["a"] = 1 }));

    [Fact]
    public void Equivalent_FailsWhenNonGenericDictionariesHaveDifferentCounts() =>
        Assert.Contains("$: expected 2 items, but found 1",
            FailureOf<object>(new Hashtable { ["a"] = 1 }, new Hashtable { ["a"] = 1, ["b"] = 2 }));

    [Fact]
    public void Equivalent_FailsWhenANonGenericDictionaryValueDiffers() =>
        Assert.Contains("$[a]: expected <2>, but found <1>",
            FailureOf<object>(new Hashtable { ["a"] = 1 }, new Hashtable { ["a"] = 2 }));

    private sealed class Mixed: IEnumerable<int>, IEnumerable<string>
    {
        IEnumerator<int> IEnumerable<int>.GetEnumerator() => Enumerable.Empty<int>().GetEnumerator();

        IEnumerator<string> IEnumerable<string>.GetEnumerator() => Enumerable.Empty<string>().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => Enumerable.Empty<int>().GetEnumerator();
    }

    [Fact]
    public void Equivalent_FailsForACollectionWithMoreThanOneElementType() =>
        Assert.Contains("expected collection has ambiguous element types",
            FailureOf<object>(new Mixed(), new Mixed()));

    private sealed class Empty;

    private sealed class Holder
    {
        public Empty Item { get; init; } = new();
    }

    [Fact]
    public void Equivalent_FailsForACollectionOfObjectsWithoutMembers() =>
        Assert.Contains("$[0]: no members were found for comparison",
            FailureOf<Empty[]>([new Empty()], [new Empty()]));

    [Fact]
    public void Equivalent_PassesForAMemberWithoutMembersBelowACollectionItem() =>
        AssertEquivalent<Holder[]>([new Holder()], [new Holder()]);

    [Fact]
    public void Equivalent_PassesForACollectionNestedInACollectionOfObjectsWithoutMembers() =>
        AssertEquivalent<Empty[][]>([[new Empty()]], [[new Empty()]]);

    [Fact]
    public void Equivalent_FailsWhenKeyValuePairsHaveAnotherKeyType() =>
        Assert.Contains("expected <System.Collections.Generic.Dictionary",
            FailureOf<object>(
                new List<KeyValuePair<int, int>> { new(1, 1) },
                new Dictionary<string, int> { ["1"] = 1 }));

    [Fact]
    public void Equivalent_FailsWhenASequenceIsComparedWithADictionary() =>
        Assert.Contains("expected <System.Collections.Generic.Dictionary",
            FailureOf<object>(new List<int> { 1 }, new Dictionary<string, int> { ["1"] = 1 }));

    [Fact]
    public void Equivalent_FailsWhenASequenceOfGenericItemsIsComparedWithADictionary() =>
        Assert.Contains("expected <System.Collections.Generic.Dictionary",
            FailureOf<object>(new List<List<int>> { new() { 1 } }, new Dictionary<string, int> { ["1"] = 1 }));

    public static TheoryData<object> NumbersEqualToOne => new()
    {
        (sbyte)1, (byte)1, (short)1, (ushort)1, 1, 1u, 1L, 1UL, 1f, 1d, 1m
    };

    [Theory]
    [MemberData(nameof(NumbersEqualToOne))]
    public void Equivalent_TreatsNumbersOfAnyWidthWithTheSameValueAsEqual(object number)
    {
        AssertEquivalent<object>(number, 1.0m);
        AssertEquivalent<object>(1.0m, number);
    }

    private sealed class CustomEquality<T>(int id)
    {
        public int Id { get; } = id;

        public int Detail { get; init; }

        public override bool Equals(object? other) => other is CustomEquality<T> value && value.Id == Id;

        public override int GetHashCode() => Id;
    }

    [Fact]
    public void Equivalent_UsesTheEqualsOfAGenericType() =>
        AssertEquivalent(new CustomEquality<string>(1) { Detail = 1 }, new CustomEquality<string>(1) { Detail = 2 });

    private sealed class WithUnreadableProperties
    {
        public int Id { get; init; }

        public int WriteOnly
        {
            set { }
        }

        public int PrivateGet
        {
            private get => 1;
            set { }
        }
    }

    [Fact]
    public void Equivalent_IgnoresPropertiesThatCannotBePubliclyRead() =>
        AssertEquivalent(new WithUnreadableProperties { Id = 1 }, new WithUnreadableProperties { Id = 1 });
}
