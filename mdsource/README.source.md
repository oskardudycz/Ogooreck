[![Twitter Follow](https://img.shields.io/twitter/follow/oskar_at_net?style=social)](https://twitter.com/oskar_at_net) ![Github Actions](https://github.com/oskardudycz/Ogooreck/actions/workflows/build.dotnet.yml/badge.svg?branch=main) [![blog](https://img.shields.io/badge/blog-event--driven.io-brightgreen)](https://event-driven.io/?utm_source=event_sourcing_net) [![blog](https://img.shields.io/badge/%F0%9F%9A%80-Architecture%20Weekly-important)](https://www.architecture-weekly.com/?utm_source=event_sourcing_net)
[![Nuget Package](https://badgen.net/nuget/v/ogooreck)](https://www.nuget.org/packages/Ogooreck/)
[![Nuget](https://img.shields.io/nuget/dt/ogooreck)](https://www.nuget.org/packages/Ogooreck/)

# Ogooreck

Ogooreck is a Sneaky Test library. It helps to write readable and self-documenting tests.

Main assumptions:
- write tests seamlessly,
- make them readable,
- cut needed boilerplate by the set of helpful extensions and wrappers,
- don't replace testing frameworks (works with all, so XUnit, NUnit, MSTests, etc.),
- testing frameworks and assert library agnostic,
- keep things simple, but allow compositions and extension.

Current available for API testing.

TODO:
- CQRS tests,
- Aggregate Tests,
- Event Sourcing tests,
- Others.

## Support

Feel free to [create an issue](https://github.com/oskardudycz/Ogooreck/issues/new) if you have any questions or request for more explanation or samples. I also take **Pull Requests**!

💖 If this tool helped you - I'd be more than happy if you **join** the group of **my official supporters** at:

👉 [Github Sponsors](https://github.com/sponsors/oskardudycz)

⭐ Star on GitHub or sharing with your friends will also help!


## API Testing

Ogooreck provides a set of helpers to set up HTTP requests, Response assertions. It's recommended to add such usings to your tests:

```csharp
using Ogooreck.API;
using static Ogooreck.API.ApiSpecification;
```

Thanks to that, you'll get cleaner access to helper methods.

See more in samples below!

### POST

Ogooreck provides a set of helpers to construct the request (e.g. `URI`, `BODY`) and check the standardised responses.

```csharp
public Task POST_CreatesNewMeeting() =>
    API.Given(
            URI("/api/meetings/),
            BODY(new CreateMeeting(Guid.NewGuid(), "Event Sourcing Workshop"))
        )
        .When(POST)
        .Then(CREATED);
```

### PUT

You can also specify headers, e.g. `IF_MATCH` to perform an optimistic concurrency check.

```csharp
 public Task PUT_ConfirmsShoppingCart() =>
    API.Given(
            URI($"/api/ShoppingCarts/{API.ShoppingCartId}/confirmation"),
            HEADERS(IF_MATCH(1))
        )
        .When(PUT)
        .Then(OK);
```

### GET

You can also do response body assertions, to, e.g. out of the box check if the response body is equivalent to the expected one:

```csharp
public Task GET_ReturnsShoppingCartDetails()
    API.Given(
            URI($"/api/ShoppingCarts/{API.ShoppingCartId}")
        )
        .When(GET)
        .Then(
            OK,
            RESPONSE_BODY(new ShoppingCartDetails
            {
                Id = API.ShoppingCartId,
                Status = ShoppingCartStatus.Confirmed,
                ProductItems = new List<PricedProductItem>(),
                ClientId = API.ClientId,
                Version = 2,
            }));
```

You can also use `GET_UNTIL` helper to check API that has eventual consistency.

You can use various conditions, e.g. `RESPONSE_SUCCEEDED` waits until a response has one of the 2xx statuses. That's useful for new resource creation scenarios.

```csharp
public Task GET_ReturnsShoppingCartDetails()
    API.Given(
            URI($"/api/ShoppingCarts/{API.ShoppingCartId}")
        )
        .When(GET_UNTIL(RESPONSE_SUCCEEDED))
        .Then(
            OK,
            RESPONSE_BODY(new ShoppingCartDetails
            {
                Id = API.ShoppingCartId,
                Status = ShoppingCartStatus.Confirmed,
                ProductItems = new List<PricedProductItem>(),
                ClientId = API.ClientId,
                Version = 2,
            }));
```

You can also use `RESPONSE_ETAG_IS` helper to check if ETag matches your expected version. That's useful for state change verification.

```csharp
public Task GET_ReturnsShoppingCartDetails() =>
    API.Given(
            URI($"/api/ShoppingCarts/{API.ShoppingCartId}")
        )
        .When(GET_UNTIL(RESPONSE_ETAG_IS(2)))
        .Then(
            OK,
            RESPONSE_BODY(new ShoppingCartDetails
            {
                Id = API.ShoppingCartId,
                Status = ShoppingCartStatus.Confirmed,
                ProductItems = new List<PricedProductItem>(),
                ClientId = API.ClientId,
                Version = 2,
            }));
```

You can also do more advanced filtering via `RESPONSE_BODY_MATCHES`. That's useful for testing filtering scenarios with eventual consistency (e.g. having `Elasticsearch` as storage).

You can also do custom checks on the body, providing expression.

```csharp
public Task GET_ReturnsShoppingCartDetails() =>
    API.Given(
            URI($"{MeetingsSearchApi.MeetingsUrl}?filter={MeetingName}")
        )
        .When(
            GET_UNTIL(
                RESPONSE_BODY_MATCHES<IReadOnlyCollection<Meeting>>(
                    meetings => meetings.Any(m => m.Id == MeetingId))
            ))
        .Then(
            RESPONSE_BODY<IReadOnlyCollection<Meeting>>(meetings =>
                meetings.Should().Contain(meeting =>
                    meeting.Id == MeetingId
                    && meeting.Name == MeetingName
                )
            ));
```

### DELETE

Of course, the delete keyword is also supported.

```csharp
public Task DELETE_ShouldRemoveProductFromShoppingCart() =>
    API.Given(
            URI(
                $"/api/ShoppingCarts/{API.ShoppingCartId}/products/{API.ProductItem.ProductId}?quantity={RemovedCount}&unitPrice={API.UnitPrice}"),
            HEADERS(IF_MATCH(1))
        )
        .When(DELETE)
        .Then(NO_CONTENT);
```

### Scenarios and advanced composition

Ogooreck supports various ways of composing the API, e.g.

**Classic Async/Await**

```csharp
public async Task POST_WithExistingSKU_ReturnsConflictStatus()
{
    // Given
    var request = new RegisterProductRequest("AA2039485", ValidName, ValidDescription);

    // first one should succeed
    await API.Given(
            URI("/api/products/"),
            BODY(request)
        )
        .When(POST)
        .Then(CREATED);

    // second one will fail with conflict
    await API.Given(
            URI("/api/products/"),
            BODY(request)
        )
        .When(POST)
        .Then(CONFLICT);
}
```

**Joining with `And`**

```csharp
public Task SendPackage_ShouldReturn_CreatedStatus_With_PackageId() =>
        API.Given(
                URI("/api/Shipments/"),
                BODY(new SendPackage(OrderId, ProductItems))
            )
            .When(POST)
            .Then(CREATED)
            .And(response => fixture.ShouldPublishInternalEventOfType<PackageWasSent>(
                @event =>
                    @event.PackageId == response.GetCreatedId<Guid>()
                    && @event.OrderId == OrderId
                    && @event.SentAt > TimeBeforeSending
                    && @event.ProductItems.Count == ProductItems.Count
                    && @event.ProductItems.All(
                        pi => ProductItems.Exists(
                            expi => expi.ProductId == pi.ProductId && expi.Quantity == pi.Quantity))
            ));
```

**Chained Api Scenario**

```csharp
public async Task Post_ShouldReturn_CreatedStatus_With_CartId()
    {
        var createdReservationId = Guid.Empty;

        await API.Scenario(
            // Create Reservations
            API.Given(
                    URI("/api/Reservations/"),
                    BODY(new CreateTentativeReservationRequest { SeatId = SeatId })
                )
                .When(POST)
                .Then(CREATED,
                    response =>
                    {
                        createdReservationId = response.GetCreatedId<Guid>();
                        return ValueTask.CompletedTask;
                    }),

            // Get reservation details
            _ => API.Given(
                    URI($"/api/Reservations/{createdReservationId}")
                )
                .When(GET)
                .Then(
                    OK,
                    RESPONSE_BODY<ReservationDetails>(reservation =>
                    {
                        reservation.Id.Should().Be(createdReservationId);
                        reservation.Status.Should().Be(ReservationStatus.Tentative);
                        reservation.SeatId.Should().Be(SeatId);
                        reservation.Number.Should().NotBeEmpty();
                        reservation.Version.Should().Be(1);
                    })),

            // Get reservations list
            _ => API.Given(
                    URI("/api/Reservations/")
                )
                .When(GET)
                .Then(
                    OK,
                    RESPONSE_BODY<PagedListResponse<ReservationShortInfo>>(reservations =>
                    {
                        reservations.Should().NotBeNull();
                        reservations.Items.Should().NotBeNull();

                        reservations.Items.Should().HaveCount(1);
                        reservations.TotalItemCount.Should().Be(1);
                        reservations.HasNextPage.Should().Be(false);

                        var reservationInfo = reservations.Items.Single();

                        reservationInfo.Id.Should().Be(createdReservationId);
                        reservationInfo.Number.Should().NotBeNull().And.NotBeEmpty();
                        reservationInfo.Status.Should().Be(ReservationStatus.Tentative);
                    })),

            // Get reservation history
            _ => API.Given(
                    URI($"/api/Reservations/{createdReservationId}/history")
                )
                .When(GET)
                .Then(
                    OK,
                    RESPONSE_BODY<PagedListResponse<ReservationHistory>>(reservations =>
                    {
                        reservations.Should().NotBeNull();
                        reservations.Items.Should().NotBeNull();

                        reservations.Items.Should().HaveCount(1);
                        reservations.TotalItemCount.Should().Be(1);
                        reservations.HasNextPage.Should().Be(false);

                        var reservationInfo = reservations.Items.Single();

                        reservationInfo.ReservationId.Should().Be(createdReservationId);
                        reservationInfo.Description.Should().StartWith("Created tentative reservation with number");
                    }))
        );
    }
```

### XUnit setup

### Injecting as Class Fixture

By default, it's recommended to inject `ApiSpecification<YourProgram>` instance as `ClassFixture` to ensure that all dependencies (e.g. `HttpClient`) will be appropriately disposed.

```csharp
public class CreateMeetingTests: IClassFixture<ApiSpecification<Program>>
{
    private readonly ApiSpecification<Program> API;

    public CreateMeetingTests(ApiSpecification<Program> api) => API = api;

    [Fact]
    [Trait("Category", "Acceptance")]
    public Task CreateCommand_ShouldPublish_MeetingCreateEvent() =>
        API.Given(
                URI("/api/meetings/),
                BODY(new CreateMeeting(Guid.NewGuid(), "Event Sourcing Workshop"))
            )
            .When(POST)
            .Then(CREATED);
}
```


### Setting up data with `IAsyncLifetime`

Sometimes you need to set up test data asynchronously (e.g. open a shopping cart before cancelling it). You might not want to pollute your tests code with test case setup or do more extended preparation. For that XUnit provides `IAsyncLifetime` interface. You can create a fixture derived from the `APISpecification` to benefit from built-in helpers and use it later in your tests.

```csharp
public class CancelShoppingCartFixture: ApiSpecification<Program>, IAsyncLifetime
{
    public Guid ShoppingCartId { get; private set; }

    public async Task InitializeAsync()
    {
        var openResponse = await Send(
            new ApiRequest(POST, URI("/api/ShoppingCarts"), BODY(new OpenShoppingCartRequest(Guid.NewGuid())))
        );

        await CREATED(openResponse);

        ShoppingCartId = openResponse.GetCreatedId<Guid>();
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }
}

public class CancelShoppingCartTests: IClassFixture<CancelShoppingCartFixture>
{
    private readonly CancelShoppingCartFixture API;

    public CancelShoppingCartTests(CancelShoppingCartFixture api) => API = api;

    [Fact]
    [Trait("Category", "Acceptance")]
    public async Task Delete_Should_Return_OK_And_Cancel_Shopping_Cart() => 
        API.Given(
                URI($"/api/ShoppingCarts/{API.ShoppingCartId}"),
                HEADERS(IF_MATCH(1))
            )
            .When(DELETE)
            .Then(OK);
}
```

## Credits

Special thanks go to:
- Simon Cropp for [MarkdownSnippets](https://github.com/SimonCropp/MarkdownSnippets) that I'm using for plugging snippets to markdown,
- Adam Ralph for [BullsEye](https://github.com/adamralph/bullseye), which I'm using to make the build process seamless,
- [Babu Annamalai](https://mysticmind.dev/) that did a similar build setup in [Marten](https://martendb.io/) which I inspired a lot,
- Dennis Doomen for [Fluent Assertions](https://fluentassertions.com/), which I'm using for internal assertions, especially checking the response body.
