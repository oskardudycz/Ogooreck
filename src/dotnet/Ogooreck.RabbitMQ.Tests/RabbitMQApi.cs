using Microsoft.AspNetCore.Mvc.Testing;
using Ogooreck.API;
using Ogooreck.RabbitMQ;
using Ogooreck.Sample.RabbitMQ;

namespace Ogooreck.RabbitMQ.Tests;

public class RabbitMQApi: ApiSpecification<Program>
{
    public RabbitMQApi(RabbitMQBroker broker, AppSettings app, RabbitMQPlug? plug = null) : this(broker, app,
        plug ?? Messaging.RabbitMQ, new ShippingFaults(), new AppServices())
    {
    }

    private RabbitMQApi(RabbitMQBroker broker, AppSettings app, RabbitMQPlug plug, ShippingFaults faults,
        AppServices services) : base(HostFor(broker, app), spec => ((RabbitMQApi)spec).Configure(app, plug, faults, services))
    {
        Queues = app.Queues;
        Faults = faults;
        Services = services;
    }

    public QueueNames Queues { get; }

    public ShippingFaults Faults { get; }

    public AppServices Services { get; }

    private static Func<WebApplicationFactory<Program>, WebApplicationFactory<Program>> HostFor(RabbitMQBroker broker,
        AppSettings app) => factory => factory.WithWebHostBuilder(builder => builder
        .UseSetting("RabbitMQ:ConnectionString", broker.ConnectionString)
        .UseSetting("RabbitMQ:Exchange", app.Queues.Exchange)
        .UseSetting("RabbitMQ:ShippingQueue", app.Queues.ShippingQueue)
        .UseSetting("RabbitMQ:DispatchConcurrency", $"{app.DispatchConcurrency}")
        .UseSetting("RabbitMQ:ShipOrders", $"{app.ShipOrders}")
        .UseSetting("RabbitMQ:AutoAck", $"{app.AutoAck}")
        .UseSetting("RabbitMQ:ConsumeShippingOutsideTheContainer", $"{app.ConsumeShippingOutsideTheContainer}"));

    private RabbitMQApi Configure(AppSettings app, RabbitMQPlug plug, ShippingFaults faults, AppServices services)
    {
        Use(plug).Use(services).Use(new SharedFaultsPlug(faults));
        if (app.Deadline is { } deadline)
            Deadline(deadline);
        return this;
    }
}
