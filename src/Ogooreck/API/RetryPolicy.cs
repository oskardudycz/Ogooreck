using Ogooreck.Assertions;

namespace Ogooreck.API;

#pragma warning disable CS1591
public record RetryPolicy(
    RetryCheck Check,
    int MaxNumberOfRetries = 5,
    int RetryIntervalInMs = 1000
)
{
    public async Task<HttpResponseMessage> Perform(Func<CancellationToken, Task<HttpResponseMessage>> send, TestContext testContext, CancellationToken ct)
    {
        var retryCount = MaxNumberOfRetries;
        var finished = false;

        HttpResponseMessage? response = null;
        do
        {
            try
            {
                response = await send(ct);

                finished = await Check(response, testContext);
            }
            catch
            {
                if (retryCount == 0)
                    throw;
            }

            await Task.Delay(RetryIntervalInMs, ct);
            retryCount--;
        } while (!finished);

        Expect.That(response is not null, "Expected an HTTP response.");

        return response!;
    }

    public static readonly RetryPolicy NoRetry = new RetryPolicy(
        (r, t) => ValueTask.FromResult(true),
        0,
        0
    );
}

public delegate ValueTask<bool> RetryCheck(HttpResponseMessage responseMessage, TestContext testContext);

