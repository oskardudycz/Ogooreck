using Microsoft.AspNetCore.Mvc.Testing;
using Ogooreck.API;
using Ogooreck.RabbitMQ;
using Ogooreck.Sample.RabbitMQ.Shipping;

namespace Ogooreck.RabbitMQ.Tests.Fixtures;

public class RabbitMQApi: ApiSpecification<Program>
{
    public RabbitMQApi(RabbitMQBroker broker, AppSettings app, RabbitMQPlugin? plugin = null) : this(broker, app,
        plugin ?? Messaging.RabbitMQ, new ShippingFaults(), new AppServices())
    {
    }

    private RabbitMQApi(RabbitMQBroker broker, AppSettings app, RabbitMQPlugin plugin, ShippingFaults faults,
        AppServices services) : base(HostFor(broker, app), spec => ((RabbitMQApi)spec).Configure(app, plugin, faults, services))
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

    private RabbitMQApi Configure(AppSettings app, RabbitMQPlugin plugin, ShippingFaults faults, AppServices services)
    {
        Use(plugin).Use(services).Use(new SharedFaultsPlugin(faults));
        if (app.Deadline is { } deadline)
            Deadline(deadline);
        return this;
    }
}
