using Ogooreck.Assertions;
using Ogooreck.BusinessLogic;
using Xunit;

namespace Ogooreck.Tests;

public class ComparisonCharacterizationTests
{
    public static TheoryData<string, bool> Cases
    {
        get
        {
            var data = new TheoryData<string, bool>();
            foreach (var (name, scenario) in ComparisonCases.All) data.Add(name, scenario.Equivalent);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void DefaultComparisonMatchesTheCharacterizedBehavior(string name, bool equivalent)
    {
        var (actual, expected) = ComparisonCases.All[name].Values();
        var result = new ThenDeciderSpecificationBuilder<object, object?>(new(() => new(actual, [])));
        if (equivalent) result.Then(expected);
        else Assert.Throws<AssertionException>(() => result.Then(expected));
    }

    [Fact]
    public void DuplicateKeysCannotBeConvertedIntoADictionary()
    {
        object actual = new[] { new KeyValuePair<string, int>("a", 1), new KeyValuePair<string, int>("a", 2) };
        object expected = new Dictionary<string, int> { ["a"] = 1 };
        var result = new ThenDeciderSpecificationBuilder<object, object>(new(() => new(actual, [])));
        Assert.Throws<ArgumentException>(() => result.Then(expected));
    }
}
