using System.Net;
using Ogooreck.Assertions;

namespace Ogooreck.API;

/// <summary>What a step can read from the steps that ran before it.</summary>
public sealed class StepContext
{
    internal StepContext(IReadOnlyList<HttpResponseMessage> responses, string step, string traceparent,
        Func<PublishRequest, CancellationToken, Task>? publishing, CancellationToken cancellationToken)
    {
        Responses = responses;
        Step = step;
        Traceparent = traceparent;
        Publishing = publishing;
        CancellationToken = cancellationToken;
    }

    internal string Step { get; }

    internal Func<PublishRequest, CancellationToken, Task>? Publishing { get; }

    /// <summary>The step's W3C <c>traceparent</c>, with a trace id unique to this step. Pass it to the app so its messages are attributed to the step.</summary>
    /// <example>
    /// <code>
    /// Action(ctx =&gt; { Items.Add(new Observed(message, ctx.Traceparent)); return Task.CompletedTask; })
    /// </code>
    /// </example>
    public string Traceparent { get; }

    /// <summary>The responses of the earlier steps, in step order.</summary>
    /// <example>
    /// <code>
    /// Request((client, ctx) =&gt; client.GetAsync(ctx.Responses[0].Headers.Location, ct))
    /// </code>
    /// </example>
    public IReadOnlyList<HttpResponseMessage> Responses { get; }

    /// <summary>The token given to <c>Then</c>. A step that waits should stop when it is cancelled.</summary>
    /// <example>
    /// <code>
    /// Action(ctx =&gt; Task.Delay(TimeSpan.FromSeconds(1), ctx.CancellationToken))
    /// </code>
    /// </example>
    public CancellationToken CancellationToken { get; }

    /// <summary>The id from the <c>Location</c> header of the first earlier <c>201 Created</c> response.</summary>
    /// <example>
    /// <code>
    /// Request((client, ctx) =&gt; client.GetAsync($"/carts/{ctx.CreatedId()}", ct))
    /// </code>
    /// </example>
    public string CreatedId() => CreatedId<string>();

    /// <summary>The id from the <c>Location</c> header of the first earlier <c>201 Created</c> response, converted to <typeparamref name="T"/>.</summary>
    /// <example>
    /// <code>
    /// Request((client, ctx) =&gt; client.GetAsync($"/api/products/{ctx.CreatedId&lt;Guid&gt;()}", ct))
    /// </code>
    /// </example>
    public T CreatedId<T>()
    {
        var index = Responses.ToList().FindIndex(response => response.StatusCode == HttpStatusCode.Created);
        if (index < 0)
            throw new AssertionException(
                $"{Step}: no created response was found among the {Responses.Count} earlier responses, so there is no created id. Earlier responses: {string.Join("; ", Responses.Select(Describe))}.");

        var created = Responses[index];
        var location = created.Headers.Location?.OriginalString
                       ?? throw new AssertionException(
                           $"{Step}: the first 201 Created response ({Describe(created, index)}) has no Location header, so there is no created id.");
        try
        {
            return created.GetCreatedId<T>();
        }
        catch (Exception exception)
        {
            throw new AssertionException(
                $"{Step}: the first 201 Created response ({Describe(created, index)}) has Location <{location}>, whose id can't be read as {typeof(T).Name}: {exception.Message}",
                exception);
        }
    }

    private static string Describe(HttpResponseMessage response, int index) =>
        $"response {index + 1}: {ApiSpecification.Describe(response.StatusCode)} from {ApiSpecification.Describe(response.RequestMessage)}";
}
