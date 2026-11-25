using System.Text;
using System.Text.Json;
using Ogooreck.Assertions;

namespace Ogooreck.API;

public partial class ApiSpecification
{
    /// <summary>
    /// Checks the messages the When step published, on the specification's recorder of published messages: the one a plugin such as <c>KafkaPlugin</c> or <c>WolverinePlugin</c> registers, or one added with <c>Recording(...)</c>.
    /// Only messages that carry the When step's trace id count. On a recorder with a catch-up function it checks that <paramref name="expected"/> is exactly what was published, in order, compared on all fields.
    /// On a recorder without one it passes as soon as <paramref name="expected"/> appears as an in-order subsequence of the published messages, and fails at the deadline.
    /// With more than one recorder of published messages it fails at once, saying to name one with <see cref="ExpectPublishedIn"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// ExpectPublished(new OrderPlaced(cartId), new ShipOrder(cartId))
    /// </code>
    /// </example>
    public static Expectation ExpectPublished(params object[] expected) => new(ExpectMessages(PublishedWording, null, expected));

    /// <summary>Checks the messages the When step published, as <see cref="ExpectPublished"/> does, on <paramref name="recorded"/>, for a specification with more than one recorder of published messages.</summary>
    /// <example>
    /// <code>
    /// ExpectPublishedIn(Messaging.Kafka.Published, new OrderPlaced(cartId))
    /// </code>
    /// </example>
    public static Expectation ExpectPublishedIn<T>(Recorded<T> recorded, params object[] expected) =>
        new(ExpectMessages(PublishedWording, Named(recorded), expected));

    /// <summary>Checks that the When step published nothing, on the specification's recorder of published messages. It needs a catch-up function on that recorder.</summary>
    /// <example>
    /// <code>
    /// ExpectNothingPublished()
    /// </code>
    /// </example>
    public static Expectation ExpectNothingPublished() => new(ExpectNoMessages(PublishedWording, null));

    /// <summary>Checks that the When step published nothing on <paramref name="recorded"/>. It needs a catch-up function on that recorder.</summary>
    /// <example>
    /// <code>
    /// ExpectNothingPublishedIn(Messaging.Kafka.Published)
    /// </code>
    /// </example>
    public static Expectation ExpectNothingPublishedIn<T>(Recorded<T> recorded) =>
        new(ExpectNoMessages(PublishedWording, Named(recorded)));

    /// <summary>
    /// Checks the events the When step appended, on the specification's recorder of appended events: the one a plugin such as <c>MartenPlugin</c> registers, or one added with <c>Appending(...)</c>.
    /// It works like <see cref="ExpectPublished"/>, and its failure messages say events.
    /// </summary>
    /// <example>
    /// <code>
    /// ExpectAppended(new OrderPlaced(cartId))
    /// </code>
    /// </example>
    public static Expectation ExpectAppended(params object[] expected) => new(ExpectMessages(AppendedWording, null, expected));

    /// <summary>Checks the events the When step appended, as <see cref="ExpectAppended"/> does, on <paramref name="recorded"/>, for a specification with more than one recorder of appended events.</summary>
    /// <example>
    /// <code>
    /// ExpectAppendedIn(recorded.Items, new OrderPlaced(cartId))
    /// </code>
    /// </example>
    public static Expectation ExpectAppendedIn<T>(Recorded<T> recorded, params object[] expected) =>
        new(ExpectMessages(AppendedWording, Named(recorded), expected));

    /// <summary>Checks that the When step appended nothing, on the specification's recorder of appended events. It needs a catch-up function on that recorder.</summary>
    /// <example>
    /// <code>
    /// ExpectNothingAppended()
    /// </code>
    /// </example>
    public static Expectation ExpectNothingAppended() => new(ExpectNoMessages(AppendedWording, null));

    /// <summary>Checks that the When step appended nothing on <paramref name="recorded"/>. It needs a catch-up function on that recorder.</summary>
    /// <example>
    /// <code>
    /// ExpectNothingAppendedIn(recorded.Items)
    /// </code>
    /// </example>
    public static Expectation ExpectNothingAppendedIn<T>(Recorded<T> recorded) =>
        new(ExpectNoMessages(AppendedWording, Named(recorded)));

    private static (object Recorded, string Name) Named<T>(Recorded<T> recorded) => (recorded, $"Recorded<{typeof(T).Name}>");

    private static readonly MessageWording PublishedWording = new(RecordingRole.Published, "ExpectPublished", "ExpectNothingPublished", "message", "published", "Published");

    private static readonly MessageWording AppendedWording = new(RecordingRole.Appended, "ExpectAppended", "ExpectNothingAppended", "event", "appended", "Appended");

    private static Func<Outcome, Task> ExpectMessages(MessageWording wording, (object Recorded, string Name)? recorder,
        object[] expected) =>
        async outcome =>
        {
            var call = recorder is { } named ? $"{wording.Check}In({named.Name}, ...)" : $"{wording.Check}(...)";
            var recording = RecordingFor(recorder?.Recorded, wording, call, ExpectInForm, outcome);
            if (!recording.HasCatchUp)
            {
                await Waiting.Until(outcome, call,
                    _ => Task.FromResult(SubsequenceMismatch(wording, recording.MessagesOf(outcome), expected, outcome)),
                    found => $"{outcome.When}: {call}: {found}");
                return;
            }

            var published = recording.MessagesOf(outcome);
            var difference = DifferenceOf(wording, published, expected);
            if (difference is not null)
                throw new AssertionException(
                    $"{outcome.When}: {call}: {difference}{Environment.NewLine}{Transcript("Expected", expected, outcome)}{Transcript($"{wording.Title} by the When step", published, outcome)}");
        };

