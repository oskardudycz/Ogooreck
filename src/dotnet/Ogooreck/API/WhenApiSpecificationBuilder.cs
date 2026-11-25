using System.Diagnostics;
using System.Runtime.CompilerServices;
using Ogooreck.Assertions;

namespace Ogooreck.API;

/// <summary>The Given steps and the When step of a scenario.</summary>
public sealed class WhenApiSpecificationBuilder
{
    private const string WhenPhase = "When";

    private readonly Lazy<StartedHost> host;
    private readonly SpecificationSettings settings;
    private readonly TestStep[] given;
    private readonly TestStep when;
    private readonly TestCaller caller;

    internal WhenApiSpecificationBuilder(Lazy<StartedHost> host, SpecificationSettings settings, TestStep[] given,
        TestStep when, TestCaller caller)
    {
        this.host = host;
        this.settings = settings;
        this.given = given;
        this.when = when;
        this.caller = caller;
    }

    /// <summary>Runs the steps, then the expectations in order. The first failing expectation fails the test.</summary>
    /// <example>
    /// <code>
    /// await api.Given(PlaceOrder(cartId)).When(GetCart(cartId)).Then(ExpectResponse(OK));
    /// </code>
    /// </example>
    public Task Then(params Expectation[] expectations) => Then(expectations, CancellationToken.None);

    /// <summary>Runs the steps, then the expectations in order. Cancelling <paramref name="ct"/> ends with <see cref="OperationCanceledException"/>.</summary>
    /// <example>
    /// <code>
    /// await api.Given(PlaceOrder(cartId)).When(GetCart(cartId)).Then([ExpectResponse(OK)], ct);
    /// </code>
    /// </example>
    public async Task Then(Expectation[] expectations, CancellationToken ct)
    {
        using var run = Diagnostics.Start("Ogooreck Given/When/Then", Diagnostics.Runs)
            .Tag("code.function.name", caller.Test)
            .Tag("code.file.path", caller.File)
            .Tag("code.line.number", caller.Line);
        SpecificationSettings.Running = settings;
        try
        {
            await Then(expectations, run, ct);
            run.Passed();
        }
        catch (Exception exception)
        {
            run.Fail(exception);
            throw;
        }
    }

    private async Task Then(Expectation[] expectations, Diagnostics run, CancellationToken ct)
    {
        for (var index = 0; index < given.Length; index++)
            if (given[index] is EventuallyStep)
                throw new AssertionException(
                    $"Given step {index + 1} {given[index].Kind}: Eventually(...) is allowed only as the When step; use the request without Eventually in Given.");

        var responses = new List<HttpResponseMessage>();
        var telemetry = new TestTelemetry(settings);
        var (firstPhase, firstStep) = given.Length > 0 ? ("Given step 1", given[0]) : (WhenPhase, when);
        ThrowIfCancelledBefore(firstPhase, ct);
        var started = await Before(run, $"{firstPhase} {firstStep.Kind}", ct);

        if (given.Length > 0)
        {
            using var givenSpan = Diagnostics.Start("Given", null, run);
            for (var index = 0; index < given.Length; index++)
            {
                var step = given[index];
                var (response, where, _) = await Run(step, $"Given step {index + 1}", started, responses, telemetry, [], givenSpan, ct);

                if (response is not null && !step.MayFail && (int)response.StatusCode >= 400)
                    throw new AssertionException(
                        $"{where} failed with {ApiSpecification.Describe(response.StatusCode)}, but a Given request must succeed; wrap it in Attempt(...) if it may fail. Body: {await response.Content.ReadAsStringAsync(ct)}");
            }
        }

        await telemetry.TakeBaseline(ct);

        var eventually = when is EventuallyStep;
        var responseExpectations = eventually ? expectations.Where(expectation => expectation.ChecksResponse).ToArray() : [];
        var (whenResponse, whenWhere, whenTraceId) = await Run(when, WhenPhase, started, responses, telemetry, responseExpectations, run, ct);
        var outcome = new Outcome(whenResponse, whenWhere, when.Kind, settings.Json ?? started.Json, whenTraceId,
            settings.Recordings.Items, telemetry, settings.Deadline, settings.Clock, ct);

        using var then = Diagnostics.Start("Then", null, run);
        for (var index = 0; index < expectations.Length; index++)
        {
            if (ct.IsCancellationRequested)
                throw new OperationCanceledException(
                    $"Then was cancelled before expectation {index + 1} of {expectations.Length} ran.", ct);
            if (eventually && expectations[index].ChecksResponse)
                continue;
            using (Diagnostics.Start($"expectation {index + 1}", Diagnostics.Expectations, then))
                await expectations[index].Verify(outcome);
        }
    }

    private async Task<StartedHost> Before(Diagnostics run, string firstStep, CancellationToken ct)
    {
        if (host.IsValueCreated && settings.Started)
            return host.Value;

        using var before = Diagnostics.Start("Before", Diagnostics.Starts, run);
        StartedHost started;
        if (host.IsValueCreated)
            started = host.Value;
        else
            using (Diagnostics.Start("start app", null, before))
                started = host.Value;
        await settings.EnsureStarted(firstStep, before, ct);
        return started;
    }

