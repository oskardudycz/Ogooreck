using System.Runtime.ExceptionServices;
using Ogooreck.Assertions;

namespace Ogooreck.API;

internal sealed class StepCatchUp(SpecificationSettings settings, CancellationToken ct): IDisposable
{
    private readonly CancellationTokenSource deadlineTimer = new(Timeout.InfiniteTimeSpan, settings.Clock);
    private readonly CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
    private CancellationTokenRegistration deadlineRegistration;

    public bool StepFinished { get; private set; }

    private bool DeadlinePassed => deadline.IsCancellationRequested && !ct.IsCancellationRequested;

    public async Task RunStep(Func<CancellationToken, Task> step, Func<string> where)
    {
        if (settings.AroundSteps.Items.Length == 0)
        {
            await step(ct);
            StepFinished = true;
            StartDeadline();
            return;
        }

        Exception? stepError = null;
        var calls = 0;
        Func<CancellationToken, Task> once = async token =>
        {
            if (Interlocked.Increment(ref calls) > 1)
                throw new AssertionException(
                    $"{where()}: the catch-up around the step called it again; it must call the step exactly once.");
            try
            {
                using var stepCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, token);
                await step(stepCancellation.Token);
                StepFinished = true;
            }
            catch (Exception exception)
            {
                stepError = exception;
                throw;
            }
            finally
            {
                StartDeadline();
            }
        };
        var wrapped = once;
        foreach (var aroundStep in Enumerable.Reverse(settings.AroundSteps.Items))
        {
            var inner = wrapped;
            wrapped = token => aroundStep(inner, token);
        }

        var running = Started(() => wrapped(deadline.Token));
        var completed = await Within(running);

        Exception? failure = null;
        if (completed)
        {
            try
            {
                await running;
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }

        if (ct.IsCancellationRequested)
            ExceptionDispatchInfo.Throw(failure ?? new OperationCanceledException(ct));
        if (calls == 0)
            throw new AssertionException(
                $"{where()}: the catch-up around the step returned without running the step; it must call the step exactly once.");
        if (calls > 1)
            throw new AssertionException(
                $"{where()}: the catch-up around the step called the step {calls} times; it must call the step exactly once. The step ran once.");
        if (stepError is not null)
            ExceptionDispatchInfo.Throw(stepError);
        if (!completed || failure is not null && DeadlinePassed)
            throw KeptProducing(where(), failure);
        if (failure is not null)
            throw new AssertionException($"{where()}: the catch-up around the step failed: {failure.Message}", failure);
    }

    public async Task AfterStep(string where)
    {
        var catchUps = settings.CatchUps.Items;
        if (catchUps.Length == 0)
            return;

        while (true)
        {
            var before = settings.RecordedCount;
            for (var index = 0; index < catchUps.Length; index++)
            {
                var catchUp = catchUps[index];
                var running = Started(() => catchUp(deadline.Token));
                var completed = await Within(running);
                ct.ThrowIfCancellationRequested();
                if (!completed)
                    throw KeptProducing(where, null);

                try
                {
                    await running;
                }
                catch (Exception exception) when (!ct.IsCancellationRequested)
                {
                    if (DeadlinePassed)
                        throw KeptProducing(where, exception);
                    throw new AssertionException(
                        $"{where}: catch-up function {index + 1} of {catchUps.Length} failed: {exception.Message}",
                        exception);
                }
            }

            if (settings.RecordedCount == before)
                return;
            ct.ThrowIfCancellationRequested();
            if (DeadlinePassed)
                throw KeptProducing(where, null);
        }
    }

    public void Dispose()
    {
        deadlineRegistration.Dispose();
        deadline.Dispose();
        deadlineTimer.Dispose();
    }

    private void StartDeadline()
    {
        deadlineRegistration = deadlineTimer.Token.Register(deadline.Cancel);
        deadlineTimer.CancelAfter(settings.Deadline);
    }

    private static Task Started(Func<Task> start)
    {
        try
        {
            return start();
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    private async Task<bool> Within(Task running)
    {
        if (await Task.WhenAny(running, Task.Delay(Timeout.Infinite, deadline.Token)) == running)
            return true;

        _ = running.ContinueWith(task => task.Exception, TaskContinuationOptions.OnlyOnFaulted);
        return false;
    }

    private AssertionException KeptProducing(string where, Exception? exception) =>
        exception is null
            ? new AssertionException(
                $"The application kept producing messages for longer than {settings.Deadline} after the {where}.")
            : new AssertionException(
                $"The application kept producing messages for longer than {settings.Deadline} after the {where}. The catch-up reported: {exception.Message}",
                exception);
}
