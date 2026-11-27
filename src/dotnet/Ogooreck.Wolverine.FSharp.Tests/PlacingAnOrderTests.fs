module PlacingAnOrderTests

open System
open System.Net
open Ogooreck.Assertions
open Ogooreck.Sample.Wolverine.Events
open Shop
open Xunit
open type Ogooreck.API.ApiSpecification

// begin-snippet: FSharpApiTest
[<Fact>]
let ``placing an order publishes OrderPlaced`` () =
    task {
        use shop = new Shop()
        let cartId = Guid.NewGuid()

        do!
            shop
                .Given(addProduct cartId "socks")
                .When(placeOrder cartId)
                .Then(
                    [| ExpectResponse(HttpStatusCode.Created)
                       ExpectPublished(
                           OrderPlaced(
                               cartId,
                               Total = 5m,
                               Products = [ "socks" ]
                           )
                       ) |],
                    TestContext.Current.CancellationToken
                )
    }
// end-snippet

[<Fact>]
let ``an unexpected published message fails with the numbered list`` () =
    task {
        use shop = new Shop()
        let cartId = Guid.NewGuid()

        let! error =
            Assert.ThrowsAsync<AssertionException>(fun () ->
                shop
                    .Given(addProduct cartId "socks")
                    .When(placeOrder cartId)
                    .Then(
                        [| ExpectPublished(OrderPlaced(Guid.NewGuid())) |],
                        TestContext.Current.CancellationToken
                    ))

        Assert.Contains("1. OrderPlaced", error.Message)
    }
