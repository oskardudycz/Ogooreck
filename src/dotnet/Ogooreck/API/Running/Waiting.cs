using Ogooreck.Assertions;

namespace Ogooreck.API;

internal static class Waiting
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    public static async Task Until(Outcome outcome, string call, Func<CancellationToken, Task<string?>> mismatch,
        Func<string, string> atDeadline)
    {
        var started = outcome.Clock.GetTimestamp();
        try
        {
            while (true)
            {
                var found = await mismatch(outcome.CancellationToken);
                if (found is null)
                    return;
                outcome.CancellationToken.ThrowIfCancellationRequested();
                var remaining = outcome.Deadline - outcome.Clock.GetElapsedTime(started);
                if (remaining <= TimeSpan.Zero)
                    throw new AssertionException(atDeadline(found));
                await Task.Delay(TimeSpan.FromTicks(Math.Min(remaining.Ticks, PollInterval.Ticks)), outcome.Clock,
                    outcome.CancellationToken);
            }
        }
        catch (OperationCanceledException exception) when (outcome.CancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(
                $"Then was cancelled while {call} was waiting, after the {outcome.When}.", exception,
                outcome.CancellationToken);
        }
    }
}
