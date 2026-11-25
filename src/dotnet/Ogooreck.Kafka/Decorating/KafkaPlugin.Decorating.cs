using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.DependencyInjection;
using Ogooreck.API;
using Ogooreck.Assertions;

namespace Ogooreck.Kafka;

/// <summary>
/// The plugin that <see cref="KafkaPlugin.Decorating()"/> returns. It decorates the app's producer and consumer, records each message the producer sends,
/// and catches up by waiting until each consumer is back in <c>Consume</c> with its positions at the end offsets, as Java's <c>KafkaMessageBus.whenCaughtUp</c> does.
/// It works in-process only.
/// </summary>
/// <remarks>
/// It waits for the consumers the app resolves from its services and for nothing else, and it creates no topics or other resources. It works with Confluent's default consumer settings and with retries inside the loop,
/// for a consume loop that handles each message before it calls <c>Consume</c> again. A loop that hands messages to other tasks needs <see cref="CatchingUpWith"/>.
/// Before the first step it waits until each consumer has an assignment, has started fetching and its own consumer group is stable. Other groups on the broker are never looked at.
/// At the start of that step and of every catch-up round, it lists the members of the groups of the consumers it decorated.
/// It asks the broker through each decorated consumer's connection, so each request waits behind that consumer's open fetch, up to its <c>fetch.wait.max.ms</c> (500 ms by default). Set it low in the test app's consumers, for example to 10 ms, so catch-ups take milliseconds.
/// A member of such a group that isn't a decorated consumer fails the step, because it takes partitions the plugin can't wait for. The failure says to resolve that consumer from DI or to use <see cref="CatchingUpWith"/>.
/// It doesn't see consumers that use <c>Assign()</c> without a group, and a consumer that joins after a check is found by the next one.
/// An app that has no consumer is not waited for, and its producer topics are not checked.
/// Run one app per test project, for example through an assembly fixture, or give each copy of the app its own group and topic names: two copies in one group split the messages.
/// Every option returns a plugin that shares this plugin's recorder, so a shared setup defines the plugin once and a test class uses an option on the same app through <c>app.With(spec =&gt; spec.Use(...))</c>.
/// </remarks>
/// <example>
/// <code>
/// public static readonly KafkaPlugin&lt;string, string&gt; Kafka = KafkaPlugin.Decorating().MessageTypes(typeof(OrderPlaced), typeof(ShipOrder));
///
/// ExpectPublishedIn(Messaging.Kafka.Published, new OrderPlaced(cartId), new ShipOrder(cartId))
/// </code>
/// </example>
public sealed class KafkaPlugin<TKey, TValue>: IDerivedPlugin
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);

    private readonly KafkaRegistry<TKey, TValue> registry;
    private readonly KafkaReading<TKey, TValue> reading;
    private readonly Func<CancellationToken, Task>? catchingUpWith;

    internal KafkaPlugin() : this(new KafkaRegistry<TKey, TValue>(), KafkaReading<TKey, TValue>.Default, null, null)
    {
    }

    private KafkaPlugin(KafkaRegistry<TKey, TValue> registry, KafkaReading<TKey, TValue> reading,
        Func<CancellationToken, Task>? catchingUpWith, object? origin)
    {
        this.registry = registry;
        this.reading = reading;
        this.catchingUpWith = catchingUpWith;
        Origin = origin ?? this;
    }

    /// <summary>The messages the app's producer sent, for <c>ExpectPublished</c>, <c>ExpectNothingPublished</c> and <c>ExpectCausation</c>.</summary>
    /// <example>
    /// <code>
    /// ExpectPublishedIn(Messaging.Kafka.Published, new OrderPlaced(cartId))
    /// </code>
    /// </example>
    public Recorded<KafkaMessage<TKey, TValue>> Published => registry.Published;

    /// <inheritdoc />
    public object Origin { get; }

    /// <summary>Lists the message types. The type header's value matches a listed type's <c>FullName</c> first, then its <c>Name</c>.</summary>
    /// <example>
    /// <code>
    /// Messaging.Kafka.MessageTypes(typeof(OrderPlaced), typeof(ShipOrder))
    /// </code>
    /// </example>
    public KafkaPlugin<TKey, TValue> MessageTypes(params Type[] types) =>
        With(reading: reading.With(reader => reader.MessageTypes(types)));

    /// <summary>Gives the plugin's own lookup from the type header's value to a type, returning null for an unknown one.</summary>
    /// <example>
    /// <code>
    /// Messaging.Kafka.MessageTypes(name =&gt; Type.GetType($"Shop.Events.{name}"))
    /// </code>
    /// </example>
    public KafkaPlugin<TKey, TValue> MessageTypes(Func<string, Type?> lookupType) =>
        With(reading: reading.With(reader => reader.MessageTypes(lookupType)));

    /// <summary>Sets the app's own header names.</summary>
    /// <example>
    /// <code>
    /// Messaging.Kafka.Headers(new MessageHeaders { MessageType = "message-type" })
    /// </code>
    /// </example>
    public KafkaPlugin<TKey, TValue> Headers(MessageHeaders names) =>
        With(reading: reading.With(reader => reader.Headers(names)));

    /// <summary>Sets the JSON options that read the message bodies. The default is <see cref="JsonSerializerOptions.Web"/>.</summary>
    /// <example>
    /// <code>
    /// Messaging.Kafka.Json(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
    /// </code>
    /// </example>
    public KafkaPlugin<TKey, TValue> Json(JsonSerializerOptions options) =>
        With(reading: reading.With(reader => reader.Json(options)));

    /// <summary>
    /// Replaces the reader entirely: the function turns a recorded message into an <see cref="Observed"/>.
    /// Use it when the message value is neither a <see cref="string"/> nor a <c>byte[]</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// Messaging.Kafka.Read(recorded =&gt; new Observed(recorded.Message.Value, traceparent))
    /// </code>
    /// </example>
    public KafkaPlugin<TKey, TValue> Read(Func<KafkaMessage<TKey, TValue>, Observed> read) =>
        With(reading: reading with { Custom = read });

    /// <summary>
    /// Returns a plugin that waits in your own way instead of its catch-up and its check, for apps whose consumers it can't wait for.
    /// It still waits for the consumers' assignment before the first step. Calling it again adds a wait: both run, the first one first.
    /// </summary>
    /// <example>
    /// <code>
    /// spec.Use(Messaging.Kafka.CatchingUpWith(async ct =&gt; await handledOrders.WaitFor(expected, ct)))
    /// </code>
    /// </example>
    public KafkaPlugin<TKey, TValue> CatchingUpWith(Func<CancellationToken, Task> catchUp) =>
        With(catchingUpWith: catchingUpWith is null
            ? catchUp
            : async ct =>
            {
                await catchingUpWith(ct);
                await catchUp(ct);
            });

    /// <inheritdoc />
    public void Register(SpecificationPluginRegistration registration)
    {
        var run = new Run(this, registration);
        registration.Services(run.Decorate).Starting(run.Start);
        if (catchingUpWith is null)
            registration.Recording(Published, reading.Read, run.CatchUp);
        else
            registration.Recording(Published, reading.Read, catchingUpWith);
    }

    private KafkaPlugin<TKey, TValue> With(KafkaReading<TKey, TValue>? reading = null,
        Func<CancellationToken, Task>? catchingUpWith = null) =>
        new(registry, reading ?? this.reading, catchingUpWith ?? this.catchingUpWith, Origin);

    private sealed class Run(KafkaPlugin<TKey, TValue> plugin, SpecificationPluginRegistration registration)
    {
        private ConcurrentQueue<TrackedConsumer<TKey, TValue>> Consumers =>
            registration.AppServices.GetRequiredKeyedService<ConcurrentQueue<TrackedConsumer<TKey, TValue>>>(plugin.Origin);

        public void Decorate(IServiceCollection services)
        {
            var producer = IsRegistered<IProducer<TKey, TValue>>(services);
            var consumer = IsRegistered<IConsumer<TKey, TValue>>(services);
            if (!producer && !consumer)
                throw new InvalidOperationException(
                    $"KafkaPlugin.Decorating found no registration of {typeof(IProducer<TKey, TValue>)} or {typeof(IConsumer<TKey, TValue>)}: the app must register at least one of them for the plugin to decorate.");

            var consumers = new ConcurrentQueue<TrackedConsumer<TKey, TValue>>();
            services.AddKeyedSingleton(plugin.Origin, consumers);
            if (producer)
                services.Decorate<IProducer<TKey, TValue>>(inner =>
                    CallRecorder.Wrap(inner, nameof(IProducer<TKey, TValue>.ProduceAsync), Record));
            if (consumer)
                services.Decorate<IConsumer<TKey, TValue>>(inner =>
                    TrackedConsumer<TKey, TValue>.Decorate(inner, plugin.registry.Consumers, consumers));
        }

        public async Task Start(CancellationToken ct)
        {
            var own = Consumers.ToArray();
            if (own.Length == 0)
                return;

            using (var waiting = KafkaTracing.Source.StartActivity("Kafka wait for the consumers' assignment and fetching"))
                await Until(async () =>
                    {
                        foreach (var consumer in own)
                            consumer.RememberMemberId();
                        return NotAssigned(own) ?? NotFetching(own) ?? Rebalancing(own, await own[0].Ask(KafkaGroups.Describe));
                    },
                    registration.Deadline, registration.Clock.GetTimestamp(), ct, waiting);
            using (KafkaTracing.Source.StartActivity("Kafka read the end offsets"))
                foreach (var consumer in own)
                {
                    foreach (var (partition, end) in await EndOffsets(consumer))
                        consumer.Baseline[partition] = end;
                }

            if (plugin.catchingUpWith is null)
                await CheckForOutsiders(own);
        }

        public async Task CatchUp(CancellationToken ct)
        {
            var started = registration.Clock.GetTimestamp();
            var own = Consumers.ToArray();
            if (own.Length == 0)
                return;

            var limit = registration.Deadline * 0.9;
            var reading = ReadEnds(own);
            var ends = await Task.WhenAny(reading,
                Task.Delay(limit - registration.Clock.GetElapsedTime(started), registration.Clock, ct)) == reading
                ? await reading
                : null;
            ct.ThrowIfCancellationRequested();
            if (ends is null)
            {
                _ = reading.ContinueWith(task => task.Exception, TaskContinuationOptions.OnlyOnFaulted);
                throw new AssertionException(
                    $"The broker did not answer within {limit.TotalSeconds:0.#} seconds. {Report(own, CachedEnds(own))}".TrimEnd());
            }

            using (var waiting = KafkaTracing.Source.StartActivity("Kafka wait for the consumers to catch up"))
                await Until(() => Task.FromResult<string?>(Report(own, ends)), limit, started, ct, waiting);
        }

        private async Task<Dictionary<TrackedConsumer<TKey, TValue>, Dictionary<TopicPartition, long>>> ReadEnds(
            TrackedConsumer<TKey, TValue>[] own)
        {
            await CheckForOutsiders(own);
            var ends = new Dictionary<TrackedConsumer<TKey, TValue>, Dictionary<TopicPartition, long>>();
            using (KafkaTracing.Source.StartActivity("Kafka read the end offsets"))
                foreach (var consumer in own)
                    ends[consumer] = await EndOffsets(consumer);
            foreach (var consumer in own)
                foreach (var (partition, end) in ends[consumer])
                    consumer.Baseline.TryAdd(partition, end);
            return ends;
        }

        private void Record(object?[] arguments)
        {
            var topic = arguments[0] is TopicPartition partition ? partition.Topic : (string)arguments[0]!;
            plugin.registry.Published.Add(new KafkaMessage<TKey, TValue>(topic, (Message<TKey, TValue>)arguments[1]!));
        }

        private async Task CheckForOutsiders(TrackedConsumer<TKey, TValue>[] own)
        {
            using var checking = KafkaTracing.Source.StartActivity("Kafka check the consumer groups for other members");
            var known = plugin.registry.Consumers.Keys.Select(consumer => consumer.MemberId).ToHashSet();
            var groups = OwnGroups(own, await own[0].Ask(KafkaGroups.Describe));

            foreach (var group in groups)
                foreach (var member in group.Members.Where(member => !known.Contains(member.ConsumerId)))
                    throw new AssertionException(
                        $"Consumer group {group.GroupId} has a member (client {member.ClientId}, member id {member.ConsumerId}) that isn't a consumer the Kafka plugin decorated, so it takes partitions Ogooreck can't wait for. Resolve that consumer from DI, so the plugin wraps it, or configure the Kafka plugin with .CatchingUpWith(...) with your own wait.");
        }

        private static IEnumerable<ConsumerGroupDescription> OwnGroups(TrackedConsumer<TKey, TValue>[] own,
            IReadOnlyList<ConsumerGroupDescription> groups)
        {
            var ownMembers = own.Select(consumer => consumer.MemberId).ToHashSet();
            return groups.Where(group => group.Members.Any(member => ownMembers.Contains(member.ConsumerId)));
        }

        private static bool IsRegistered<TService>(IServiceCollection services) =>
            services.Any(descriptor => descriptor.ServiceType == typeof(TService) && !descriptor.IsKeyedService);

        private static string? NotAssigned(TrackedConsumer<TKey, TValue>[] own) =>
            own.Where(consumer => consumer.Inner.Assignment.Count == 0)
                .Select(consumer => $"the Kafka consumer of topics [{string.Join(", ", consumer.Topics)}] has no partitions assigned")
                .FirstOrDefault();

        private static string? NotFetching(TrackedConsumer<TKey, TValue>[] own) =>
            own.Where(consumer => consumer.Inner.Assignment.Any(partition =>
                    consumer.Inner.GetWatermarkOffsets(partition).High == Offset.Unset))
                .Select(consumer => $"the Kafka consumer of topics [{string.Join(", ", consumer.Topics)}] has not started fetching its partitions")
                .FirstOrDefault();

        private static string? Rebalancing(TrackedConsumer<TKey, TValue>[] own, IReadOnlyList<ConsumerGroupDescription> groups) =>
            OwnGroups(own, groups).Where(KafkaGroups.IsRebalancing).Select(group => $"the consumer group {group.GroupId} is still rebalancing").FirstOrDefault();

        private static Dictionary<TrackedConsumer<TKey, TValue>, Dictionary<TopicPartition, long>> CachedEnds(
            TrackedConsumer<TKey, TValue>[] own) =>
            own.ToDictionary(consumer => consumer, consumer => consumer.Inner.Assignment.ToDictionary(
                partition => partition, partition => consumer.Inner.GetWatermarkOffsets(partition).High.Value));

        private static bool IsBehind(TrackedConsumer<TKey, TValue> consumer, Dictionary<TopicPartition, long> ends) =>
            !consumer.InsideConsume || ends.Any(entry => consumer.Inner.Position(entry.Key) == Offset.Unset
                ? entry.Value != consumer.Baseline.GetValueOrDefault(entry.Key, entry.Value)
                : consumer.Inner.Position(entry.Key).Value < entry.Value);

        private static string Report(TrackedConsumer<TKey, TValue>[] own,
            Dictionary<TrackedConsumer<TKey, TValue>, Dictionary<TopicPartition, long>> ends) =>
            string.Join("; ", own.Where(consumer => IsBehind(consumer, ends[consumer])).Select(consumer =>
                $"the Kafka consumer of topics [{string.Join(", ", consumer.Topics)}] has not caught up (inside Consume: {consumer.InsideConsume}): {string.Join("; ", ends[consumer].Select(entry => Describe(consumer, entry.Key, entry.Value)))}"));

        private static string Describe(TrackedConsumer<TKey, TValue> consumer, TopicPartition partition, long end)
        {
            var position = consumer.Inner.Position(partition);
            return $"{partition.Topic} partition {partition.Partition.Value} position {(position == Offset.Unset ? "unset" : position.Value)}, end offset {end}";
        }

        private static Task<Dictionary<TopicPartition, long>> EndOffsets(TrackedConsumer<TKey, TValue> consumer) =>
            consumer.Ask(admin => KafkaGroups.EndOffsets(admin, consumer.Inner.Assignment));

        private async Task Until(Func<Task<string?>> problem, TimeSpan limit, long started, CancellationToken ct,
            Activity? waiting)
        {
            string? last = null;
            while (await problem() is { Length: > 0 } found)
            {
                if (found != last)
                    waiting?.AddEvent(new ActivityEvent(last = found));
                if (registration.Clock.GetElapsedTime(started) >= limit)
                    throw new AssertionException($"{found}, after waiting {limit.TotalSeconds:0.#} seconds.");
                await Task.Delay(PollInterval, registration.Clock, ct);
            }
        }
    }
}
