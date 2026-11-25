using System.ComponentModel;
using System.Text.Json;
using Ogooreck.Assertions;

namespace Ogooreck.API;

/// <summary>
/// Reads what an API returned from an <see cref="HttpResponseMessage"/>: the id of a created resource from the <c>Location</c> header, and the JSON body as an object.
/// Use them in a <c>Request</c> step to feed one response into the next request.
/// </summary>
public static class HttpResponseMessageExtensions
{
    /// <summary>
    /// Reads the id of a created resource from the last segment of the <c>Location</c> header, converted to <typeparamref name="T"/>.
    /// Returns <c>false</c> when there is no <c>Location</c> header or the segment is empty.
    /// </summary>
    /// <example>
    /// <code>
    /// response.TryGetCreatedId&lt;Guid&gt;(out var id)
    /// </code>
    /// </example>
    public static bool TryGetCreatedId<T>(this HttpResponseMessage response, out T? value)
    {
        value = default;

        var locationHeader = response.Headers.Location?.OriginalString.TrimEnd('/');

        if (string.IsNullOrEmpty(locationHeader))
            return false;

        locationHeader = locationHeader.StartsWith("/") ? locationHeader : $"/{locationHeader}";

        var start = locationHeader.LastIndexOf("/", locationHeader.Length - 1, StringComparison.Ordinal);

        var createdId = locationHeader.Substring(start + 1, locationHeader.Length - 1 - start);

        value = (T?)TypeDescriptor.GetConverter(typeof(T)).ConvertFromInvariantString(createdId);

        return true;
    }

    /// <summary>
    /// Reads the id of a created resource from the <c>Location</c> header, and throws when the response has none.
    /// </summary>
    /// <example>
    /// <code>
    /// var cartId = response.GetCreatedId&lt;Guid&gt;();
    /// </code>
    /// </example>
    public static T GetCreatedId<T>(this HttpResponseMessage response) =>
        response.TryGetCreatedId<T>(out var createdId)
            ? createdId!
            : throw new ArgumentOutOfRangeException(nameof(response.Headers.Location));

    /// <summary>
    /// Reads the id of a created resource from the <c>Location</c> header as a string.
    /// </summary>
    /// <example>
    /// <code>
    /// Request(async (client, context) => await client.PostAsync($"/carts/{context.CreatedId()}/items", content))
    /// </code>
    /// </example>
    public static string GetCreatedId(this HttpResponseMessage response) =>
        response.GetCreatedId<string>();

    /// <summary>
    /// Deserializes the JSON body into <typeparamref name="T"/>, with web defaults when you pass no <paramref name="options"/>.
    /// It can be called more than once on the same response, and fails with a clear message when the body is <c>null</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// var cart = await response.GetResultFromJson&lt;ShoppingCartDetails&gt;();
    /// </code>
    /// </example>
    public static async Task<T> GetResultFromJson<T>(
        this HttpResponseMessage response,
        JsonSerializerOptions? options = null
    )
    {
        // Scenarios may assert or extract the same body more than once. HttpContent
        // buffers text reads, unlike consuming and disposing its JSON stream.
        var json = await response.Content.ReadAsStringAsync();
        var deserialised = JsonSerializer.Deserialize<T>(json, options ?? JsonSerializerOptions.Web);
        Expect.That(deserialised is not null, "Expected a JSON response value, but found null.");
        return deserialised;
    }
}
