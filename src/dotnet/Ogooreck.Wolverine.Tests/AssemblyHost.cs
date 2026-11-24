using System.Collections.Concurrent;
using Ogooreck.API;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Wolverine.Tests.ShopRequests;

namespace Ogooreck.Wolverine.Tests;

public static class AssemblyHost
{
    private static readonly ConcurrentBag<string> Instances = [];

    public static async Task Record(AppApi api)
    {
        await api.Given()
            .When(GetInstance())
            .Then([ExpectResponse(OK,
                    Satisfying(async response => Instances.Add(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))))],
                TestContext.Current.CancellationToken);

        Assert.Single(Instances.Distinct());
    }
}
