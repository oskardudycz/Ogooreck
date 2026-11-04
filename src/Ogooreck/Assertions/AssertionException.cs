namespace Ogooreck.Assertions;

/// <summary>A failed Ogooreck expectation, independent of the test framework.</summary>
public sealed class AssertionException(string message) : InvalidOperationException(message);
