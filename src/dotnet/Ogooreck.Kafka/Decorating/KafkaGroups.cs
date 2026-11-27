using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Ogooreck.Kafka;

internal static class KafkaGroups
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    public static async Task<IReadOnlyList<ConsumerGroupDescription>> Describe(IAdminClient admin)
    {
        ListConsumerGroupsResult listing;
        using (KafkaTracing.Source.StartActivity("Kafka ListConsumerGroups"))
            listing = await admin.ListConsumerGroupsAsync(new ListConsumerGroupsOptions { RequestTimeout = RequestTimeout });
        var active = listing.Valid
            .Where(group => group.State is not ConsumerGroupState.Empty)
            .Select(group => group.GroupId)
            .ToList();
        using (KafkaTracing.Source.StartActivity("Kafka DescribeConsumerGroups"))
            return (await admin.DescribeConsumerGroupsAsync(active,
                new DescribeConsumerGroupsOptions { RequestTimeout = RequestTimeout })).ConsumerGroupDescriptions;
    }

    public static bool IsRebalancing(ConsumerGroupDescription group) =>
        group.State is not ConsumerGroupState.Stable;

    public static async Task<Dictionary<TopicPartition, long>> EndOffsets(IAdminClient admin,
        IReadOnlyCollection<TopicPartition> partitions)
    {
        ListOffsetsResult listed;
        using (KafkaTracing.Source.StartActivity("Kafka ListOffsets"))
            listed = await admin.ListOffsetsAsync(
                partitions.Select(partition => new TopicPartitionOffsetSpec
                { TopicPartition = partition, OffsetSpec = OffsetSpec.Latest() }),
                new ListOffsetsOptions { RequestTimeout = RequestTimeout });
        return listed.ResultInfos.ToDictionary(info => info.TopicPartitionOffsetError.TopicPartition,
            info => info.TopicPartitionOffsetError.Offset.Value);
    }
}
