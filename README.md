[![Twitter Follow](https://img.shields.io/twitter/follow/oskar_at_net?style=social)](https://twitter.com/oskar_at_net) ![Github Actions](https://github.com/oskardudycz/Ogooreck/actions/workflows/build.dotnet.yml/badge.svg?branch=main) [![blog](https://img.shields.io/badge/blog-event--driven.io-brightgreen)](https://event-driven.io/?utm_source=event_sourcing_net) [![blog](https://img.shields.io/badge/%F0%9F%9A%80-Architecture%20Weekly-important)](https://www.architecture-weekly.com/?utm_source=event_sourcing_net)
[![Nuget Package](https://badgen.net/nuget/v/ogooreck)](https://www.nuget.org/packages/Ogooreck/)
[![Nuget](https://img.shields.io/nuget/dt/ogooreck)](https://www.nuget.org/packages/Ogooreck/)

# 🥒 Ogooreck

Ogooreck is a Sneaky Test library. It helps to write readable and self-documenting tests. It's both C# and F# friendly!

Main assumptions:
- write tests seamlessly,
- make them readable,
- cut needed boilerplate by the set of helpful extensions and wrappers,
- don't create a full-blown BDD framework,
- no Domain-Specific Language,
- don't replace testing frameworks (works with all, so XUnit, NUnit, MSTests, etc.),
- testing frameworks and assert library agnostic,
- keep things simple, but allow compositions and extension.

Current available for API testing.


Current available for testing:
- [Business Logic](#business-logic-testing),
- [API](#api-testing).

Check also [introduction post on my blog](https://event-driven.io/en/ogooreck_sneaky_bdd_testing_framework/).

## Support

Feel free to [create an issue](https://github.com/oskardudycz/Ogooreck/issues/new) if you have any questions or request for more explanation or samples. I also take **Pull Requests**!

💖 If this tool helped you - I'd be more than happy if you **join** the group of **my official supporters** at:

👉 [Github Sponsors](https://github.com/sponsors/oskardudycz)

⭐ Star on GitHub or sharing with your friends will also help!

## Business Logic Testing

Ogooreck provides a set of helpers to set up business logic tests. It's recommended to add such using to your tests:

```csharp
using Ogooreck.BusinessLogic;
```

Read more in the [Testing business logic in Event Sourcing, and beyond!](https://event-driven.io/en/testing_event_sourcing/) article.

### Decider and Command Handling tests

You can use `DeciderSpecification` to run decider and command handling tests. See the example:

**C#**
```csharp
using FluentAssertions;
using Ogooreck.BusinessLogic;

namespace Ogooreck.Sample.BusinessLogic.Tests.Deciders;

using static BankAccountEventsBuilder;

public class BankAccountTests
{
    private readonly Random random = new();
    private static readonly DateTimeOffset now = DateTimeOffset.UtcNow;

    private readonly DeciderSpecification<BankAccount> Spec = Specification.For<BankAccount>(
        (command, bankAccount) => BankAccountDecider.Handle(() => now, command, bankAccount),
        BankAccount.Evolve
    );

    [Fact]
    public void GivenNonExistingBankAccount_WhenOpenWithValidParams_ThenSucceeds()
    {
        var bankAccountId = Guid.NewGuid();
        var accountNumber = Guid.NewGuid().ToString();
        var clientId = Guid.NewGuid();
        var currencyISOCode = "USD";

        Spec.Given()
            .When(new OpenBankAccount(bankAccountId, accountNumber, clientId, currencyISOCode))
            .Then(new BankAccountOpened(bankAccountId, accountNumber, clientId, currencyISOCode, now, 1));
    }

    [Fact]
    public void GivenOpenBankAccount_WhenRecordDepositWithValidParams_ThenSucceeds()
    {
        var bankAccountId = Guid.NewGuid();

        var amount = (decimal)random.NextDouble();
        var cashierId = Guid.NewGuid();

        Spec.Given(BankAccountOpened(bankAccountId, now, 1))
            .When(new RecordDeposit(amount, cashierId))
            .Then(new DepositRecorded(bankAccountId, amount, cashierId, now, 2));
    }

    [Fact]
    public void GivenClosedBankAccount_WhenRecordDepositWithValidParams_ThenFailsWithInvalidOperationException()
    {
        var bankAccountId = Guid.NewGuid();

        var amount = (decimal)random.NextDouble();
        var cashierId = Guid.NewGuid();

        Spec.Given(
                BankAccountOpened(bankAccountId, now, 1),
                BankAccountClosed(bankAccountId, now, 2)
            )
            .When(new RecordDeposit(amount, cashierId))
            .ThenThrows<InvalidOperationException>(exception => exception.Message.Should().Be("Account is closed!"));
    }
}

public static class BankAccountEventsBuilder
{
    public static BankAccountOpened BankAccountOpened(Guid bankAccountId, DateTimeOffset now, long version)
    {
        var accountNumber = Guid.NewGuid().ToString();
        var clientId = Guid.NewGuid();
        var currencyISOCode = "USD";

        return new BankAccountOpened(bankAccountId, accountNumber, clientId, currencyISOCode, now, version);
    }

    public static BankAccountClosed BankAccountClosed(Guid bankAccountId, DateTimeOffset now, long version)
    {
        var reason = Guid.NewGuid().ToString();

        return new BankAccountClosed(bankAccountId, reason, now, version);
    }
}
```

**F#**
```fsharp
module BankAccountTests

open System
open Deciders.BankAccount
open Deciders.BankAccountPrimitives
open Deciders.BankAccountDecider
open Ogooreck.BusinessLogic
open FsCheck.Xunit

let random = Random()

let spec =
    Specification.For(decide, evolve, Initial)

let BankAccountOpenedWith bankAccountId now version =
    let accountNumber =
        AccountNumber.parse (Guid.NewGuid().ToString())

    let clientId = ClientId.newId ()

    let currencyISOCode =
        CurrencyIsoCode.parse "USD"

    BankAccountOpened
        { BankAccountId = bankAccountId
          AccountNumber = accountNumber
          ClientId = clientId
          CurrencyIsoCode = currencyISOCode
          CreatedAt = now
          Version = version }

let BankAccountClosedWith bankAccountId now version =
    BankAccountClosed
        { BankAccountId = bankAccountId
          Reason = Guid.NewGuid().ToString()
          ClosedAt = now
          Version = version }

[<Property>]
let ``GIVEN non existing bank account WHEN open with valid params THEN bank account is opened``
    bankAccountId
    accountNumber
    clientId
    currencyISOCode
    now
    =
    let notExistingAccount = Array.empty

    spec
        .Given(notExistingAccount)
        .When(
            OpenBankAccount
                { BankAccountId = bankAccountId
                  AccountNumber = accountNumber
                  ClientId = clientId
                  CurrencyIsoCode = currencyISOCode
                  Now = now }
        )
        .Then(
            BankAccountOpened
                { BankAccountId = bankAccountId
                  AccountNumber = accountNumber
                  ClientId = clientId
                  CurrencyIsoCode = currencyISOCode
                  CreatedAt = now
                  Version = 1 }
        )
    |> ignore

[<Property>]
let ``GIVEN open bank account WHEN record deposit with valid params THEN deposit is recorded``
    bankAccountId
    amount
    cashierId
    now
    =
    spec
        .Given(BankAccountOpenedWith bankAccountId now 1)
        .When(
            RecordDeposit
                { Amount = amount
                  CashierId = cashierId
                  Now = now }
        )
        .Then(
            DepositRecorded
                { BankAccountId = bankAccountId
                  Amount = amount
                  CashierId = cashierId
                  RecordedAt = now
                  Version = 2 }
        )
    |> ignore

[<Property>]
let ``GIVEN closed bank account WHEN record deposit with valid params THEN fails with invalid operation exception``
    bankAccountId
    amount
    cashierId
    now
    =
    spec
        .Given(
            BankAccountOpenedWith bankAccountId now 1,
            BankAccountClosedWith bankAccountId now 2
        )
        .When(
            RecordDeposit
                { Amount = amount
                  CashierId = cashierId
                  Now = now }
        )
        .ThenThrows<InvalidOperationException>
    |> ignore
```

See full sample in [tests](/src/Ogooreck.Sample.BusinessLogic.Tests/Deciders/BankAccountTests.cs).

### Event-Sourced command handlers

You can use `HandlerSpecification` to run event-sourced command handling tests for pure functions and entities. See the example:

```csharp
using Ogooreck.BusinessLogic;

namespace Ogooreck.Sample.BusinessLogic.Tests.Functions.EventSourced;

using static IncidentEventsBuilder;
using static IncidentService;

public class IncidentTests
{
    private static readonly DateTimeOffset now = DateTimeOffset.UtcNow;

    private static readonly Func<Incident, object, Incident> evolve =
        (incident, @event) =>
        {
            return @event switch
            {
                IncidentLogged logged => Incident.Create(logged),
                IncidentCategorised categorised => incident.Apply(categorised),
                IncidentPrioritised prioritised => incident.Apply(prioritised),
                AgentRespondedToIncident agentResponded => incident.Apply(agentResponded),
                CustomerRespondedToIncident customerResponded => incident.Apply(customerResponded),
                IncidentResolved resolved => incident.Apply(resolved),
                ResolutionAcknowledgedByCustomer acknowledged => incident.Apply(acknowledged),
                IncidentClosed closed => incident.Apply(closed),
                _ => incident
            };
        };

    private readonly HandlerSpecification<Incident> Spec = Specification.For<Incident>(evolve);

    [Fact]
    public void GivenNonExistingIncident_WhenOpenWithValidParams_ThenSucceeds()
    {
        var incidentId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var contact = new Contact(ContactChannel.Email, EmailAddress: "john@doe.com");
        var description = Guid.NewGuid().ToString();
        var loggedBy = Guid.NewGuid();

        Spec.Given()
            .When(() => Handle(() => now, new LogIncident(incidentId, customerId, contact, description, loggedBy)))
            .Then(new IncidentLogged(incidentId, customerId, contact, description, loggedBy, now));
    }

    [Fact]
    public void GivenOpenIncident_WhenCategoriseWithValidParams_ThenSucceeds()
    {
        var incidentId = Guid.NewGuid();

        var category = IncidentCategory.Database;
        var categorisedBy = Guid.NewGuid();

        Spec.Given(IncidentLogged(incidentId, now))
            .When(incident => Handle(() => now, incident, new CategoriseIncident(incidentId, category, categorisedBy)))
            .Then(new IncidentCategorised(incidentId, category, categorisedBy, now));
    }
}

public static class IncidentEventsBuilder
{
    public static IncidentLogged IncidentLogged(Guid incidentId, DateTimeOffset now)
    {
        var customerId = Guid.NewGuid();
        var contact = new Contact(ContactChannel.Email, EmailAddress: "john@doe.com");
        var description = Guid.NewGuid().ToString();
        var loggedBy = Guid.NewGuid();

        return new IncidentLogged(incidentId, customerId, contact, description, loggedBy, now);
    }
}
```

See full sample in [tests](/src/Ogooreck.Sample.BusinessLogic.Tests/Functions/EventSourced/IncidentTests.cs).

### State-based command handlers

You can use `HandlerSpecification` to run state-based command handling tests for pure functions and entities. See the example:

```csharp
using Ogooreck.BusinessLogic;

namespace Ogooreck.Sample.BusinessLogic.Tests.Functions.StateBased;

using static IncidentEventsBuilder;
using static IncidentService;

public class IncidentTests
{
    private static readonly DateTimeOffset now = DateTimeOffset.UtcNow;

    private readonly HandlerSpecification<Incident> Spec = Specification.For<Incident>();

    [Fact]
    public void GivenNonExistingIncident_WhenOpenWithValidParams_ThenSucceeds()
    {
        var incidentId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var contact = new Contact(ContactChannel.Email, EmailAddress: "john@doe.com");
        var description = Guid.NewGuid().ToString();
        var loggedBy = Guid.NewGuid();

        Spec.Given()
            .When(() => Handle(() => now, new LogIncident(incidentId, customerId, contact, description, loggedBy)))
            .Then(new Incident(incidentId, customerId, contact, loggedBy, now, description));
    }

    [Fact]
    public void GivenOpenIncident_WhenCategoriseWithValidParams_ThenSucceeds()
    {
        var incidentId = Guid.NewGuid();
        var loggedIncident = LoggedIncident(incidentId, now);

        var category = IncidentCategory.Database;
        var categorisedBy = Guid.NewGuid();

        Spec.Given(loggedIncident)
            .When(incident => Handle(() => now, incident, new CategoriseIncident(incidentId, category, categorisedBy)))
            .Then(loggedIncident with { Category = category });
    }
}

public static class IncidentEventsBuilder
{
    public static Incident LoggedIncident(Guid incidentId, DateTimeOffset now)
    {
        var customerId = Guid.NewGuid();
        var contact = new Contact(ContactChannel.Email, EmailAddress: "john@doe.com");
        var description = Guid.NewGuid().ToString();
        var loggedBy = Guid.NewGuid();

        return new Incident(incidentId, customerId, contact, loggedBy, now, description);
    }
}

```

See full sample in [tests](/src/Ogooreck.Sample.BusinessLogic.Tests/Functions/EventSourced/IncidentTests.cs).


### Event-Driven Aggregate tests

You can use `HandlerSpecification` to run event-driven aggregat tests. See the example:

```csharp
using Ogooreck.BusinessLogic;
using Ogooreck.Sample.BusinessLogic.Tests.Aggregates.EventSourced.Core;
using Ogooreck.Sample.BusinessLogic.Tests.Aggregates.EventSourced.Pricing;
using Ogooreck.Sample.BusinessLogic.Tests.Aggregates.EventSourced.Products;
using Ogooreck.Sample.BusinessLogic.Tests.Functions.EventSourced;

namespace Ogooreck.Sample.BusinessLogic.Tests.Aggregates.EventSourced;

using static ShoppingCartEventsBuilder;
using static ProductItemBuilder;
using static AggregateTestExtensions<ShoppingCart>;

public class ShoppingCartTests
{
    private readonly Random random = new();

    private readonly HandlerSpecification<ShoppingCart> Spec =
        Specification.For<ShoppingCart>(Handle, ShoppingCart.Evolve);

    private class DummyProductPriceCalculator: IProductPriceCalculator
    {
        private readonly decimal price;

        public DummyProductPriceCalculator(decimal price) => this.price = price;

        public IReadOnlyList<PricedProductItem> Calculate(params ProductItem[] productItems) =>
            productItems.Select(pi => PricedProductItem.For(pi, price)).ToList();
    }

    [Fact]
    public void GivenNonExistingShoppingCart_WhenOpenWithValidParams_ThenSucceeds()
    {
        var shoppingCartId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        Spec.Given()
            .When(() => ShoppingCart.Open(shoppingCartId, clientId))
            .Then(new ShoppingCartOpened(shoppingCartId, clientId));
    }

    [Fact]
    public void GivenOpenShoppingCart_WhenAddProductWithValidParams_ThenSucceeds()
    {
        var shoppingCartId = Guid.NewGuid();

        var productItem = ValidProductItem();
        var price = random.Next(1, 1000);
        var priceCalculator = new DummyProductPriceCalculator(price);

        Spec.Given(ShoppingCartOpened(shoppingCartId))
            .When(cart => cart.AddProduct(priceCalculator, productItem))
            .Then(new ProductAdded(shoppingCartId, PricedProductItem.For(productItem, price)));
    }
}

public static class ShoppingCartEventsBuilder
{
    public static ShoppingCartOpened ShoppingCartOpened(Guid shoppingCartId)
    {
        var clientId = Guid.NewGuid();

        return new ShoppingCartOpened(shoppingCartId, clientId);
    }
}

public static class ProductItemBuilder
{
    private static readonly Random Random = new();

    public static ProductItem ValidProductItem() =>
        ProductItem.From(Guid.NewGuid(), Random.Next(1, 100));
}

public static class AggregateTestExtensions<TAggregate> where TAggregate : Aggregate
{
    public static DecideResult<object, TAggregate> Handle(Handler<object, TAggregate> handle, TAggregate aggregate)
    {
        var result = handle(aggregate);
        var updatedAggregate = result.NewState ?? aggregate;
        return DecideResult.For(updatedAggregate, updatedAggregate.DequeueUncommittedEvents());
    }
}
```

See full sample in [tests](/src/Ogooreck.Sample.BusinessLogic.Tests/Aggregates/EventSourced/ShoppingCartTests.cs).

### State-based Aggregate tests

You can use `HandlerSpecification` to run event-driven aggregat tests. See the example:

```csharp
using FluentAssertions;
using Ogooreck.BusinessLogic;
using Ogooreck.Sample.BusinessLogic.Tests.Aggregates.StateBased.Pricing;
using Ogooreck.Sample.BusinessLogic.Tests.Aggregates.StateBased.Products;

namespace Ogooreck.Sample.BusinessLogic.Tests.Aggregates.StateBased;

using static ShoppingCartEventsBuilder;
using static ProductItemBuilder;

public class ShoppingCartTests
{
    private readonly Random random = new();

    private readonly HandlerSpecification<ShoppingCart> Spec = Specification.For<ShoppingCart>();

    private class DummyProductPriceCalculator: IProductPriceCalculator
    {
        private readonly decimal price;

        public DummyProductPriceCalculator(decimal price) => this.price = price;

        public IReadOnlyList<PricedProductItem> Calculate(params ProductItem[] productItems) =>
            productItems.Select(pi => PricedProductItem.For(pi, price)).ToList();
    }

    [Fact]
    public void GivenNonExistingShoppingCart_WhenOpenWithValidParams_ThenSucceeds()
    {
        var shoppingCartId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        Spec.Given()
            .When(() => ShoppingCart.Open(shoppingCartId, clientId))
            .Then((state, _) =>
            {
                state.Id.Should().Be(shoppingCartId);
                state.ClientId.Should().Be(clientId);
                state.ProductItems.Should().BeEmpty();
                state.Status.Should().Be(ShoppingCartStatus.Pending);
                state.TotalPrice.Should().Be(0);
            });
    }

    [Fact]
    public void GivenOpenShoppingCart_WhenAddProductWithValidParams_ThenSucceeds()
    {
        var shoppingCartId = Guid.NewGuid();

        var productItem = ValidProductItem();
        var price = random.Next(1, 1000);
        var priceCalculator = new DummyProductPriceCalculator(price);

        Spec.Given(OpenedShoppingCart(shoppingCartId))
            .When(cart => cart.AddProduct(priceCalculator, productItem))
            .Then((state, _) =>
            {
                state.ProductItems.Should().NotBeEmpty();
                state.ProductItems.Single().Should().Be(PricedProductItem.For(productItem, price));
            });
    }
}

public static class ShoppingCartEventsBuilder
{
    public static ShoppingCart OpenedShoppingCart(Guid shoppingCartId)
    {
        var clientId = Guid.NewGuid();

        return ShoppingCart.Open(shoppingCartId, clientId);
    }
}

public static class ProductItemBuilder
{
    private static readonly Random Random = new();

    public static ProductItem ValidProductItem() =>
        ProductItem.From(Guid.NewGuid(), Random.Next(1, 100));
}
```

See full sample in [tests](/src/Ogooreck.Sample.BusinessLogic.Tests/Aggregates/StateBased/ShoppingCartTests.cs).

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
public Task GET_ReturnsShoppingCartDetails() =>
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
public Task GET_ReturnsShoppingCartDetails() =>
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
public async Task POST_WithExistingSKU_ReturnsConflictStatus() =>
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
