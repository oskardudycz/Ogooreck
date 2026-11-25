using Google.Protobuf;

namespace Ogooreck.Tests.API.Telemetry;

public static class OtlpPayloads
{
    public const int Delta = 1;
    public const int Cumulative = 2;

    public static byte[] Message(Action<CodedOutputStream> write)
    {
        using var stream = new MemoryStream();
        var output = new CodedOutputStream(stream);
        write(output);
        output.Flush();
        return stream.ToArray();
    }

    public static void Nested(this CodedOutputStream output, int field, byte[] message)
    {
        output.WriteTag(field, WireFormat.WireType.LengthDelimited);
        output.WriteBytes(ByteString.CopyFrom(message));
    }

    public static byte[] Text(string key, string value) =>
        KeyValue(key, value: Message(any => { any.WriteTag(1, WireFormat.WireType.LengthDelimited); any.WriteString(value); }));

    public static byte[] Bool(string key, bool value) =>
        KeyValue(key, Message(any => { any.WriteTag(2, WireFormat.WireType.Varint); any.WriteBool(value); }));

    public static byte[] Int(string key, long value) =>
        KeyValue(key, Message(any => { any.WriteTag(3, WireFormat.WireType.Varint); any.WriteInt64(value); }));

    public static byte[] Double(string key, double value) =>
        KeyValue(key, Message(any => { any.WriteTag(4, WireFormat.WireType.Fixed64); any.WriteDouble(value); }));

    public static byte[] Array(string key) =>
        KeyValue(key, Message(any => any.Nested(5, Message(array => array.Nested(1, Message(item =>
        {
            item.WriteTag(1, WireFormat.WireType.LengthDelimited);
            item.WriteString("ignored");
        }))))));

    private static byte[] KeyValue(string key, byte[] value) =>
        Message(output =>
        {
            output.WriteTag(1, WireFormat.WireType.LengthDelimited);
            output.WriteString(key);
            output.Nested(2, value);
        });

    public static byte[] Span(string traceId, string spanId, string? parentSpanId, string name,
        params byte[][] attributes) =>
        Message(span =>
        {
            span.WriteTag(1, WireFormat.WireType.LengthDelimited);
            span.WriteBytes(ByteString.CopyFrom(Convert.FromHexString(traceId)));
            span.WriteTag(2, WireFormat.WireType.LengthDelimited);
            span.WriteBytes(ByteString.CopyFrom(Convert.FromHexString(spanId)));
            if (parentSpanId is not null)
            {
                span.WriteTag(4, WireFormat.WireType.LengthDelimited);
                span.WriteBytes(ByteString.CopyFrom(Convert.FromHexString(parentSpanId)));
            }

            span.WriteTag(5, WireFormat.WireType.LengthDelimited);
            span.WriteString(name);
            span.WriteTag(6, WireFormat.WireType.Varint);
            span.WriteInt32(2);
            span.WriteTag(7, WireFormat.WireType.Fixed64);
            span.WriteFixed64(1);
            foreach (var attribute in attributes)
                span.Nested(9, attribute);
            span.WriteTag(15, WireFormat.WireType.Varint);
            span.WriteInt32(7);
        });

    public static byte[] TraceRequest(params byte[][] spans) =>
        Message(request => request.Nested(1, Message(resource =>
        {
            resource.Nested(1, Message(attributes => attributes.Nested(1, Text("service.name", "api"))));
            resource.Nested(2, Message(scope =>
            {
                scope.Nested(1, Message(name => { name.WriteTag(1, WireFormat.WireType.LengthDelimited); name.WriteString("lib"); }));
                foreach (var span in spans)
                    scope.Nested(2, span);
            }));
        })));

    public static byte[] NumberPoint(double value, params byte[][] attributes) =>
        Message(point =>
        {
            point.WriteTag(3, WireFormat.WireType.Fixed64);
            point.WriteFixed64(1);
            point.WriteTag(4, WireFormat.WireType.Fixed64);
            point.WriteDouble(value);
            foreach (var attribute in attributes)
                point.Nested(7, attribute);
        });

    public static byte[] IntegerPoint(long value, params byte[][] attributes) =>
        Message(point =>
        {
            point.WriteTag(6, WireFormat.WireType.Fixed64);
            point.WriteSFixed64(value);
            foreach (var attribute in attributes)
                point.Nested(7, attribute);
        });

    public static byte[] HistogramPoint(ulong count, params byte[][] attributes) =>
        Message(point =>
        {
            point.WriteTag(4, WireFormat.WireType.Fixed64);
            point.WriteFixed64(count);
            point.WriteTag(5, WireFormat.WireType.Fixed64);
            point.WriteDouble(99.5);
            point.Nested(6, Message(buckets => { buckets.WriteFixed64(2); buckets.WriteFixed64(3); }));
            point.Nested(7, Message(bounds => bounds.WriteDouble(5)));
            foreach (var attribute in attributes)
                point.Nested(9, attribute);
        });

    public static byte[] Sum(string name, int temporality, params byte[][] points) =>
        Metric(name, 7, Message(sum =>
        {
            foreach (var point in points)
                sum.Nested(1, point);
            sum.WriteTag(2, WireFormat.WireType.Varint);
            sum.WriteInt32(temporality);
            sum.WriteTag(3, WireFormat.WireType.Varint);
            sum.WriteBool(true);
        }));

    public static byte[] Gauge(string name, params byte[][] points) =>
        Metric(name, 5, Message(gauge =>
        {
            foreach (var point in points)
                gauge.Nested(1, point);
        }));

    public static byte[] Histogram(string name, int temporality, params byte[][] points) =>
        Metric(name, 9, Message(histogram =>
        {
            foreach (var point in points)
                histogram.Nested(1, point);
            histogram.WriteTag(2, WireFormat.WireType.Varint);
            histogram.WriteInt32(temporality);
        }));

    public static byte[] Metric(string name, int dataField, byte[] data) =>
        Message(metric =>
        {
            metric.WriteTag(1, WireFormat.WireType.LengthDelimited);
            metric.WriteString(name);
            metric.WriteTag(3, WireFormat.WireType.LengthDelimited);
            metric.WriteString("ms");
            metric.Nested(dataField, data);
        });

    public static byte[] MetricsRequest(params byte[][] metrics) =>
        Message(request => request.Nested(1, Message(resource => resource.Nested(2, Message(scope =>
        {
            foreach (var metric in metrics)
                scope.Nested(2, metric);
        })))));
}
