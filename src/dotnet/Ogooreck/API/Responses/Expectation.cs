using System.Diagnostics;
using System.Text.Json;

namespace Ogooreck.API;

/// <summary>A check that <c>Then</c> runs after the When step.</summary>
/// <example>
/// <code>
/// .Then([ExpectResponse(OK), ExpectPublished(new OrderPlaced(cartId))], ct)
/// </code>
/// </example>
public sealed class Expectation
{
    internal Expectation(Func<Outcome, Task> verify, bool checksResponse = false)
    {
        Verify = verify;
        ChecksResponse = checksResponse;
    }

    internal Func<Outcome, Task> Verify { get; }

    internal bool ChecksResponse { get; }
}

/// <summary>A check of the When step's response, given to <c>ExpectResponse</c>.</summary>
/// <example>
/// <code>
/// ExpectResponse(OK, Body(new { status = "Placed" }), ETag(WeakETag(2)))
/// </code>
/// </example>
public sealed class ResponseExpectation
{
    internal ResponseExpectation(Func<HttpResponseMessage, Outcome, Task> verify) => Verify = verify;

    internal Func<HttpResponseMessage, Outcome, Task> Verify { get; }
}

internal sealed record Outcome(HttpResponseMessage? Response, string When, string WhenKind, JsonSerializerOptions Json,
    ActivityTraceId WhenTraceId, IReadOnlyList<RecordingSource> Recordings, TestTelemetry Telemetry, TimeSpan Deadline,
    TimeProvider Clock, CancellationToken CancellationToken);