    private static void ThrowIfCancelledBefore(string phase, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            throw new OperationCanceledException(
                $"Then was cancelled before {(phase == WhenPhase ? "the When step" : phase)} started.", ct);
    }

    private async Task<(HttpResponseMessage? Response, string Where, ActivityTraceId TraceId)> Run(TestStep step,
        string phase, StartedHost started, List<HttpResponseMessage> responses, TestTelemetry telemetry,
        Expectation[] responseExpectations, Diagnostics parent, CancellationToken ct)
    {
        ThrowIfCancelledBefore(phase, ct);

        var phaseTag = phase == WhenPhase ? "when" : "given";

        var traceId = default(ActivityTraceId);
        HttpRequestMessage? lastRequest = null;
        HttpResponseMessage? response = null;
        string Where() => WhereOf(phase, step, lastRequest);
        using var catchUp = new StepCatchUp(settings, ct);

        async Task<HttpResponseMessage?> Send(CancellationToken token)
        {
            traceId = ActivityTraceId.CreateRandom();
            telemetry.Add(traceId);
            var traceparent = $"00-{traceId}-{ActivitySpanId.CreateRandom()}-01";
            var (client, handler) = started.CreateStepClient(traceparent, token);
            using (client)
            {
                try
                {
                    return await step.Run(client,
                        new StepContext(responses.ToArray(), $"{phase} {step.Kind}", traceparent, settings.Publishing,
                            token),
                        token);
                }
                finally
                {
                    lastRequest = handler.LastRequest;
                }
            }
        }

        async Task<HttpResponseMessage?> SendEventually(CancellationToken token)
        {
            var firstAttempt = settings.Clock.GetTimestamp();
            for (var attempt = 1; ; attempt++)
            {
                var sent = (await Send(token))!;
                var attemptOutcome = new Outcome(sent, Where(), step.Kind, settings.Json ?? started.Json, traceId,
                    settings.Recordings.Items, telemetry, settings.Deadline, settings.Clock, ct);
                var failure = await ResponseFailureOf(responseExpectations, attemptOutcome);
                if (failure is null)
                    return sent;
                token.ThrowIfCancellationRequested();
                if (settings.Clock.GetElapsedTime(firstAttempt) >= settings.Deadline)
                    throw new AssertionException(
                        $"{Where()}: the response expectations still failed when the deadline of {settings.Deadline} passed, after {attempt} attempts. Last response: {ApiSpecification.Describe(sent.StatusCode)}. Body: {await sent.Content.ReadAsStringAsync(token)}. The last failure: {failure.Message}",
                        failure);
                await Task.Delay(Waiting.PollInterval, settings.Clock, token);
            }
        }

        try
        {
            using var span = Diagnostics.Start(phase, null, parent, phaseTag);
            using (Diagnostics.Measure(Diagnostics.Steps, phaseTag))
                try
                {
                    await catchUp.RunStep(async token =>
                        response = step is EventuallyStep ? await SendEventually(token) : await Send(token), Where);
                }
                finally
                {
                    span.Rename(Where()).Tag("ogooreck.step.trace_id", traceId.ToString());
                }

            if (response is not null)
                responses.Add(response);

            using (var catchingUp = Diagnostics.Start("catch-up", Diagnostics.CatchUps, span, phaseTag))
                await catchingUp.Within(() => catchUp.AfterStep(Where()));
            return (response, Where(), traceId);
        }
        catch (Exception exception) when (ct.IsCancellationRequested)
        {
            var message = catchUp.StepFinished
                ? $"Then was cancelled while waiting for catch-up after the {Where()}."
                : $"Then was cancelled while the {Where()} was running.";
            if (exception is not OperationCanceledException)
                message += $" The step also failed: {exception.Message}";
            throw new OperationCanceledException(message, exception, ct);
        }
    }

    private static async Task<Exception?> ResponseFailureOf(Expectation[] responseExpectations, Outcome outcome)
    {
        foreach (var expectation in responseExpectations)
        {
            try
            {
                await expectation.Verify(outcome);
            }
            catch (Exception exception) when (!outcome.CancellationToken.IsCancellationRequested)
            {
                return exception;
            }
        }

        return null;
    }

    private static string WhereOf(string phase, TestStep step, HttpRequestMessage? request) =>
        request is null ? $"{phase} {step.Kind}" : $"{phase} {step.Kind} {ApiSpecification.Describe(request)}";
}

internal readonly record struct TestCaller(string Test, string File, int Line)
{
    public static TestCaller Of(StackFrame caller, string method, string file, int line)
    {
        var type = caller.GetMethod()?.DeclaringType;
        while (type is { DeclaringType: not null } && type.IsDefined(typeof(CompilerGeneratedAttribute), false))
            type = type.DeclaringType;
        return new(type is null ? method : $"{type.FullName}.{method}", file, line);
    }
}
