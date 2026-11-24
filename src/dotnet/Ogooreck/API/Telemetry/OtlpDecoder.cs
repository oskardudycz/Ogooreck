using System.Globalization;
using Google.Protobuf;

namespace Ogooreck.API;

internal static class OtlpDecoder
{
    private const int Delta = 1;
    private const uint DoubleValueOrHistogramCountTag = 33;
    private const uint IntegerValueTag = 49;
    private const uint NumberAttributeTag = 58;
    private const uint HistogramAttributeTag = 74;

    public static List<RecordedSpan> Spans(byte[] request) =>
        ScopesOf(request).SelectMany(scope => FieldsOf(scope, 2)).Select(Span).ToList();

    public static List<RecordedMeasurement> Measurements(byte[] request)
    {
        var measurements = new List<RecordedMeasurement>();
        foreach (var metric in ScopesOf(request).SelectMany(scope => FieldsOf(scope, 2)))
            ReadMetric(metric, measurements);
        return measurements;
    }

    private static IEnumerable<byte[]> ScopesOf(byte[] request) =>
        FieldsOf(request, 1).SelectMany(resource => FieldsOf(resource, 2));

    private static IEnumerable<byte[]> FieldsOf(byte[] message, int wanted)
    {
        var input = new CodedInputStream(message);
        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            if (WireFormat.GetTagFieldNumber(tag) == wanted)
                yield return input.ReadBytes().ToByteArray();
            else
                input.SkipLastField();
        }
    }

    private static RecordedSpan Span(byte[] message)
    {
        string traceId = "", spanId = "", name = "";
        string? parentSpanId = null;
        var attributes = new Dictionary<string, string>();
        var input = new CodedInputStream(message);
        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            switch (tag)
            {
                case 10:
                    traceId = Convert.ToHexStringLower(input.ReadBytes().Span);
                    break;
                case 18:
                    spanId = Convert.ToHexStringLower(input.ReadBytes().Span);
                    break;
                case 34:
                    var parent = input.ReadBytes();
                    parentSpanId = parent.IsEmpty ? null : Convert.ToHexStringLower(parent.Span);
                    break;
                case 42:
                    name = input.ReadString();
                    break;
                case 74:
                    AddAttribute(input.ReadBytes().ToByteArray(), attributes);
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return new RecordedSpan(traceId, spanId, parentSpanId, name, attributes);
    }

    private static void ReadMetric(byte[] message, List<RecordedMeasurement> measurements)
    {
        var name = "";
        var points = new List<RecordedMeasurement>();
        var input = new CodedInputStream(message);
        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            switch (tag)
            {
                case 10:
                    name = input.ReadString();
                    break;
                case 42:
                    ReadPoints(input.ReadBytes().ToByteArray(), pointField: 1, temporalityField: 0, isHistogram: false,
                        points);
                    break;
                case 58:
                    ReadPoints(input.ReadBytes().ToByteArray(), pointField: 1, temporalityField: 2, isHistogram: false,
                        points);
                    break;
                case 74:
                    ReadPoints(input.ReadBytes().ToByteArray(), pointField: 1, temporalityField: 2, isHistogram: true,
                        points);
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        measurements.AddRange(points.Select(point => point with { Instrument = name }));
    }

    private static void ReadPoints(byte[] data, int pointField, int temporalityField, bool isHistogram,
        List<RecordedMeasurement> points)
    {
        var pointMessages = new List<byte[]>();
        var temporality = 0;
        var input = new CodedInputStream(data);
        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            var field = WireFormat.GetTagFieldNumber(tag);
            if (field == pointField)
                pointMessages.Add(input.ReadBytes().ToByteArray());
            else if (field == temporalityField)
                temporality = input.ReadEnum();
            else
                input.SkipLastField();
        }

        var isCumulative = temporalityField == 0 || temporality != Delta;
        points.AddRange(pointMessages.Select(point => Point(point, isHistogram, isCumulative)));
    }

    private static RecordedMeasurement Point(byte[] message, bool isHistogram, bool isCumulative)
    {
        var value = 0d;
        var attributes = new Dictionary<string, string>();
        var attributeTag = isHistogram ? HistogramAttributeTag : NumberAttributeTag;
        var input = new CodedInputStream(message);
        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            switch (tag)
            {
                case DoubleValueOrHistogramCountTag:
                    value = isHistogram ? input.ReadFixed64() : input.ReadDouble();
                    break;
                case IntegerValueTag:
                    value = input.ReadSFixed64();
                    break;
                case var attribute when attribute == attributeTag:
                    AddAttribute(input.ReadBytes().ToByteArray(), attributes);
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return new RecordedMeasurement("", attributes, value, null, isCumulative);
    }

    private static void AddAttribute(byte[] keyValue, Dictionary<string, string> attributes)
    {
        var key = "";
        string? value = null;
        var input = new CodedInputStream(keyValue);
        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            if (tag == 10)
                key = input.ReadString();
            else if (tag == 18)
                value = ValueOf(input.ReadBytes().ToByteArray());
            else
                input.SkipLastField();
        }

        if (value is not null)
            attributes[key] = value;
    }

    private static string? ValueOf(byte[] anyValue)
    {
        string? value = null;
        var input = new CodedInputStream(anyValue);
        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            switch (tag)
            {
                case 10:
                    value = input.ReadString();
                    break;
                case 16:
                    value = input.ReadBool() ? "true" : "false";
                    break;
                case 24:
                    value = input.ReadInt64().ToString(CultureInfo.InvariantCulture);
                    break;
                case 33:
                    value = input.ReadDouble().ToString(CultureInfo.InvariantCulture);
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return value;
    }
}
