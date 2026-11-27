using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Ogooreck.API;

internal sealed class Diagnostics: IDisposable
{
    private static readonly ActivitySource Source = new("Ogooreck");
    private static readonly Meter Meter = new("Ogooreck");

    public static readonly Histogram<double> Runs = Meter.CreateHistogram<double>("ogooreck.run.duration", "s",
        "How long a Given/When/Then run took, from Then until its last expectation.");

    public static readonly Histogram<double> Starts = Meter.CreateHistogram<double>("ogooreck.start.duration", "s",
        "How long starting the app and its plugins took before the first step of a run that started them.");

    public static readonly Histogram<double> Steps = Meter.CreateHistogram<double>("ogooreck.step.duration", "s",
        "How long a step took, with a catch-up that wraps it.");

    public static readonly Histogram<double> CatchUps = Meter.CreateHistogram<double>("ogooreck.catch_up.duration", "s",
        "How long the catch-up after a step took.");

    public static readonly Histogram<double> Expectations = Meter.CreateHistogram<double>("ogooreck.expectation.duration", "s",
        "How long an expectation took, with its waiting.");

    private readonly Activity activity;
    private readonly Histogram<double>? duration;
    private readonly long started = Stopwatch.GetTimestamp();
    private TagList tags;

    static Diagnostics() => ActivitySource.AddActivityListener(new ActivityListener
    {
        ShouldListenTo = source => source == Source,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
    });

    private Diagnostics(Activity activity, Histogram<double>? duration, TagList tags)
    {
        this.activity = activity;
        this.duration = duration;
        this.tags = tags;
    }

    public static Diagnostics Start(string name, Histogram<double>? duration, Diagnostics? parent = null,
        string? phase = null)
    {
        var previous = Activity.Current;
        Activity.Current = null;
        var activity = Source.StartActivity(name, ActivityKind.Internal,
            parent is null ? previous?.Context ?? default : parent.activity.Context)!;
        Activity.Current = previous;
        var tags = new TagList();
        if (phase is not null)
            tags.Add("ogooreck.phase", phase);
        return new Diagnostics(activity, duration, tags);
    }

    public static IDisposable Measure(Histogram<double> duration, string phase) => new Measurement(duration, phase);

    public Diagnostics Tag(string key, string value)
    {
        activity.SetTag(key, value);
        return this;
    }

    public Diagnostics Tag(string key, int value)
    {
        activity.SetTag(key, value);
        return this;
    }

    public Diagnostics Rename(string name)
    {
        activity.DisplayName = name;
        return this;
    }

    public async Task Within(Func<Task> run)
    {
        Activity.Current = activity;
        await run();
    }

    public void Passed() => tags.Add("ogooreck.outcome", "passed");

    public void Fail(Exception exception)
    {
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.AddException(exception);
        tags.Add("ogooreck.outcome", "failed");
    }

    public void Dispose()
    {
        duration?.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, tags);
        var previous = Activity.Current;
        activity.Stop();
        Activity.Current = previous;
    }

    private sealed class Measurement(Histogram<double> duration, string phase): IDisposable
    {
        private readonly long started = Stopwatch.GetTimestamp();

        public void Dispose() =>
            duration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, new KeyValuePair<string, object?>("ogooreck.phase", phase));
    }
}
