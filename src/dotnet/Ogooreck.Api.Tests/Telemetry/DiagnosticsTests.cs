using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Ogooreck.API;
using Ogooreck.Assertions;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.Api.Tests.ShopRequests;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests.Telemetry;

public class DiagnosticsTests(ApiSpecification<Program> api): IClassFixture<ApiSpecification<Program>>, IDisposable
{
    private static readonly ActivitySource TestSource = new("Ogooreck.Api.Tests.DiagnosticsTests");

    private readonly Guid cartId = Guid.NewGuid();
    private readonly OgooreckSpans spans = new();

    [Fact]
    public async Task Run_IsTracedAsGivenWhenThen_WithEachCatchUpUnderItsStep()
    {
        await using var app = For<Program>();
        await app.Given().When(GetCart(Guid.NewGuid())).Then([ExpectError(NotFound)], TestContext.Current.CancellationToken);

        await app.Given(AddProduct(cartId, "socks"))
            .When(GetCart(cartId))
            .Then([ExpectResponse(OK)], TestContext.Current.CancellationToken);

        var run = RunOf($"When request GET /carts/{cartId}");
        Assert.Equal("Ogooreck Given/When/Then", run.DisplayName);
        Assert.Equal(["Given", $"When request GET /carts/{cartId}", "Then"], NamesUnder(run));
        Assert.Equal([$"Given step 1 request POST /carts/{cartId}/products"], NamesUnder(Named("Given", run)));
        Assert.Equal(["catch-up"], NamesUnder(Named($"Given step 1 request POST /carts/{cartId}/products", run)));
        Assert.Equal(["catch-up"], NamesUnder(Named($"When request GET /carts/{cartId}", run)));
        Assert.Equal(["expectation 1"], NamesUnder(Named("Then", run)));
        Assert.Equal(ActivityStatusCode.Unset, run.Status);
    }

    [Fact]
    public async Task FirstRunOnAnApp_StartsItInBefore()
    {
        await using var app = For<Program>().Use(new TracingPlug());

        await app.Given().When(GetCart(cartId)).Then([ExpectError(NotFound)], TestContext.Current.CancellationToken);

        var run = RunOf($"When request GET /carts/{cartId}");
        Assert.Equal(["Before", $"When request GET /carts/{cartId}", "Then"], NamesUnder(run));
        Assert.Equal(["start app", "start plug TracingPlug"], NamesUnder(Named("Before", run)));
    }

