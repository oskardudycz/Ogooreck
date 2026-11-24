namespace Ogooreck.RabbitMQ;

internal sealed record ProbeArrival(ChannelState Channel, ulong DeliveryTag);
