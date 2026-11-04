using System.Diagnostics.CodeAnalysis;

namespace Ogooreck.Assertions;

internal static class Expect
{
    public static void That([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
            throw new AssertionException(message);
    }

    public static void Equal<T>(T actual, T expected, string description) =>
        That(EqualityComparer<T>.Default.Equals(actual, expected),
            $"{description}: expected <{expected}>, but found <{actual}>.");

    public static void Equivalent<T>(T actual, T expected, string description) =>
        StructuralComparison.AssertEquivalent(actual, expected, description);

    public static void OrderedEquivalent<T>(IReadOnlyList<T> actual, IReadOnlyList<T> expected, string description)
        => StructuralComparison.AssertOrderedEquivalent(actual, expected, description);
}
