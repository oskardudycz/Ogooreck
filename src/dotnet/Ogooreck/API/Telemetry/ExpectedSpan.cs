namespace Ogooreck.API;

/// <summary>
/// A span that <c>ExpectTrace</c> looks for: its name, the attributes it must have, and the spans expected below it.
/// Build it with <c>Span(...)</c>, for example <c>Span("POST /carts/{id}/orders", Span("publish OrderPlaced"))</c>.
/// </summary>
public sealed class ExpectedSpan
{
    internal ExpectedSpan(string name, IReadOnlyDictionary<string, string> attributes, IReadOnlyList<ExpectedSpan> children)
    {
        Name = name;
        Attributes = attributes;
        Children = children;
    }

    /// <summary>The span name to match exactly.</summary>
    /// <example>
    /// <code>
    /// Span("publish OrderPlaced").Name // "publish OrderPlaced"
    /// </code>
    /// </example>
    public string Name { get; }

    /// <summary>The attributes the span must have, compared as strings; the span may have others.</summary>
    /// <example>
    /// <code>
    /// Span("POST /carts/{id}/orders", new Dictionary&lt;string, string&gt; { ["cart.closed"] = "true" }).Attributes
    /// </code>
    /// </example>
    public IReadOnlyDictionary<string, string> Attributes { get; }

    /// <summary>The spans that must be anywhere below this one, in the same trace.</summary>
    /// <example>
    /// <code>
    /// Span("POST /carts/{id}/orders", Span("publish OrderPlaced")).Children
    /// </code>
    /// </example>
    public IReadOnlyList<ExpectedSpan> Children { get; }

    /// <summary>Describes the expected tree as it is written, for example <c>Span(POST /carts/{id}/orders, Span(publish OrderPlaced))</c>. Failure messages use it.</summary>
    /// <example>
    /// <code>
    /// Span("POST /carts/{id}/orders", Span("publish OrderPlaced")).ToString() // Span(POST /carts/{id}/orders, Span(publish OrderPlaced))
    /// </code>
    /// </example>
    public override string ToString()
    {
        var parts = new List<string> { Name };
        if (Attributes.Count > 0)
            parts.Add(SpanTree.Describe(Attributes));
        parts.AddRange(Children.Select(child => child.ToString()));
        return $"Span({string.Join(", ", parts)})";
    }
}
