namespace Ogooreck.Assertions;

/// <summary>A failed Ogooreck expectation, independent of the test framework.</summary>
/// <example>
/// <code>
/// var error = await Assert.ThrowsAsync&lt;AssertionException&gt;(() =&gt;
///     spec.Given().When(GetCart(cartId)).Then([ExpectResponse(OK)], ct));
/// </code>
/// </example>
public sealed class AssertionException: InvalidOperationException
{
    /// <summary>Creates a failure with its message.</summary>
    /// <example>
    /// <code>
    /// throw new AssertionException($"{outcome.When}: expected {expected}, but found {actual}.");
    /// </code>
    /// </example>
    public AssertionException(string message) : base(message) { }

    /// <summary>Creates a failure caused by another error, kept as <see cref="Exception.InnerException"/>. The message is used as given, so include the inner error's message in it.</summary>
    /// <example>
    /// <code>
    /// throw new AssertionException($"Starting the host failed: {exception.Message}", exception);
    /// </code>
    /// </example>
    public AssertionException(string message, Exception innerException) : base(message, innerException) { }
}
