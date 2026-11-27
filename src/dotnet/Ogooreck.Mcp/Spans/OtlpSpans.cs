using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Google.Protobuf;

namespace Ogooreck.Mcp;

internal static class OtlpSpans
{
    private const int ErrorStatus = 2;
    private static readonly string[] Kinds = ["unspecified", "internal", "server", "client", "producer", "consumer"];

    public static List<CollectedSpan> Decode(byte[] request)
    {
        var spans = new List<CollectedSpan>();
        foreach (var resourceSpans in FieldsOf(request, 1))
        {
            var resource = FieldsOf(resourceSpans, 1).Select(AttributesOf).FirstOrDefault([]);
            var service = resource.GetValueOrDefault("service.name")?.ToString() ?? "";
            var instance = resource.GetValueOrDefault("service.instance.id")?.ToString() ?? "";
            foreach (var scopeSpans in FieldsOf(resourceSpans, 2))
            {
                var scope = FieldsOf(scopeSpans, 1).Select(scope => FieldsOf(scope, 1).Select(Encoding.UTF8.GetString).FirstOrDefault(""))
                    .FirstOrDefault("");
                spans.AddRange(FieldsOf(scopeSpans, 2).Select(span => Span(span, service, instance, scope)));
            }
        }

        return spans;
    }

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

    private static Dictionary<string, JsonNode> AttributesOf(byte[] resource)
    {
        var attributes = new Dictionary<string, JsonNode>();
        foreach (var keyValue in FieldsOf(resource, 1))
            AddAttribute(keyValue, attributes);
        return attributes;
    }

    private static CollectedSpan Span(byte[] message, string service, string instance, string scope)
    {
        string traceId = "", spanId = "", name = "";
        string? parentSpanId = null, error = null;
        ulong start = 0, end = 0;
        var kind = 0;
        var attributes = new Dictionary<string, JsonNode>();
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
                    parentSpanId = Convert.ToHexStringLower(input.ReadBytes().Span);
                    break;
                case 48:
                    kind = input.ReadEnum();
                    break;
                case 42:
                    name = input.ReadString();
                    break;
                case 57:
                    start = input.ReadFixed64();
                    break;
                case 65:
                    end = input.ReadFixed64();
                    break;
                case 74:
                    AddAttribute(input.ReadBytes().ToByteArray(), attributes);
                    break;
                case 122:
                    error = ErrorOf(input.ReadBytes().ToByteArray());
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return new CollectedSpan(service, instance, scope, traceId, spanId, parentSpanId, name, Kinds[kind], FromUnixNanoseconds(start),
            FromUnixNanoseconds(end), error, attributes);
    }

    private static DateTimeOffset FromUnixNanoseconds(ulong nanoseconds) =>
        DateTimeOffset.UnixEpoch.AddTicks((long)(nanoseconds / 100));

    private static string? ErrorOf(byte[] status)
    {
        var message = "";
        var code = 0;
        var input = new CodedInputStream(status);
        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            if (tag == 18)
                message = input.ReadString();
            else
                code = input.ReadEnum();
        }

        return code == ErrorStatus ? message : null;
    }

    private static void AddAttribute(byte[] keyValue, Dictionary<string, JsonNode> attributes)
    {
        var key = "";
        JsonNode? value = null;
        var input = new CodedInputStream(keyValue);
        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            if (tag == 10)
                key = input.ReadString();
            else
                value = ValueOf(input.ReadBytes().ToByteArray());
        }

        if (value is not null)
            attributes[key] = value is JsonArray ? value : value.ToString();
    }

    private static JsonNode? ValueOf(byte[] anyValue)
    {
        JsonNode? value = null;
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
                    value = input.ReadBool();
                    break;
                case 24:
                    value = input.ReadInt64();
                    break;
                case 33:
                    var number = input.ReadDouble();
                    value = double.IsFinite(number) ? number : number.ToString(CultureInfo.InvariantCulture);
                    break;
                case 42:
                    value = new JsonArray(FieldsOf(input.ReadBytes().ToByteArray(), 1).Select(ValueOf).OfType<JsonNode>().ToArray());
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return value;
    }
}
