using System.Text;
using System.Text.Json;
using Ogooreck.Assertions;

namespace Ogooreck.API;

public partial class ApiSpecification
{
    /// <summary>A command or an event that the app published, on the specification's recorder of published messages, expected to cause <paramref name="caused"/>. It means the same as <see cref="Published"/>.</summary>
    /// <example>
    /// <code>
    /// Published(new OrderPlaced(cartId), Sent(new ShipOrder(cartId)))
    /// </code>
    /// </example>
    public static Caused Sent(object message, params Caused[] caused) => new(null, PublishedWording, message, caused);

    /// <summary>A message or event that the app published, on the specification's recorder of published messages, expected to cause <paramref name="caused"/>.</summary>
    /// <example>
    /// <code>
    /// ExpectCausation(Published(new OrderPlaced(cartId), Sent(new ShipOrder(cartId))))
    /// </code>
    /// </example>
    public static Caused Published(object message, params Caused[] caused) => new(null, PublishedWording, message, caused);

    /// <summary>An event that the app appended, on the specification's recorder of appended events, expected to cause <paramref name="caused"/>.</summary>
    /// <example>
    /// <code>
    /// ExpectCausation(Appended(new OrderPlaced(cartId), Published(new ShipOrder(cartId))))
    /// </code>
    /// </example>
    public static Caused Appended(object message, params Caused[] caused) => new(null, AppendedWording, message, caused);

    /// <summary>As <see cref="Sent"/>, on <paramref name="recorded"/>, for a specification with more than one recorder of its kind.</summary>
    /// <example>
    /// <code>
    /// SentIn(Messaging.Kafka.Published, new ShipOrder(cartId))
    /// </code>
    /// </example>
    public static Caused SentIn<T>(Recorded<T> recorded, object message, params Caused[] caused) =>
        new(recorded, PublishedWording, message, caused);

    /// <summary>As <see cref="Published"/>, on <paramref name="recorded"/>, for a specification with more than one recorder of its kind.</summary>
    /// <example>
    /// <code>
    /// PublishedIn(Messaging.Kafka.Published, new OrderPlaced(cartId), SentIn(Messaging.Kafka.Published, new ShipOrder(cartId)))
    /// </code>
    /// </example>
    public static Caused PublishedIn<T>(Recorded<T> recorded, object message, params Caused[] caused) =>
        new(recorded, PublishedWording, message, caused);

    /// <summary>As <see cref="Appended"/>, on <paramref name="recorded"/>, for a specification with more than one recorder of its kind.</summary>
    /// <example>
    /// <code>
    /// AppendedIn(recorded.Items, new OrderPlaced(cartId))
    /// </code>
    /// </example>
    public static Caused AppendedIn<T>(Recorded<T> recorded, object message, params Caused[] caused) =>
        new(recorded, AppendedWording, message, caused);

    /// <summary>
    /// Checks what caused what, with the ids that the messages really carry. Every message in a tree has the correlation id of its root,
    /// and each child's causation id is the message id of its parent. Only items of the When step count.
    /// The recorders in the trees need <c>read</c> to return message, correlation and causation ids in <see cref="Observed"/>.
    /// When every recorder in the trees has a catch-up function it checks once after catch-up; otherwise it waits until the trees are found, or fails at the deadline.
    /// </summary>
    /// <example>
    /// <code>
    /// ExpectCausation(Published(new OrderPlaced(cartId), Sent(new ShipOrder(cartId))))
    /// </code>
    /// </example>
    public static Expectation ExpectCausation(params Caused[] roots) =>
        new(async outcome =>
        {
            const string call = "ExpectCausation(...)";
            var recordings = roots.SelectMany(Flatten).Select(node => RecordingFor(node.Recorded, node.Wording, call, CausationForm, outcome)).ToList();

            if (recordings.All(recording => recording.HasCatchUp))
            {
                var mismatch = CausationMismatch(roots, outcome);
                if (mismatch is not null)
                    throw new AssertionException($"{outcome.When}: {call}: {mismatch}");
                return;
            }

            await Waiting.Until(outcome, call, _ => Task.FromResult(CausationMismatch(roots, outcome)),
                found => $"{outcome.When}: {call} did not find the tree within {outcome.Deadline}: {found}");
        });

    private static string CausationForm(MessageWording wording, string? recorder) =>
        (wording.Role, recorder) switch
        {
            (RecordingRole.Published, null) => "PublishedIn(...) or SentIn(...)",
            (RecordingRole.Published, _) => $"PublishedIn({recorder}, ...) or SentIn({recorder}, ...)",
            (_, null) => "AppendedIn(...)",
            _ => $"AppendedIn({recorder}, ...)"
        };

