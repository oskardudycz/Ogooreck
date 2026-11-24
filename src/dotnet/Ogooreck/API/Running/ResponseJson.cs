using System.Text.Json;

namespace Ogooreck.API;

internal static class ResponseJson
{
    public static object? Value(JsonElement json) =>
        json.ValueKind switch
        {
            JsonValueKind.Object => json.EnumerateObject().ToDictionary(field => field.Name, field => Value(field.Value)),
            JsonValueKind.Array => json.EnumerateArray().Select(Value).ToList(),
            JsonValueKind.String => json.GetString(),
            JsonValueKind.Number => json.TryGetDecimal(out var number) ? number : json.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };

    public static object? FieldsOf(JsonElement actual, JsonElement expected) =>
        (actual.ValueKind, expected.ValueKind) switch
        {
            (JsonValueKind.Object, JsonValueKind.Object) => expected.EnumerateObject().ToDictionary(
                field => field.Name,
                field => actual.TryGetProperty(field.Name, out var value) ? FieldsOf(value, field.Value) : Missing),
            (JsonValueKind.Array, JsonValueKind.Array) => actual.EnumerateArray().Select((item, index) =>
                index < expected.GetArrayLength() ? FieldsOf(item, expected[index]) : Value(item)).ToList(),
            _ => Value(actual)
        };

    private static readonly MissingField Missing = new();

    private sealed class MissingField
    {
        public override string ToString() => "no such field";
    }
}