    [Fact]
    public async Task FailedRun_IsMarkedAsAnErrorWithTheFailure()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(GetCart(cartId)).Then([ExpectResponse(OK)], TestContext.Current.CancellationToken));

        var run = RunOf($"When request GET /carts/{cartId}");
        Assert.Equal(ActivityStatusCode.Error, run.Status);
        Assert.Equal(error.Message, run.StatusDescription);
    }

    [Fact]
    public async Task Run_NamesTheTestThatCalledWhen()
    {
        var whenLine = CurrentLine() + 1;
        await api.Given().When(GetCart(cartId)).Then([ExpectError(NotFound)], TestContext.Current.CancellationToken);

        var run = RunOf($"When request GET /carts/{cartId}");
        Assert.Equal($"{typeof(DiagnosticsTests).FullName}.{nameof(Run_NamesTheTestThatCalledWhen)}", run.GetTagItem("code.function.name"));
        Assert.Equal(CurrentFile(), run.GetTagItem("code.file.path"));
        Assert.Equal(whenLine, run.GetTagItem("code.line.number"));
    }

    [Fact]
    public async Task Run_NestsUnderTheSpanOfTheTest()
    {
        using var testSpans = new ActivityListener
        {
            ShouldListenTo = source => source == TestSource,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(testSpans);
        using var test = TestSource.StartActivity(nameof(Run_NestsUnderTheSpanOfTheTest))!;

        await api.Given().When(GetCart(cartId)).Then([ExpectError(NotFound)], TestContext.Current.CancellationToken);

        var run = RunOf($"When request GET /carts/{cartId}");
        Assert.Equal(test.TraceId, run.TraceId);
        Assert.Equal(test.SpanId, run.ParentSpanId);
    }

    [Fact]
    public async Task Run_WithoutASpanOfTheTest_StartsItsOwnTrace()
    {
        await api.Given().When(GetCart(cartId)).Then([ExpectError(NotFound)], TestContext.Current.CancellationToken);

        Assert.Equal(default, RunOf($"When request GET /carts/{cartId}").ParentSpanId);
    }

    [Fact]
    public async Task FailedRun_RecordsTheFailureAsAnExceptionEvent()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(GetCart(cartId)).Then([ExpectResponse(OK)], TestContext.Current.CancellationToken));

        var exception = Assert.Single(RunOf($"When request GET /carts/{cartId}").Events);
        Assert.Equal("exception", exception.Name);
        Assert.Equal(typeof(AssertionException).FullName, exception.Tags.Single(tag => tag.Key == "exception.type").Value);
        Assert.Equal(error.Message, exception.Tags.Single(tag => tag.Key == "exception.message").Value);
        Assert.StartsWith($"{typeof(AssertionException).FullName}: {error.Message}",
            (string)exception.Tags.Single(tag => tag.Key == "exception.stacktrace").Value!);
    }

    [Fact]
    public async Task RunFailingInTheCatchUpAfterTheWhenStep_IsMarkedAsAnError()
    {
        await using var spec = api.With(setup => setup.CatchUp(_ => throw new InvalidOperationException("stuck")));

        await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(GetCart(cartId)).Then([], TestContext.Current.CancellationToken));

        Assert.Equal(ActivityStatusCode.Error, RunOf($"When request GET /carts/{cartId}").Status);
    }

    [Fact]
    public async Task SpansOfAPlug_NestUnderItsStartAndItsCatchUp()
    {
        await using var app = For<Program>().Use(new TracingPlug());

        await app.Given().When(GetCart(cartId)).Then([ExpectError(NotFound)], TestContext.Current.CancellationToken);

        var run = RunOf($"When request GET /carts/{cartId}");
        Assert.Equal(["tracing plug started"], NamesUnder(Named("start plug TracingPlug", run)));
        Assert.Equal(["tracing plug caught up"], NamesUnder(Named("catch-up", run)));
    }

    [Fact]
    public void Durations_ArePublishedAsHistogramsOfOgooreck()
    {
        var instruments = new ConcurrentBag<string>();
        using var meters = new MeterListener
        {
            InstrumentPublished = (instrument, _) =>
            {
                if (instrument.Meter.Name == "Ogooreck")
                    instruments.Add(instrument.Name);
            }
        };

        meters.Start();

        Assert.Equal(
            ["ogooreck.catch_up.duration", "ogooreck.expectation.duration", "ogooreck.run.duration", "ogooreck.start.duration", "ogooreck.step.duration"],
            instruments.Order());
    }

    public void Dispose() => spans.Dispose();

    private Activity RunOf(string whenStep)
    {
        var when = spans.Stopped.Single(span => span.DisplayName == whenStep);
        return spans.Stopped.Single(span => span.SpanId == when.ParentSpanId);
    }

    private static int CurrentLine([CallerLineNumber] int line = 0) => line;

    private static string CurrentFile([CallerFilePath] string path = "") => path;

    private Activity Named(string name, Activity run) =>
        spans.Stopped.Single(span => span.TraceId == run.TraceId && span.DisplayName == name);

    private string[] NamesUnder(Activity parent) =>
        spans.Stopped.Where(span => span.ParentSpanId == parent.SpanId)
            .OrderBy(span => span.StartTimeUtc)
            .Select(span => span.DisplayName)
            .ToArray();

    private sealed class TracingPlug: IPlug
    {
        private static readonly ActivitySource Source = new("Ogooreck");

        public void Plug(PlugRegistration registration) =>
            registration
                .Starting(_ => Traced("tracing plug started"))
                .CatchUp(_ => Traced("tracing plug caught up"));

        private static Task Traced(string name)
        {
            using (Source.StartActivity(name))
                return Task.CompletedTask;
        }
    }

    private sealed class OgooreckSpans: IDisposable
    {
        private readonly ActivityListener listener;

        public OgooreckSpans()
        {
            listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Ogooreck",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = Stopped.Enqueue
            };
            ActivitySource.AddActivityListener(listener);
        }

        public ConcurrentQueue<Activity> Stopped { get; } = new();

        public void Dispose() => listener.Dispose();
    }
}