    private static IEnumerable<Caused> Flatten(Caused node) => node.Children.SelectMany(Flatten).Prepend(node);

    private static string? CausationMismatch(IReadOnlyList<Caused> roots, Outcome outcome)
    {
        foreach (var root in roots)
        {
            var found = FindIn(root, null, outcome, out var mismatch);
            if (found is null)
                return mismatch;
            mismatch = CausedBy(root, found, found.CorrelationId, outcome);
            if (mismatch is not null)
                return mismatch;
        }

        return null;
    }

    private static string? CausedBy(Caused expected, Observed cause, string? correlationId, Outcome outcome)
    {
        if (cause.CorrelationId != correlationId)
            return $"expected correlation id {correlationId}, but found {cause.CorrelationId} for {Describe(expected, outcome)}.{Environment.NewLine}{Listing(expected, outcome)}";

        foreach (var child in expected.Children)
        {
            var found = FindIn(child, cause, outcome, out var mismatch);
            if (found is null)
                return mismatch;
            mismatch = CausedBy(child, found, correlationId, outcome);
            if (mismatch is not null)
                return mismatch;
        }

        return null;
    }

    private static Observed? FindIn(Caused expected, Observed? parent, Outcome outcome, out string? mismatch)
    {
        var observed = ObservedOfWhenStep(expected, outcome);
        var matching = observed.Where(item => IsEqual(item.Message, expected.Message)).ToList();
        if (matching.Count == 0)
        {
            mismatch =
                $"expected {Describe(expected, outcome)} to be {expected.Wording.Verb}, but the When step {expected.Wording.Verb} only:{Environment.NewLine}{Listing(expected, outcome)}";
            return null;
        }

        foreach (var item in matching)
            RequireIds(item, parent is not null, expected.Children.Count > 0, outcome);

        if (parent is null)
        {
            mismatch = null;
            return matching[0];
        }

        var caused = matching.FirstOrDefault(item => item.CausationId == parent.MessageId);
        if (caused is null)
        {
            mismatch =
                $"{Describe(expected, outcome)}: expected causation id {parent.MessageId}, but found {string.Join(", ", matching.Select(item => item.CausationId))}.{Environment.NewLine}{Listing(expected, outcome)}";
            return null;
        }

        mismatch = null;
        return caused;
    }

    private static void RequireIds(Observed item, bool needsCausation, bool needsMessageId, Outcome outcome)
    {
        var missing = new List<string>();
        if (needsMessageId && item.MessageId is null)
            missing.Add("message id");
        if (item.CorrelationId is null)
            missing.Add("correlation id");
        if (needsCausation && item.CausationId is null)
            missing.Add("causation id");
        if (missing.Count == 0)
            return;

        throw new AssertionException(
            $"{outcome.When}: ExpectCausation(...) needs message, correlation and causation ids from read, but read returned no {string.Join(" and ", missing)} for {item.Message.GetType().Name}. Return them in Observed from the function given to Recording(...).");
    }

    private static IReadOnlyList<Observed> ObservedOfWhenStep(Caused node, Outcome outcome) =>
        RecordingFor(node.Recorded, node.Wording, "ExpectCausation(...)", CausationForm, outcome).ObservedOf(outcome);

    private static bool IsEqual(object actual, object expected)
    {
        if (actual.GetType() != expected.GetType())
            return false;
        try
        {
            Expect.EquivalentWithEveryCollectionInOrder(actual, expected, "message");
            return true;
        }
        catch (AssertionException)
        {
            return false;
        }
    }

    private static string Describe(Caused node, Outcome outcome) =>
        $"{node.Message.GetType().Name} {JsonSerializer.Serialize(node.Message, node.Message.GetType(), outcome.Json)}";

    private static string Listing(Caused node, Outcome outcome)
    {
        var observed = ObservedOfWhenStep(node, outcome);
        var listing = new StringBuilder($"{node.Wording.Title} by the When step:{Environment.NewLine}");
        if (observed.Count == 0)
            listing.AppendLine("  (none)");
        for (var index = 0; index < observed.Count; index++)
            listing.AppendLine(
                $"  {index + 1}. {observed[index].Message.GetType().Name} {JsonSerializer.Serialize(observed[index].Message, observed[index].Message.GetType(), outcome.Json)} (id {observed[index].MessageId ?? "(none)"}, correlation {observed[index].CorrelationId ?? "(none)"}, causation {observed[index].CausationId ?? "(none)"})");
        return listing.ToString();
    }
}