    private static Func<Outcome, Task> ExpectNoMessages(MessageWording wording, (object Recorded, string Name)? recorder) =>
        outcome =>
        {
            var call = recorder is { } named ? $"{wording.CheckNothing}In({named.Name})" : $"{wording.CheckNothing}()";
            var recording = RecordingFor(recorder?.Recorded, wording, call, ExpectNothingInForm, outcome);
            if (!recording.HasCatchUp)
                throw new AssertionException(
                    $"{outcome.When}: {call} needs a catch-up function on that recorder to know that nothing more will arrive; register it with {RegistrationOf(wording).Replace("...", "recorded, read, catchUp")}.");

            var published = recording.MessagesOf(outcome);
            if (published.Count > 0)
                throw new AssertionException(
                    $"{outcome.When}: {call}: expected no {wording.Item}s, but the When step {wording.Verb} {published.Count}.{Environment.NewLine}{Transcript($"{wording.Title} by the When step", published, outcome)}");
            return Task.CompletedTask;
        };

    private static string ExpectInForm(MessageWording wording, string? recorder) =>
        recorder is null ? $"{wording.Check}In(...)" : $"{wording.Check}In({recorder}, ...)";

    private static string ExpectNothingInForm(MessageWording wording, string? recorder) =>
        recorder is null ? $"{wording.CheckNothing}In(...)" : $"{wording.CheckNothing}In({recorder})";

    private static RecordingSource RecordingFor(object? recorded, MessageWording wording, string call,
        Func<MessageWording, string?, string> namedForm, Outcome outcome)
    {
        if (recorded is not null)
        {
            var named = outcome.Recordings.FirstOrDefault(recording => ReferenceEquals(recording.Recorded, recorded))
                ?? throw new AssertionException(
                    $"{outcome.When}: {call} needs a recorder registered on the specification with {RegistrationOf(wording)}, but this recorder is not registered.");
            if (named.Role == wording.Role)
                return named;
            var other = named.Role == RecordingRole.Published ? PublishedWording : AppendedWording;
            throw new AssertionException(
                $"{outcome.When}: {call} checks the {wording.Item}s the app {wording.Verb}, but this recorder is registered with {RegistrationOf(other)}, so it holds the {other.Item}s the app {other.Verb}. Check it with {namedForm(other, null)}.");
        }

        var recordings = outcome.Recordings.Where(recording => recording.Role == wording.Role).ToList();
        return recordings.Count switch
        {
            1 => recordings[0],
            0 => throw new AssertionException(
                $"{outcome.When}: {call} needs a recorder of the {wording.Item}s the app {wording.Verb}, but the specification has none. Add a plugin with Use(...), or register a recorder with {RegistrationOf(wording)}."),
            _ => throw new AssertionException(
                $"{outcome.When}: {call} found {recordings.Count} recorders of the {wording.Item}s the app {wording.Verb}, so it can't tell which one to check. Name it with {namedForm(wording, null)}, for example {namedForm(wording, ExampleRecorder(wording))}.")
        };
    }

    private static string ExampleRecorder(MessageWording wording) =>
        wording.Role == RecordingRole.Published ? "Messaging.Kafka.Published" : "Events.Marten.Appended";

    private static string RegistrationOf(MessageWording wording) =>
        wording.Role == RecordingRole.Published ? "Recording(...)" : "Appending(...)";

    private static string? DifferenceOf(MessageWording wording, IReadOnlyList<object> published, object[] expected)
    {
        if (published.Count != expected.Length)
            return $"expected {expected.Length} {wording.Item}s, but the When step {wording.Verb} {published.Count}.";

        for (var index = 0; index < expected.Length; index++)
        {
            var difference = DifferenceOfOne(wording, published[index], expected[index], index);
            if (difference is not null)
                return difference;
        }

        return null;
    }

    private static string? DifferenceOfOne(MessageWording wording, object published, object expected, int index)
    {
        var (actualType, expectedType) = (published.GetType(), expected.GetType());
        if (actualType != expectedType)
            return $"{wording.Item} {index + 1}: expected {expectedType.Name}, but found {actualType.Name}.";
        try
        {
            Expect.EquivalentWithEveryCollectionInOrder(published, expected, $"{wording.Item} {index + 1} ({expectedType.Name})");
        }
        catch (AssertionException difference)
        {
            return difference.Message;
        }

        return null;
    }

    private static string? SubsequenceMismatch(MessageWording wording, IReadOnlyList<object> published, object[] expected,
        Outcome outcome)
    {
        var matched = 0;
        foreach (var message in published)
            if (matched < expected.Length && DifferenceOfOne(wording, message, expected[matched], matched) is null)
                matched++;

        if (matched == expected.Length)
            return null;

        return $"after {outcome.Deadline}, {matched} of {expected.Length} expected {wording.Item}s arrived in order; {wording.Item} {matched + 1} ({expected[matched].GetType().Name}) did not arrive.{Environment.NewLine}{Transcript("Expected", expected, outcome)}{Transcript($"{wording.Title} by the When step", published, outcome)}";
    }

    private static string Transcript(string title, IReadOnlyList<object> messages, Outcome outcome)
    {
        var transcript = new StringBuilder($"{title}:{Environment.NewLine}");
        if (messages.Count == 0)
            transcript.AppendLine("  (none)");
        for (var index = 0; index < messages.Count; index++)
            transcript.AppendLine(
                $"  {index + 1}. {messages[index].GetType().Name} {JsonSerializer.Serialize(messages[index], messages[index].GetType(), outcome.Json)}");
        return transcript.ToString();
    }
}
