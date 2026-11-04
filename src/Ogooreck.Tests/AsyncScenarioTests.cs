using Ogooreck.API;
using TestContext = Ogooreck.API.TestContext;
using Xunit;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Tests;

public class AsyncScenarioTests
{
    private static Task<Result> CompletedResult(HttpResponseMessage response) =>
        Task.FromResult(new Result(response, new TestContext(), () => new HttpClient()));

    [Fact]
    public async Task AndWaitsForAsyncCallbacksAndPropagatesTheirFailure()
    {
        using var response = new HttpResponseMessage();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = CompletedResult(response).And((Func<HttpResponseMessage, Task>)(async _ =>
        {
            callbackStarted.SetResult();
            await completion.Task;
        }));
        await callbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        Assert.False(task.IsCompleted);
        completion.SetException(new InvalidOperationException("callback failed"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => task);
    }

    [Fact]
    public async Task AndAwaitsAllAsyncOverloads()
    {
        using var response = new HttpResponseMessage();
        var failed = Task.FromException(new InvalidOperationException());
        await Assert.ThrowsAsync<InvalidOperationException>(() => CompletedResult(response).And(() => failed));
        await Assert.ThrowsAsync<InvalidOperationException>(() => CompletedResult(response).And(failed));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CompletedResult(response).And<int>(_ => Task.FromException<int>(new InvalidOperationException())));
    }

    [Fact]
    public async Task FailedPreviousStepPreventsCallbackAndPreservesException()
    {
        var failure = new InvalidOperationException("previous step failed");
        var previous = Task.FromException<Result>(failure);
        var called = false;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => previous.And(() =>
        {
            called = true;
            return Task.CompletedTask;
        }));
        Assert.False(called);
        Assert.Same(failure, error);
    }
}
