using System.Diagnostics;
using Ogooreck.Assertions;

namespace Ogooreck.API;

internal sealed class RecordingSource(object recorded, Func<IReadOnlyList<Observed>> observe, Func<int> count, bool hasCatchUp,
    RecordingRole role)
{
    public RecordingRole Role { get; } = role;

    public object Recorded { get; } = recorded;

    public bool HasCatchUp { get; } = hasCatchUp;

    public int Count => count();

    public static RecordingSource For<T>(Recorded<T> recorded, Func<T, Observed> read, bool hasCatchUp,
        RecordingRole role = RecordingRole.Published) =>
        new(recorded, () => recorded.Items().Select(read).ToList(), recorded.Count, hasCatchUp, role);

    public IReadOnlyList<Observed> ObservedOf(Outcome outcome)
    {
        var observed = observe().Where(item => TraceIdOf(item.Traceparent) == outcome.WhenTraceId).ToList();
        if (observed.Select(item => item.Message).OfType<UnknownMessage>().FirstOrDefault() is { } unknown)
            throw new AssertionException(
                $"{outcome.When}: a message of the When step has an unknown type: its {unknown.HeaderName} header is {unknown.TypeHeader ?? "missing"}, and the listed types are {(unknown.ListedTypes.Count == 0 ? "none" : string.Join(", ", unknown.ListedTypes.Select(type => type.Name)))}. List its type with MessageTypes(...).");
        return observed;
    }

    public IReadOnlyList<object> MessagesOf(Outcome outcome) =>
        ObservedOf(outcome).Select(observed => observed.Message).ToList();

    private static ActivityTraceId? TraceIdOf(string? traceparent) =>
        ActivityContext.TryParse(traceparent, null, out var context) ? context.TraceId : null;
}
