using Microsoft.Extensions.DependencyInjection;
using Ogooreck.Assertions;
using Ogooreck.Sample.Kafka;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Kafka.Tests.ShopRequests;

namespace Ogooreck.Kafka.Tests;

public class StuckRebalanceTests(KafkaBroker broker): IAsyncDisposable
{
    private readonly Guid cartId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;
    private readonly List<KafkaApi> apps = [];

    public async ValueTask DisposeAsync()
    {
        foreach (var app in apps)
            await app.DisposeAsync();
    }

    [Fact]
    public async Task AMemberThatDoesNotPoll_LeavesAnotherMemberOfTheGroupWithoutAnAssignment_FailingItsFirstStep()
    {
        var group = await GroupWithAMemberThatDoesNotPoll();

        var error = await FirstStepOf(group);

        Assert.Contains($"consumer of topics [{group.Topic}] has no partitions assigned", error.Message);
    }

    private async Task<AppSettings> GroupWithAMemberThatDoesNotPoll()
    {
        var group = AppSettings.Own(await broker.NewTopic());
        var services = new AppServicesPlug();
        var blocked = Track(new KafkaApi(broker, group with { BlockHandler = true },
            Messaging.Kafka.CatchingUpWith(_ => Task.CompletedTask), services));
        await blocked.Given().When(PlaceOrderToShip(cartId)).Then([ExpectResponse(NoContent)], ct);
        var faults = services.Services.GetRequiredService<ShippingFaults>();
        await faults.HandlerBlocked.WaitAsync(ct);
        return group;
    }

    private async Task<AssertionException> FirstStepOf(AppSettings group)
    {
        var joining = Track(new KafkaApi(broker, group with { Deadline = TimeSpan.FromSeconds(2), BlockHandler = false },
            Messaging.Kafka));
        return await Assert.ThrowsAsync<AssertionException>(() =>
            joining.Given().When(GetCart(cartId)).Then([ExpectResponse(OK)], ct));
    }

    private KafkaApi Track(KafkaApi app)
    {
        apps.Add(app);
        return app;
    }
}
