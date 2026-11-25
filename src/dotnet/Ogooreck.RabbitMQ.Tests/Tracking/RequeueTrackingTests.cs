using Microsoft.Extensions.DependencyInjection;
using Ogooreck.API;
using Ogooreck.Assertions;
using Ogooreck.RabbitMQ.Tests.Fixtures;
using Ogooreck.Testing;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.RabbitMQ.Tests.Fixtures.ShopRequests;

namespace Ogooreck.RabbitMQ.Tests.Tracking;

public class RequeueTrackingTests(RabbitMQBroker broker)
{
    private readonly Guid orderId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task NackOfSeveralDeliveriesWithRequeue_CountsEachOfThemPending()
    {
        var clock = new PassingLaterClock();
        await using var app = new RabbitMQApi(broker, AppSettings.Own() with { Deadline = TimeSpan.FromSeconds(2) });
        await using var shop = app.With(spec => spec.Clock(clock));
        using var release = new CancellationTokenSource();

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            shop.Given().When(NackThreeTogetherThenBlockOnTheRedelivery(app, clock, release.Token)).Then([ExpectResponse(NotFound)], ct));
        await release.CancelAsync();

        Assert.Contains("Handlers running: 1. Requeues pending: 2.", error.Message);
    }

    [Fact]
    public async Task FailedNackWithRequeue_LeavesNoRequeuePending()
    {
        await using var shop = new RabbitMQApi(broker, AppSettings.Own());

        await shop.Given().When(NackOnAClosedChannel(shop)).Then([ExpectResponse(NotFound)], ct);
    }

    private TestStep NackThreeTogetherThenBlockOnTheRedelivery(RabbitMQApi shop, PassingLaterClock clock,
        CancellationToken release) =>
        Request(async client =>
        {
            var connection = shop.Services.Provider.GetRequiredService<IConnection>();
            var channel = await connection.CreateChannelAsync(cancellationToken: ct);
            var queue = await channel.QueueDeclareAsync(durable: true, exclusive: false, autoDelete: true,
                cancellationToken: ct);
            var delivered = 0;
            var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, delivery) =>
            {
                if (delivery.Redelivered)
                {
                    blocked.TrySetResult();
                    await Task.Delay(Timeout.Infinite, release).ContinueWith(_ => { }, CancellationToken.None);
                }
                else if (++delivered == 3)
                    await channel.BasicNackAsync(delivery.DeliveryTag, true, true, ct);
            };
            for (var index = 0; index < 3; index++)
                await channel.BasicPublishAsync(string.Empty, queue.QueueName, "{}"u8.ToArray(), ct);
            await channel.BasicConsumeAsync(queue.QueueName, false, consumer, ct);
            await blocked.Task.WaitAsync(ct);
            clock.PassFromNow();
            return await client.GetAsync($"/orders/{orderId}", ct);
        });

    private TestStep NackOnAClosedChannel(RabbitMQApi shop) =>
        Request(async client =>
        {
            var connection = shop.Services.Provider.GetRequiredService<IConnection>();
            var channel = await connection.CreateChannelAsync(cancellationToken: ct);
            var queue = await channel.QueueDeclareAsync(durable: true, exclusive: false, autoDelete: false,
                cancellationToken: ct);
            var delivered = new TaskCompletionSource<ulong>();
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (_, delivery) =>
            {
                delivered.SetResult(delivery.DeliveryTag);
                return Task.CompletedTask;
            };
            await channel.BasicConsumeAsync(queue.QueueName, false, consumer, ct);
            await channel.BasicPublishAsync(string.Empty, queue.QueueName, "{}"u8.ToArray(), ct);
            var deliveryTag = await delivered.Task.WaitAsync(ct);
            await channel.CloseAsync(ct);
            await Assert.ThrowsAnyAsync<Exception>(() => channel.BasicNackAsync(deliveryTag, false, true, ct).AsTask());
            var other = await connection.CreateChannelAsync(cancellationToken: ct);
            var requeued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var alive = new AsyncEventingBasicConsumer(other);
            alive.ReceivedAsync += (_, _) =>
            {
                requeued.TrySetResult();
                return Task.CompletedTask;
            };
            await other.BasicConsumeAsync(queue.QueueName, true, alive, ct);
            await requeued.Task.WaitAsync(ct);
            return await client.GetAsync($"/orders/{orderId}", ct);
        });
}
