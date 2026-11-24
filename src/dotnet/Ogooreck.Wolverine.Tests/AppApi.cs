using System.Collections.Concurrent;
using Ogooreck.API;
using Wolverine;

namespace Ogooreck.Wolverine.Tests;

public class AppApi: ApiSpecification<Program>
{
    public static readonly ConcurrentQueue<Envelope> EveryPublishedMessage = PublishedMessagesMiddleware.EveryPublishedMessage;

    private static readonly TimeSpan DefaultDeadline = TimeSpan.FromSeconds(30);

    private readonly SampleApp app;

    public AppApi() : this(DefaultDeadline) { }

    private AppApi(TimeSpan deadline) : this(deadline, new SampleApp()) { }

    private AppApi(TimeSpan deadline, SampleApp app) : base(spec => ((AppApi)spec).Configure(deadline, app)) =>
        this.app = app;

    public static AppApi WithDeadline(TimeSpan deadline) => new(deadline);

    public int ConfigurationRuns { get; private set; }

    public FakeClock AppClock { get; } = new();

    public IServiceProvider Services => app.Services;

    private AppApi Configure(TimeSpan specificationDeadline, SampleApp sampleApp)
    {
        ConfigurationRuns++;
        Use(Messaging.Wolverine).Use(AppClock).Use(sampleApp).Deadline(specificationDeadline);
        return this;
    }
}
