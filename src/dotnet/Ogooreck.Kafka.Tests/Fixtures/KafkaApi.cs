using Microsoft.AspNetCore.Mvc.Testing;
using Ogooreck.API;

namespace Ogooreck.Kafka.Tests.Fixtures;

public class KafkaApi: ApiSpecification<Program>
{
    public KafkaApi(KafkaBroker broker, AppSettings app, params ISpecificationPlugin[] plugins) : base(HostFor(broker, app),
        spec => ((KafkaApi)spec).Configure(app, plugins))
    {
    }

    private static Func<WebApplicationFactory<Program>, WebApplicationFactory<Program>> HostFor(KafkaBroker broker,
        AppSettings app) => factory => factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Kafka:BootstrapServers", broker.BootstrapServers)
            .UseSetting("Kafka:Topic", app.Topic)
            .UseSetting("Kafka:ShippingGroupId", app.ShippingGroupId)
            .UseSetting("Kafka:Consumer:fetch.wait.max.ms", "10")
            .UseSetting("Shipping:Enabled", $"{app.Shipping}")
            .UseSetting("Shipping:BlockHandler", $"{app.BlockHandler}");
        if (app.BillingGroupId is not null)
            builder.UseSetting("Billing:Enabled", "true").UseSetting("Kafka:BillingGroupId", app.BillingGroupId);
    });

    private KafkaApi Configure(AppSettings app, ISpecificationPlugin[] plugins)
    {
        Deadline(app.Deadline ?? TimeSpan.FromSeconds(30));
        foreach (var plugin in plugins)
            Use(plugin);
        return this;
    }
}
