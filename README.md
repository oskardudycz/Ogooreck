[<img src="https://img.shields.io/badge/LinkedIn-0077B5?style=for-the-badge&logo=linkedin&logoColor=white" height="20px" />](https://www.linkedin.com/in/oskardudycz/) [![Github Sponsors](https://img.shields.io/static/v1?label=Sponsor&message=%E2%9D%A4&logo=GitHub&link=https://github.com/sponsors/event-driven-io)](https://github.com/sponsors/event-driven-io) [![blog](https://img.shields.io/badge/blog-event--driven.io-brightgreen)](https://event-driven.io/?utm_source=event_sourcing_nodejs) [![blog](https://img.shields.io/badge/%F0%9F%9A%80-Architecture%20Weekly-important)](https://www.architecture-weekly.com/?utm_source=event_sourcing_nodejs)
![Github Actions](https://github.com/oskardudycz/Ogooreck/actions/workflows/dotnet-ci.yml/badge.svg?branch=main) [![blog](https://img.shields.io/badge/blog-event--driven.io-brightgreen)](https://event-driven.io/?utm_source=event_sourcing_net) [![blog](https://img.shields.io/badge/%F0%9F%9A%80-Architecture%20Weekly-important)](https://www.architecture-weekly.com/?utm_source=event_sourcing_net)
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

Current available for testing:
- [Business Logic](#business-logic-testing),
- [API](#api-testing), with messages, events, traces and metrics,
- [exporting the traces of tests](#exporting-the-traces-of-tests) and the [MCP server](#mcp-server) for coding agents.

Check also my articles: 
- [Ogooreck introduction](https://event-driven.io/en/ogooreck_sneaky_bdd_testing_framework/),
- [Testing business logic in Event Sourcing, and beyond!](https://event-driven.io/en/testing_event_sourcing/),
- [Writing and testing business logic in F#](https://event-driven.io/en/writing_and_testing_business_logic_in_fsharp/).

## Support

Feel free to [create an issue](https://github.com/oskardudycz/Ogooreck/issues/new) if you have any questions or request for more explanation or samples. I also take **Pull Requests**! See [how to contribute](./CONTRIBUTING.md).

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
<!-- snippet: BankAccountDeciderTests -->
<a id='snippet-BankAccountDeciderTests'></a>
```cs
using Ogooreck.BusinessLogic;

namespace Ogooreck.BusinessLogic.Tests.Deciders;

using static BankAccountEventsBuilder;

public class BankAccountTests
{
    private readonly Random random = new();
    private static readonly DateTimeOffset now = DateTimeOffset.UtcNow;

    private readonly DeciderSpecification<BankAccount> Spec =
        Specification.For<BankAccount>(
            (command, bankAccount) =>
                BankAccountDecider.Handle(() => now, command, bankAccount),
            BankAccount.Evolve
        );

    [Fact]
    public void GivenMissingAccount_WhenOpen_ThenSucceeds()
    {
        var bankAccountId = Guid.NewGuid();
        var accountNumber = Guid.NewGuid().ToString();
        var clientId = Guid.NewGuid();
        var currencyISOCode = "USD";

        Spec.Given()
            .When(new OpenBankAccount(
                bankAccountId, accountNumber, clientId, currencyISOCode))
            .Then(new BankAccountOpened(
                bankAccountId, accountNumber, clientId,
                currencyISOCode, now, 1));
    }

    [Fact]
    public void GivenOpenAccount_WhenRecordDeposit_ThenSucceeds()
    {
        var bankAccountId = Guid.NewGuid();

        var amount = (decimal)random.NextDouble();
        var cashierId = Guid.NewGuid();

        Spec.Given(BankAccountOpened(bankAccountId, now, 1))
            .When(new RecordDeposit(amount, cashierId))
            .Then(new DepositRecorded(
                bankAccountId, amount, cashierId, now, 2));
    }

    [Fact]
    public void GivenClosedAccount_WhenDeposit_ThenThrows()
    {
        var bankAccountId = Guid.NewGuid();

        var amount = (decimal)random.NextDouble();
        var cashierId = Guid.NewGuid();

        Spec.Given(
                BankAccountOpened(bankAccountId, now, 1),
                BankAccountClosed(bankAccountId, now, 2)
            )
            .When(new RecordDeposit(amount, cashierId))
            .ThenThrows<InvalidOperationException>(exception =>
                Assert.Equal("Account is closed!", exception.Message));
    }
}

public static class BankAccountEventsBuilder
{
    public static BankAccountOpened BankAccountOpened(
        Guid bankAccountId, DateTimeOffset now, long version)
    {
        var accountNumber = Guid.NewGuid().ToString();
        var clientId = Guid.NewGuid();
        var currencyISOCode = "USD";

        return new BankAccountOpened(
            bankAccountId, accountNumber, clientId,
            currencyISOCode, now, version);
    }

    public static BankAccountClosed BankAccountClosed(
        Guid bankAccountId, DateTimeOffset now, long version)
    {
        var reason = Guid.NewGuid().ToString();

        return new BankAccountClosed(bankAccountId, reason, now, version);
    }
}
```
<sup><a href='/src/dotnet/Ogooreck.BusinessLogic.Tests/Deciders/BankAccountTests.cs#L1-L90' title='Snippet source file'>snippet source</a> | <a href='#snippet-BankAccountDeciderTests' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

See full sample in [tests](/src/dotnet/Ogooreck.BusinessLogic.Tests/Deciders/BankAccountTests.cs).

**F#**
<!-- snippet: FSharpBankAccountDeciderTests -->
<a id='snippet-FSharpBankAccountDeciderTests'></a>
```fs
module BankAccountTests

open System
open Deciders.BankAccount
open Deciders.BankAccountPrimitives
open Deciders.BankAccountDecider
open Ogooreck.BusinessLogic
open FsCheck.Xunit

let random = Random()

let spec = Specification.For(decide, evolve, Initial)

let BankAccountOpenedWith bankAccountId now version =
    let accountNumber = AccountNumber.parse (Guid.NewGuid().ToString())

    let clientId = ClientId.newId ()

    let currencyISOCode = CurrencyIsoCode.parse "USD"

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
let ``GIVEN missing account WHEN open THEN account is opened``
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
let ``GIVEN open account WHEN deposit THEN deposit is recorded``
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
let ``GIVEN closed account WHEN deposit THEN throws``
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
        .ThenThrows<InvalidOperationException>()
    |> ignore
```
<sup><a href='/src/dotnet/Ogooreck.BusinessLogic.FSharp.Tests/Deciders/BankAccountTests.fs#L1-L113' title='Snippet source file'>snippet source</a> | <a href='#snippet-FSharpBankAccountDeciderTests' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

See full sample in [tests](/src/dotnet/Ogooreck.BusinessLogic.FSharp.Tests/Deciders/BankAccountTests.fs).

### Event-Sourced command handlers

You can use `HandlerSpecification` to run event-sourced command handling tests for pure functions and entities. See the example:

```csharp
using Ogooreck.BusinessLogic;

namespace Ogooreck.BusinessLogic.Tests.Functions.EventSourced;

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

See full sample in [tests](/src/dotnet/Ogooreck.BusinessLogic.Tests/Functions/EventSourced/IncidentTests.cs).

### State-based command handlers

You can use `HandlerSpecification` to run state-based command handling tests for pure functions and entities. See the example:

```csharp
using Ogooreck.BusinessLogic;

namespace Ogooreck.BusinessLogic.Tests.Functions.StateBased;

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

See full sample in [tests](/src/dotnet/Ogooreck.BusinessLogic.Tests/Functions/EventSourced/IncidentTests.cs).


### Event-Driven Aggregate tests

You can use `HandlerSpecification` to run event-driven aggregat tests. See the example:

```csharp
using Ogooreck.BusinessLogic;
using Ogooreck.BusinessLogic.Tests.Aggregates.EventSourced.Core;
using Ogooreck.BusinessLogic.Tests.Aggregates.EventSourced.Pricing;
using Ogooreck.BusinessLogic.Tests.Aggregates.EventSourced.Products;
using Ogooreck.BusinessLogic.Tests.Functions.EventSourced;

namespace Ogooreck.BusinessLogic.Tests.Aggregates.EventSourced;

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

See full sample in [tests](/src/dotnet/Ogooreck.BusinessLogic.Tests/Aggregates/EventSourced/ShoppingCartTests.cs).

### State-based Aggregate tests

You can use `HandlerSpecification` to run event-driven aggregat tests. See the example:

<!-- snippet: StateBasedShoppingCartTests -->
<a id='snippet-StateBasedShoppingCartTests'></a>
```cs
using Ogooreck.BusinessLogic;
using Ogooreck.BusinessLogic.Tests.Aggregates.StateBased.Pricing;
using Ogooreck.BusinessLogic.Tests.Aggregates.StateBased.Products;

namespace Ogooreck.BusinessLogic.Tests.Aggregates.StateBased;

using static ProductItemBuilder;
using static ShoppingCartEventsBuilder;

public class ShoppingCartTests
{
    private readonly Random random = new();

    private readonly HandlerSpecification<ShoppingCart> Spec =
        Specification.For<ShoppingCart>();

    private class DummyProductPriceCalculator: IProductPriceCalculator
    {
        private readonly decimal price;

        public DummyProductPriceCalculator(decimal price) => this.price = price;

        public IReadOnlyList<PricedProductItem> Calculate(
            params ProductItem[] productItems) =>
            productItems.Select(pi => PricedProductItem.For(pi, price))
                .ToList();
    }

    [Fact]
    public void GivenMissingCart_WhenOpen_ThenSucceeds()
    {
        var shoppingCartId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        Spec.Given()
            .When(() => ShoppingCart.Open(shoppingCartId, clientId))
            .Then((state, _) =>
            {
                Assert.Equal(shoppingCartId, state.Id);
                Assert.Equal(clientId, state.ClientId);
                Assert.Empty(state.ProductItems);
                Assert.Equal(ShoppingCartStatus.Pending, state.Status);
                Assert.Equal(0, state.TotalPrice);
            });
    }

    [Fact]
    public void GivenOpenCart_WhenAddProduct_ThenSucceeds()
    {
        var shoppingCartId = Guid.NewGuid();

        var productItem = ValidProductItem();
        var price = random.Next(1, 1000);
        var priceCalculator = new DummyProductPriceCalculator(price);

        Spec.Given(OpenedShoppingCart(shoppingCartId))
            .When(cart => cart.AddProduct(priceCalculator, productItem))
            .Then((state, _) =>
            {
                Assert.NotEmpty(state.ProductItems);
                Assert.Equal(
                    PricedProductItem.For(productItem, price),
                    state.ProductItems.Single());
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
<sup><a href='/src/dotnet/Ogooreck.BusinessLogic.Tests/Aggregates/StateBased/ShoppingCartTests.cs#L1-L86' title='Snippet source file'>snippet source</a> | <a href='#snippet-StateBasedShoppingCartTests' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

See full sample in [tests](/src/dotnet/Ogooreck.BusinessLogic.Tests/Aggregates/StateBased/ShoppingCartTests.cs).

## API Testing

Ogooreck tests your ASP.NET Core app as a black box, the same way as [Emmett's `ApiE2ESpecification`](https://event-driven.io/en/testing_event_driven_application/). Given steps describe what the outside world did before, a single When step is the action under test, and Then expectations check everything the app does in response: the HTTP response, the messages it publishes, the events it appends, which message caused which, the traces it produces and the metrics it changes.

A test never sleeps. After each step, Ogooreck waits until the app has caught up: its consumers have handled what the step published, and its projections have processed what the step appended. So the next step sees the result, and the expectations check exactly what happened.

### Packages

| Package | What it adds |
|---|---|
| [Ogooreck](https://www.nuget.org/packages/Ogooreck/) | `ApiSpecification`: steps, response expectations, traces and metrics, in process or over OTLP. Also the business logic specifications. |
| [Ogooreck.Wolverine](https://www.nuget.org/packages/Ogooreck.Wolverine/) | `WolverinePlugin`: records what the app publishes through Wolverine and waits through Wolverine's tracked sessions. |
| [Ogooreck.Marten](https://www.nuget.org/packages/Ogooreck.Marten/) | `MartenPlugin`: records the events the app appends and waits for its async projections. |
| [Ogooreck.Kafka](https://www.nuget.org/packages/Ogooreck.Kafka/) | `KafkaPlugin.Decorating()`: records what the app produces and waits until its consumers have read it. `KafkaPlugin.Consuming(...)`: records the messages of an app running out of process. |
| [Ogooreck.RabbitMQ](https://www.nuget.org/packages/Ogooreck.RabbitMQ/) | `RabbitMQPlugin.Decorating()`: records what the app publishes and waits until its consumers have handled it. |
| [Ogooreck.Mcp](https://www.nuget.org/packages/Ogooreck.Mcp/) | A dotnet tool: the [MCP server](#mcp-server) that runs your tests for coding agents and explains failures with their spans. |

The packages target .NET 10 and .NET 11. The core package references no broker or database client, so you add only the plugins for the tools your app uses.

Add these usings to your tests:

```csharp
using Ogooreck.API;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
```

### The first test

Inject `ApiSpecification<Program>` as a class fixture. It starts your app once for the test class with `WebApplicationFactory<Program>`, takes the JSON options from the app's services, and listens to the app's traces and metrics in process.

<!-- snippet: ApiGetSample -->
<a id='snippet-ApiGetSample'></a>
```cs
[Fact]
public Task GetProducts()
{
    var ct = TestContext.Current.CancellationToken;

    return API.Given()
        .When(Request(client => client.GetAsync("/api/products", ct)))
        .Then([ExpectResponse(OK)], ct);
}
```
<sup><a href='/src/dotnet/Ogooreck.Api.Tests/API/Responses/ApiTests.cs#L14-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-ApiGetSample' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A request step is a plain function of `HttpClient`, so there is no request DSL to learn:

<!-- snippet: ApiPostSample -->
<a id='snippet-ApiPostSample'></a>
```cs
[Fact]
public Task RegisterProduct()
{
    var ct = TestContext.Current.CancellationToken;

    return API.Given()
        .When(Request(client => client.PostAsJsonAsync("/api/products",
            new RegisterProductRequest("abc-123", "Ogooreck"), ct)))
        .Then([ExpectResponse(Created,
            Location(l => l.StartsWith("/api/products/")))], ct);
}
```
<sup><a href='/src/dotnet/Ogooreck.Api.Tests/API/Responses/ApiTests.cs#L29-L43' title='Snippet source file'>snippet source</a> | <a href='#snippet-ApiPostSample' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Pass a cancellation token to `Then`. xUnit v3 asks for it with analyzer xUnit1051, so the samples pass `TestContext.Current.CancellationToken`.

### Requests as steps

Write the requests of a module once, as step functions in one class, and reuse them in Given and When. Each test then reads as the scenario it checks:

<!-- snippet: ApiRequests -->
<a id='snippet-ApiRequests'></a>
```cs
public static class ShopRequests
{
    public static TestStep CreateCart() =>
        Request(client => client.PostAsync("/carts", null,
            TestContext.Current.CancellationToken));

    public static TestStep AddProduct(Guid cartId, string product) =>
        Request(client => client.PostAsJsonAsync($"/carts/{cartId}/products",
            new { product }, TestContext.Current.CancellationToken));

    public static TestStep GetCart(Guid cartId) =>
        Request(client => client.GetAsync($"/carts/{cartId}",
            TestContext.Current.CancellationToken));

    public static TestStep ConfirmCart(Guid cartId, long version) =>
        Request(client =>
        {
            var request = new HttpRequestMessage(HttpMethod.Put,
                $"/carts/{cartId}/confirmation");
            request.Headers.IfMatch.Add(
                EntityTagHeaderValue.Parse(WeakETag(version)));
            return client.SendAsync(request,
                TestContext.Current.CancellationToken);
        });

    public static TestStep RemoveProduct(Guid cartId, string product,
        long version) =>
        Request(client =>
        {
            var request = new HttpRequestMessage(HttpMethod.Delete,
                $"/carts/{cartId}/products/{product}");
            request.Headers.IfMatch.Add(
                EntityTagHeaderValue.Parse(WeakETag(version)));
            return client.SendAsync(request,
                TestContext.Current.CancellationToken);
        });

    public static TestStep PlaceOrder(Guid cartId) =>
        Request(client => client.PostAsync($"/carts/{cartId}/orders", null,
            TestContext.Current.CancellationToken));
```
<sup><a href='/src/dotnet/Ogooreck.Api.Tests/Fixtures/ShopRequests.cs#L9-L52' title='Snippet source file'>snippet source</a> | <a href='#snippet-ApiRequests' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The tests below share one app through the class fixture, and each test uses its own new ids, so they can run in parallel:

<!-- snippet: ApiClassFixture -->
<a id='snippet-ApiClassFixture'></a>
```cs
public class CartsApiTests(ApiSpecification<Program> api)
    : IClassFixture<ApiSpecification<Program>>
{
    private readonly Guid cartId = Guid.NewGuid();

    private readonly CancellationToken ct =
        TestContext.Current.CancellationToken;
```
<sup><a href='/src/dotnet/Ogooreck.Api.Tests/Carts/CartsApiTests.cs#L9-L19' title='Snippet source file'>snippet source</a> | <a href='#snippet-ApiClassFixture' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### GET

`Body` matches the fields you give and allows extra ones. Array order and length matter at every level. `ETag`, `Location` and `Headers` check the response headers:

<!-- snippet: ApiGetCart -->
<a id='snippet-ApiGetCart'></a>
```cs
[Fact]
public Task AddingAProduct_ShowsItInTheCart() =>
    api.Given(AddProduct(cartId, "socks"))
        .When(GetCart(cartId))
        .Then([
            ExpectResponse(OK,
                Body(new { products = new[] { "socks" } }),
                ETag(WeakETag(1)))
        ], ct);
```
<sup><a href='/src/dotnet/Ogooreck.Api.Tests/Carts/CartsApiTests.cs#L21-L33' title='Snippet source file'>snippet source</a> | <a href='#snippet-ApiGetCart' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### POST

<!-- snippet: ApiPostCreated -->
<a id='snippet-ApiPostCreated'></a>
```cs
[Fact]
public Task PlacingAnOrder_RespondsWithTheOrderAndItsLocation() =>
    api.Given(AddProduct(cartId, "socks"))
        .When(PlaceOrder(cartId))
        .Then([
            ExpectResponse(Created,
                Location($"/orders/{cartId}"),
                Body(new { status = "Placed" }))
        ], ct);
```
<sup><a href='/src/dotnet/Ogooreck.Api.Tests/Carts/CartsApiTests.cs#L76-L88' title='Snippet source file'>snippet source</a> | <a href='#snippet-ApiPostCreated' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### PUT

Set request headers on an `HttpRequestMessage`, for example `If-Match` for an optimistic concurrency check, as `ConfirmCart` in the requests above does:

<!-- snippet: ApiPutWithIfMatch -->
<a id='snippet-ApiPutWithIfMatch'></a>
```cs
[Fact]
public Task ConfirmingACart_WithItsVersion_ConfirmsIt() =>
    api.Given(AddProduct(cartId, "socks"))
        .When(ConfirmCart(cartId, version: 1))
        .Then([ExpectResponse(NoContent)], ct);

[Fact]
public Task ConfirmingACart_WithAnOutdatedVersion_IsRejected() =>
    api.Given(AddProduct(cartId, "socks"), AddProduct(cartId, "shoes"))
        .When(ConfirmCart(cartId, version: 1))
        .Then([ExpectError(PreconditionFailed)], ct);
```
<sup><a href='/src/dotnet/Ogooreck.Api.Tests/Carts/CartsApiTests.cs#L46-L60' title='Snippet source file'>snippet source</a> | <a href='#snippet-ApiPutWithIfMatch' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### DELETE

A DELETE is a step like any other, so it can be one of the Given steps:

<!-- snippet: ApiDelete -->
<a id='snippet-ApiDelete'></a>
```cs
[Fact]
public Task RemovingAProduct_TakesItOutOfTheCart() =>
    api.Given(
            AddProduct(cartId, "socks"),
            AddProduct(cartId, "shoes"),
            RemoveProduct(cartId, "socks", version: 2))
        .When(GetCart(cartId))
        .Then([ExpectResponse(OK, Body(new { products = new[] { "shoes" } }))],
            ct);
```
<sup><a href='/src/dotnet/Ogooreck.Api.Tests/Carts/CartsApiTests.cs#L62-L74' title='Snippet source file'>snippet source</a> | <a href='#snippet-ApiDelete' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Using data from results of the previous steps

A step that needs an id the server generated takes a second argument, the step context. `ctx.CreatedId()` reads the id from the `Location` of the first `201 Created` response, and `ctx.CreatedId<T>()` converts it. `ctx.Responses` holds all earlier responses:

<!-- snippet: ApiCreatedId -->
<a id='snippet-ApiCreatedId'></a>
```cs
[Fact]
public Task CreatedCart_CanBeReadByItsId() =>
    api.Given(CreateCart())
        .When(Request((client, ctx) =>
            client.GetAsync($"/carts/{ctx.CreatedId()}", ct)))
        .Then([ExpectResponse(OK, Body(new { status = "Open" }))], ct);
```
<sup><a href='/src/dotnet/Ogooreck.Api.Tests/Carts/CartsApiTests.cs#L35-L44' title='Snippet source file'>snippet source</a> | <a href='#snippet-ApiCreatedId' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Errors

`ExpectError` checks an error status, and optionally the problem details, field by field:

<!-- snippet: ApiErrors -->
<a id='snippet-ApiErrors'></a>
```cs
[Fact]
public Task GettingAnUnknownCart_IsNotFound() =>
    api.Given()
        .When(GetCart(cartId))
        .Then([ExpectError(NotFound, new { title = "Cart not found" })], ct);
```
<sup><a href='/src/dotnet/Ogooreck.Api.Tests/Carts/CartsApiTests.cs#L90-L98' title='Snippet source file'>snippet source</a> | <a href='#snippet-ApiErrors' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A failing Given request fails the test, saying which step failed, with its status and body. Wrap a Given request that may fail in `Attempt(...)`.

### Scenarios

Group the Given steps of a scenario into an array named after it, and reuse it across tests. `Given(scenario, more...)` and `Scenario(scenario, more...)` join an array with more steps:

<!-- snippet: ApiScenario -->
<a id='snippet-ApiScenario'></a>
```cs
[Fact]
public Task PlacingAnOrder_ForAConfirmedCart_IsAConflict()
{
    TestStep[] confirmedCart =
        [AddProduct(cartId, "socks"), ConfirmCart(cartId, version: 1)];

    return api.Given(confirmedCart)
        .When(PlaceOrder(cartId))
        .Then([ExpectError(Conflict, new { title = "Cart already closed" })],
            ct);
}
```
<sup><a href='/src/dotnet/Ogooreck.Api.Tests/Carts/CartsApiTests.cs#L100-L114' title='Snippet source file'>snippet source</a> | <a href='#snippet-ApiScenario' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Redirects and pages

Ogooreck doesn't follow redirects and keeps no cookies, so a test sees what the app answered:

<!-- snippet: ApiRedirectsAndHtml -->
<a id='snippet-ApiRedirectsAndHtml'></a>
```cs
[Fact]
public Task CartSummary_RedirectsToTheCart() =>
    api.Given(AddProduct(cartId, "socks"))
        .When(GetCartSummary(cartId))
        .Then([ExpectResponse(Found, RedirectsTo($"/carts/{cartId}"))], ct);

[Fact]
public Task OrderPage_ShowsThatTheOrderWasPlaced() =>
    api.Given(AddProduct(cartId, "socks"), PlaceOrder(cartId))
        .When(GetOrderPage(cartId))
        .Then([ExpectResponse(OK, HtmlContaining("Order placed"))], ct);
```
<sup><a href='/src/dotnet/Ogooreck.Api.Tests/Carts/CartsApiTests.cs#L116-L130' title='Snippet source file'>snippet source</a> | <a href='#snippet-ApiRedirectsAndHtml' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`Satisfying(response => ...)` runs your own check on the response.

### Messages and catching up

Ogooreck records what the app publishes and appends through decorators on the app's own services, and after each step it asks each tool whether the app has caught up. You add a tool with one `Use(plugin)` call in the specification's configuration. Keep the plugin in one place, so tests can name its recorder.

`ExpectPublished` checks exactly the messages the When step published, in order, and `ExpectNothingPublished` checks that it published none. `ExpectAppended` and `ExpectNothingAppended` do the same for the events the When step appended. `ExpectCausation` checks which message caused which, through their correlation and causation ids, with `Published`, `Sent` and `Appended` as its nodes.

After each step, Ogooreck runs the plugins' catch-up until a round finds nothing new. So the When step's messages are all there when the expectations run, and the next step sees what the app's consumers did, with no waiting in the test.

#### Wolverine

<!-- snippet: WolverinePlugin -->
<a id='snippet-WolverinePlugin'></a>
```cs
public static class Messaging
{
    public static readonly WolverinePlugin Wolverine = new WolverinePlugin()
        .MessageTypes(type => type.Namespace == typeof(OrderPlaced).Namespace);
}
```
<sup><a href='/src/dotnet/Ogooreck.Wolverine.Tests/Fixtures/Messaging.cs#L6-L14' title='Snippet source file'>snippet source</a> | <a href='#snippet-WolverinePlugin' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The specification uses the plugin, and a [fake clock plugin](#writing-a-plugin) for the tests that move time:

<!-- snippet: WolverineShopApi -->
<a id='snippet-WolverineShopApi'></a>
```cs
public class ShopApi: ApiSpecification<Program>
{
    public ShopApi()
        : base(spec => spec
            .Use(Messaging.Wolverine)
            .Use(((ShopApi)spec).AppClock))
    { }

    public FakeClock AppClock { get; } = new();
}
```
<sup><a href='/src/dotnet/Ogooreck.Wolverine.Tests/Fixtures/ShopApi.cs#L5-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-WolverineShopApi' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: WolverineExpectPublished -->
<a id='snippet-WolverineExpectPublished'></a>
```cs
[Fact]
public Task PlacingAnOrder_PublishesOrderPlaced() =>
    api.Given(AddProduct(cartId, "socks"))
        .When(PlaceOrder(cartId))
        .Then([
            ExpectResponse(Created),
            ExpectPublished(
                new OrderPlaced(cartId) { Total = 5, Products = ["socks"] })
        ], ct);

[Fact]
public Task CheckingOut_PlacesTheOrderAndClosesTheCart() =>
    api.Given(AddProduct(cartId, "socks"))
        .When(CheckOut(cartId))
        .Then([
            ExpectPublished(
                new OrderPlaced(cartId) { Total = 5, Products = ["socks"] },
                new CartClosed(cartId))
        ], ct);

[Fact]
public Task ViewingACart_PublishesNothing() =>
    api.Given(AddProduct(cartId, "socks"))
        .When(GetCart(cartId))
        .Then([ExpectResponse(OK), ExpectNothingPublished()], ct);
```
<sup><a href='/src/dotnet/Ogooreck.Wolverine.Tests/Orders/OrdersApiTests.cs#L16-L44' title='Snippet source file'>snippet source</a> | <a href='#snippet-WolverineExpectPublished' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

#### Marten

<!-- snippet: MartenPlugin -->
<a id='snippet-MartenPlugin'></a>
```cs
public static class EventStore
{
    public static readonly MartenPlugin Marten = new();
}
```
<sup><a href='/src/dotnet/Ogooreck.Marten.Tests/Fixtures/EventStore.cs#L5-L12' title='Snippet source file'>snippet source</a> | <a href='#snippet-MartenPlugin' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The specification points the app at the test database. Each copy of the app gets its own schema, so copies don't see each other's data:

<!-- snippet: MartenShopApi -->
<a id='snippet-MartenShopApi'></a>
```cs
public class ShopApi(PostgresDatabase database): ApiSpecification<Program>(
    factory => factory.WithWebHostBuilder(builder => builder
        .UseSetting("ConnectionStrings:Marten", database.ConnectionString)
        .UseSetting("Marten:Schema", $"shop_{Guid.NewGuid():N}")),
    spec => spec.Use(EventStore.Marten));
```
<sup><a href='/src/dotnet/Ogooreck.Marten.Tests/Fixtures/ShopApi.cs#L5-L13' title='Snippet source file'>snippet source</a> | <a href='#snippet-MartenShopApi' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: MartenExpectAppended -->
<a id='snippet-MartenExpectAppended'></a>
```cs
[Fact]
public Task PlacingAnOrder_AppendsOrderPlaced() =>
    shop.Given(AddProduct(cartId, "socks"))
        .When(PlaceOrder(cartId))
        .Then([ExpectAppended(new OrderPlaced(cartId))], ct);

[Fact]
public Task ShippingAnOrder_AppendsOrderShipped_CausedByOrderPlaced() =>
    shop.Given()
        .When(ShipOrder(cartId))
        .Then([
            ExpectCausation(Appended(new OrderPlaced(cartId),
                Appended(new OrderShipped(cartId))))
        ], ct);
```
<sup><a href='/src/dotnet/Ogooreck.Marten.Tests/Orders/OrdersApiTests.cs#L15-L32' title='Snippet source file'>snippet source</a> | <a href='#snippet-MartenExpectAppended' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Catch-up waits for the async projections, so a read right after a write sees it:

<!-- snippet: MartenProjectionCatchUp -->
<a id='snippet-MartenProjectionCatchUp'></a>
```cs
[Fact]
public Task AddingAProduct_ShowsItInTheCartProjection() =>
    shop.Given(AddProduct(cartId, "socks"))
        .When(GetCart(cartId))
        .Then([ExpectResponse(OK, Body(new { products = new[] { "socks" } }))],
            ct);
```
<sup><a href='/src/dotnet/Ogooreck.Marten.Tests/Orders/OrdersApiTests.cs#L34-L43' title='Snippet source file'>snippet source</a> | <a href='#snippet-MartenProjectionCatchUp' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

#### Kafka

<!-- snippet: KafkaPlugin -->
<a id='snippet-KafkaPlugin'></a>
```cs
public static class Messaging
{
    public static readonly KafkaPlugin<string, string> Kafka =
        KafkaPlugin.Decorating().MessageTypes(typeof(OrderPlaced),
            typeof(CartClosed), typeof(ShipOrder), typeof(OrderPaid),
            typeof(StockChecked));
}
```
<sup><a href='/src/dotnet/Ogooreck.Kafka.Tests/Fixtures/Messaging.cs#L6-L16' title='Snippet source file'>snippet source</a> | <a href='#snippet-KafkaPlugin' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The specification points the app at the broker and gives it its own topic and consumer group. A low `fetch.wait.max.ms` makes the app's consumers answer catch-up fast:

<!-- snippet: KafkaShopApi -->
<a id='snippet-KafkaShopApi'></a>
```cs
public class ShopApi(KafkaBroker broker): ApiSpecification<Program>(
    factory => factory.WithWebHostBuilder(builder => builder
        .UseSetting("Kafka:BootstrapServers", broker.BootstrapServers)
        .UseSetting("Kafka:Topic", Topic)
        .UseSetting("Kafka:ShippingGroupId", "shop-shipping")
        .UseSetting("Kafka:Consumer:fetch.wait.max.ms", "10")),
    spec => spec.Use(Messaging.Kafka))
{
    public const string Topic = "shop";
}
```
<sup><a href='/src/dotnet/Ogooreck.Kafka.Tests/Fixtures/ShopApi.cs#L5-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-KafkaShopApi' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The When step places an order; the app's shipping consumer handles `OrderPlaced` and publishes `ShipOrder`. Catch-up waits until that consumer is back in `Consume` at the end offsets, so the exact check sees both messages, and the next step sees the order shipped:

<!-- snippet: KafkaCatchUp -->
<a id='snippet-KafkaCatchUp'></a>
```cs
[Fact]
public Task PlacingAnOrder_ShipsIt() =>
    shop.Given()
        .When(PlaceOrderToShip(cartId))
        .Then([
            ExpectPublished(new OrderPlaced(cartId), new ShipOrder(cartId)),
            ExpectCausation(Published(new OrderPlaced(cartId),
                Sent(new ShipOrder(cartId))))
        ], ct);

[Fact]
public Task AfterPlacingAnOrder_TheCartShowsItShipped() =>
    shop.Given(PlaceOrderToShip(cartId))
        .When(GetCart(cartId))
        .Then([ExpectResponse(OK, Body(new { cartId, shipped = true }))], ct);
```
<sup><a href='/src/dotnet/Ogooreck.Kafka.Tests/Shipping/ShippingApiTests.cs#L15-L33' title='Snippet source file'>snippet source</a> | <a href='#snippet-KafkaCatchUp' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

#### RabbitMQ

<!-- snippet: RabbitMQPlugin -->
<a id='snippet-RabbitMQPlugin'></a>
```cs
public static class Messaging
{
    public static readonly RabbitMQPlugin RabbitMQ = RabbitMQPlugin.Decorating()
        .MessageTypes(typeof(OrderPlaced), typeof(ShipOrder));
}
```
<sup><a href='/src/dotnet/Ogooreck.RabbitMQ.Tests/Fixtures/Messaging.cs#L6-L14' title='Snippet source file'>snippet source</a> | <a href='#snippet-RabbitMQPlugin' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The specification points the app at the broker and gives it its own exchange and queue:

<!-- snippet: RabbitMQShopApi -->
<a id='snippet-RabbitMQShopApi'></a>
```cs
public class ShopApi(RabbitMQBroker broker): ApiSpecification<Program>(
    factory => factory.WithWebHostBuilder(builder => builder
        .UseSetting("RabbitMQ:ConnectionString", broker.ConnectionString)
        .UseSetting("RabbitMQ:Exchange", "shop")
        .UseSetting("RabbitMQ:ShippingQueue", "shop-shipping")),
    spec => spec.Use(Messaging.RabbitMQ));
```
<sup><a href='/src/dotnet/Ogooreck.RabbitMQ.Tests/Fixtures/ShopApi.cs#L5-L14' title='Snippet source file'>snippet source</a> | <a href='#snippet-RabbitMQShopApi' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Catch-up sends a probe through each queue the app consumes and waits until it comes back with no handler running:

<!-- snippet: RabbitMQCatchUp -->
<a id='snippet-RabbitMQCatchUp'></a>
```cs
[Fact]
public Task PlacingAnOrder_ShipsIt() =>
    shop.Given()
        .When(PlaceOrder(orderId))
        .Then([
            ExpectPublished(new OrderPlaced(orderId), new ShipOrder(orderId)),
            ExpectCausation(Published(new OrderPlaced(orderId),
                Sent(new ShipOrder(orderId))))
        ], ct);

[Fact]
public Task AfterPlacingAnOrder_TheOrderIsShipped() =>
    shop.Given(PlaceOrder(orderId))
        .When(GetOrder(orderId))
        .Then([ExpectResponse(OK, Body(new { status = "Shipped" }))], ct);
```
<sup><a href='/src/dotnet/Ogooreck.RabbitMQ.Tests/Shipping/ShippingApiTests.cs#L15-L33' title='Snippet source file'>snippet source</a> | <a href='#snippet-RabbitMQCatchUp' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

#### What each plugin waits for

| Plugin | Catches up when |
|---|---|
| `WolverinePlugin` | Wolverine's tracked session for the step has finished. Run those tests serially, as Wolverine's docs keep tracked-session tests in one collection. |
| `MartenPlugin` | Marten's async projections have processed the step's events. `WithoutCatchUp()` turns it off. |
| `KafkaPlugin.Decorating()` | Every consumer the app gets from DI is back in `Consume` with its positions at the end offsets. |
| `RabbitMQPlugin.Decorating()` | A probe message sent to each queue the app consumes has come back, and no handler is running or requeued. |
| `KafkaPlugin.Consuming(...)` | Never: it reads the topics from the test, for an app out of process. `ExpectPublished` then passes as soon as the expected messages arrive, in that order with others between them, and `ExpectNothingPublished` fails at once, saying it needs a catch-up. |

The plugins wait only for the consumers that the app gets from DI. Ogooreck creates no topics, queues or exchanges: the app or its AppHost creates them, or, as in these tests, the broker fixture. Each copy of the app runs its own consumers, so two copies in one Kafka group or on one RabbitMQ queue split the messages. Give each copy its own group or queue names, as the specifications above do, or run one app per test project through an assembly fixture.

A consumer the plugin can't wait for, such as a loop that hands messages to other tasks, needs your own catch-up: `CatchUp(ct => ...)` runs after each step, and `CatchUp((step, ct) => ...)` wraps it. Give it to one test with `api.With(spec => spec.CatchUp(...))`, which runs on the same app with the test setup changed.

#### Messages from other systems, actions and time

`Publish(...)` puts messages on the app's bus, as another module or system would. `Action(ctx => ...)` runs any code, such as advancing a fake clock:

<!-- snippet: WolverinePublish -->
<a id='snippet-WolverinePublish'></a>
```cs
[Fact]
public Task ConfirmedPayment_MarksTheOrderPaid() =>
    api.Given(AddProduct(cartId, "socks"), PlaceOrder(cartId))
        .When(Publish(new PaymentConfirmed(cartId)))
        .Then([ExpectPublished(new OrderPaid(cartId))], ct);
```
<sup><a href='/src/dotnet/Ogooreck.Wolverine.Tests/Orders/OrdersApiTests.cs#L46-L54' title='Snippet source file'>snippet source</a> | <a href='#snippet-WolverinePublish' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: WolverineAction -->
<a id='snippet-WolverineAction'></a>
```cs
[Fact]
public async Task AnAbandonedCart_ExpiresAfterADay()
{
    await using var shop = new ShopApi();

    await shop.Given(AddProduct(cartId, "socks"))
        .When(Action(_ =>
        {
            shop.AppClock.Advance(TimeSpan.FromDays(1));
            return Task.CompletedTask;
        }))
        .Then([ExpectPublished(new CartExpired(cartId))], ct);
}
```
<sup><a href='/src/dotnet/Ogooreck.Wolverine.Tests/Orders/OrdersApiTests.cs#L56-L72' title='Snippet source file'>snippet source</a> | <a href='#snippet-WolverineAction' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

#### Message headers

The Kafka and RabbitMQ plugins read the recommended headers without any setup: `messageType`, `messageId`, `correlationId`, `causationId` and `traceparent`. An app with other header names passes its own `MessageHeaders` to `Headers(...)`.

#### Eventually

Use `Eventually(request)` only for an app with no idle signal, for example a read model that a background job updates. It re-sends the When request while its response expectations fail, until they pass or the deadline passes:

<!-- snippet: WolverineEventually -->
<a id='snippet-WolverineEventually'></a>
```cs
[Fact]
public Task PlacedOrder_EventuallyShowsAsPlaced() =>
    api.Given(AddProduct(cartId, "socks"), PlaceOrder(cartId))
        .When(Eventually(GetOrder(cartId)))
        .Then([ExpectResponse(OK, Body(new { status = "Placed" }))], ct);
```
<sup><a href='/src/dotnet/Ogooreck.Wolverine.Tests/Orders/OrdersApiTests.cs#L74-L82' title='Snippet source file'>snippet source</a> | <a href='#snippet-WolverineEventually' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Trace context ties everything to the test

Each step gets a new W3C `traceparent`. Ogooreck sends it with every request and passes it to actions (`ctx.Traceparent`) and publish functions. The app must carry it into the messages and events it writes, as a `traceparent` header. ASP.NET Core already starts the request's `Activity` from it, so the app reads `Activity.Current.Id` when it publishes, as the Kafka, RabbitMQ and Marten samples do.

Ogooreck counts a message or an event as the When step's only when it carries the When step's trace id. Messages of the Given steps, of other tests on the same broker, and late arrivals don't count. That makes parallel tests against a shared broker and database safe. When `ExpectPublished` finds no message that the app did publish, check that the message carries the `traceparent` header.

### Traces and metrics

`ExpectTrace` checks that the spans of the test's steps contain a span tree. `ExpectCorrelatedTrace` checks that all spans of a trace carry the same correlation id:

<!-- snippet: ApiTrace -->
<a id='snippet-ApiTrace'></a>
```cs
[Fact]
public Task PlacingAnOrder_PublishesAndHandlesOrderPlaced() =>
    api.Given(AddProduct(cartId, "socks"))
        .When(PlaceOrder(cartId))
        .Then([
            ExpectTrace(Span("POST /carts/{id}/orders",
                Span("publish OrderPlaced",
                    Span("handle OrderPlaced"))))
        ], ct);
```
<sup><a href='/src/dotnet/Ogooreck.Api.Tests/Carts/CartsApiTests.cs#L132-L144' title='Snippet source file'>snippet source</a> | <a href='#snippet-ApiTrace' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`ExpectMetric` checks how much a counter grew since the baseline that Ogooreck takes after the Given steps:

<!-- snippet: ApiMetric -->
<a id='snippet-ApiMetric'></a>
```cs
[Fact]
public Task PlacingAnOrder_CountsAWebOrder() =>
    api.Given(AddProduct(cartId, "socks"))
        .When(PlaceOrder(cartId))
        .Then([
            ExpectMetric("orders.placed",
                new Dictionary<string, string>
                {
                    ["channel"] = "web",
                    ["cart.id"] = cartId.ToString()
                },
                increase: 1)
        ], ct);
```
<sup><a href='/src/dotnet/Ogooreck.Api.Tests/Carts/CartsApiTests.cs#L146-L162' title='Snippet source file'>snippet source</a> | <a href='#snippet-ApiMetric' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Measurements without a trace id can't be tied to a test: observable instruments, counters recorded outside any request's activity, and anything that arrives over OTLP. Filter them by a tag unique to the test, as the `cart.id` tag above, or run those tests serially.

### Setting up the app

Each configuration call replaces its default, before the first step:

| Setup | What it does |
|---|---|
| `IClassFixture<ApiSpecification<Program>>`, `ApiSpecification.For<Program>()` | Starts `WebApplicationFactory<Program>` on the first step. Telemetry in process, deadline 30 seconds. |
| `For<Program>(factory => factory.WithWebHostBuilder(...))`, or a subclass passing it to the base constructor | Changes the host, for example its settings or services. |
| `.Use(plugin)` | Adds a messaging tool or event store. Repeatable. |
| `.Deadline(TimeSpan)` | How long an expectation or catch-up may wait before it fails. It only bounds failures. |
| `.Clock(TimeProvider)`, `.Json(JsonSerializerOptions)`, `.Telemetry(source)` | The clock of the deadline, the JSON options of `Body`, and where traces and metrics come from (`TelemetrySources.Off` turns them off). |
| `api.With(spec => ...)` | Another test setup on the same running app. |
| `api.WithNewApp(spec => ...)` | A separate app, for a plugin that changes the app's services. |
| `ApiSpecification.For(async () => address)` | An app that runs out of process, for example in Aspire. |
| `ApiSpecification.For(host)` | A host you built yourself with `UseTestServer()`. |

When a project uses one setup everywhere, write it once in a subclass, such as `ShopApi: ApiSpecification<Program>`, and share it through your test framework's fixtures: `IClassFixture`, a collection or assembly fixture, NUnit's `SetUpFixture` or MSTest's `AssemblyInitialize`.

### Writing a plugin

A plugin implements `ISpecificationPlugin`. Its `Register` method adds services to the test host, recorders (`Recording`, `Appending`), catch-up and a publish function:

<!-- snippet: FakeClockPlugin -->
<a id='snippet-FakeClockPlugin'></a>
```cs
public sealed class FakeClock: ISpecificationPlugin
{
    private readonly FakeTimeProvider time = new();

    public void Advance(TimeSpan by) => time.Advance(by);

    public void Register(SpecificationPluginRegistration registration) =>
        registration.Services(services =>
            services.AddSingleton<TimeProvider>(time));
}
```
<sup><a href='/src/dotnet/Ogooreck.Wolverine.Tests/Fixtures/FakeClock.cs#L7-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-FakeClockPlugin' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

For another messaging tool, decorate the app's own service with `services.Decorate<TService>(...)`, record its calls with `CallRecorder.Wrap(...)`, and read the recommended headers with `MessageReader`.

### Apps out of process (Aspire)

An app that runs in another process exports its telemetry over OTLP. `OtlpReceiver` is an OTLP/HTTP endpoint in the test: start it first and pass its `Endpoint` to the app. `ForwardTo` sends every export on to another collector, so the app's spans also show up next to the test's:

<!-- snippet: AspireSpecification -->
<a id='snippet-AspireSpecification'></a>
```cs
public sealed class AspireApi: ApiSpecification, IAsyncDisposable
{
    public static readonly OtlpReceiver Otlp = OtlpReceiver.Start(
        new OtlpReceiverOptions { ForwardTo = TestRunCollector.Endpoint });

    public static readonly AspireApp App = new(Otlp, withKafka: true);

    public AspireApi() : base(App.Api, spec => spec
        .Telemetry(Otlp)
        .Use(App.Kafka)
        .Deadline(TimeSpan.FromSeconds(30)))
    {
    }
```
<sup><a href='/src/dotnet/Ogooreck.Aspire.Tests/Fixtures/AspireApi.cs#L5-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-AspireSpecification' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: AspireTest -->
<a id='snippet-AspireTest'></a>
```cs
[Fact]
public async Task PlacingAnOrder_ExportsTheRequestSpanOverOtlp()
{
    var cartId = Guid.NewGuid();

    await api.Given(AddProduct(cartId, "socks"))
        .When(PlaceOrder(cartId))
        .Then([
            ExpectResponse(Created),
            ExpectTrace(Span("POST /carts/{id}/orders", Cart(cartId)))
        ], Ct);
}
```
<sup><a href='/src/dotnet/Ogooreck.Aspire.Tests/AspireApiTests.cs#L19-L34' title='Snippet source file'>snippet source</a> | <a href='#snippet-AspireTest' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Over OTLP, data arrives in periodic exports, so an increase from a Given step can arrive after the When step's baseline. Tag such metrics with something only the When step touches, or start the receiver with `WaitForMetricsExport = true`, which waits for the next export before the baseline and before each check. Other processes may export to the receiver too: when Aspire starts the app with `dotnet run`, the .NET 11 SDK's `dotnet` CLI reads the same `OTEL_*` variables and sends its own metrics. The receiver waits for each process that is exporting, so they don't hide the app's export; set `ServiceName` to the app's `service.name` to wait only for the app.

### Other test frameworks and F#

Ogooreck throws its own `AssertionException`, so it works with any test framework, for example NUnit:

<!-- snippet: NUnitTest -->
<a id='snippet-NUnitTest'></a>
```cs
[Test]
public async Task RegisteringAProduct_RespondsWithCreated()
{
    var ct = TestContext.CurrentContext.CancellationToken;
    await using var api = For<Program>();

    await api.Given()
        .When(Request(client => client.PostAsJsonAsync("/api/products",
            new RegisterProductRequest($"nunit-{Guid.NewGuid()}", "Ogooreck"),
            ct)))
        .Then([ExpectResponse(Created)], ct);
}
```
<sup><a href='/src/dotnet/Ogooreck.NUnit.Tests/ProductTests.cs#L11-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-NUnitTest' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

From F#, open `Ogooreck.API.ApiSpecification` as a type:

<!-- snippet: FSharpApiTest -->
<a id='snippet-FSharpApiTest'></a>
```fs
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
```
<sup><a href='/src/dotnet/Ogooreck.Wolverine.FSharp.Tests/PlacingAnOrderTests.fs#L11-L34' title='Snippet source file'>snippet source</a> | <a href='#snippet-FSharpApiTest' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Exporting the traces of tests

Ogooreck traces each test with the `Ogooreck` `ActivitySource`: a `Ogooreck Given/When/Then` span with the test's method, file and line, and below it `Before` (starting the app and its plugins), each step with its catch-up, and `Then` with each expectation. Each step's span carries the trace id of the requests it sent, so you can follow the step into the app's spans. The `Ogooreck` meter records how long runs, app starts, steps, catch-ups and expectations take.

To see those spans next to the app's, export them from the test project with OpenTelemetry to any OTLP backend, such as the Aspire dashboard, Jaeger or an OpenTelemetry Collector. This repository's test projects start the export only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set:

<!-- snippet: TestTelemetryExport -->
<a id='snippet-TestTelemetryExport'></a>
```cs
[ModuleInitializer]
internal static void StartWhenAnEndpointIsSet()
{
    var endpoint =
        Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
    if (string.IsNullOrEmpty(endpoint))
        return;

    var resource = ResourceBuilder.CreateDefault()
        .AddService(typeof(TestTelemetryExport).Assembly.GetName().Name!);
    traces = Sdk.CreateTracerProviderBuilder()
        .SetResourceBuilder(resource)
        .AddSource("*")
        .AddOtlpExporter(options =>
        {
            options.ExportProcessorType = ExportProcessorType.Batch;
            options.BatchExportProcessorOptions.ScheduledDelayMilliseconds = 50;
            options.BatchExportProcessorOptions.MaxQueueSize = 65536;
        })
        .Build();
    metrics = Sdk.CreateMeterProviderBuilder()
        .SetResourceBuilder(resource)
        .AddMeter("Ogooreck")
        .AddOtlpExporter((_, reader) => reader
            .PeriodicExportingMetricReaderOptions
            .ExportIntervalMilliseconds = 1000)
        .Build();
    AppDomain.CurrentDomain.ProcessExit += (_, _) =>
    {
        metrics?.Dispose();
        traces?.Dispose();
    };
}
```
<sup><a href='/src/dotnet/Shared/TestTelemetryExport.cs#L14-L50' title='Snippet source file'>snippet source</a> | <a href='#snippet-TestTelemetryExport' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Use the Batch processor, as above. With the Simple processor, the exporter sends spans in the flow of the test, so its own HTTP calls to the endpoint show up under the test's spans.

In CI, run an OpenTelemetry Collector that writes the spans to a file, and upload the file when a run fails. This repository's .NET workflow does that with this config:

<!-- snippet: .github/otel-collector.yaml -->
<a id='snippet-.github/otel-collector.yaml'></a>
```yaml
receivers:
  otlp:
    protocols:
      http:
        endpoint: 0.0.0.0:4318
exporters:
  file:
    path: /spans/spans.pb
    format: proto
service:
  pipelines:
    traces:
      receivers: [otlp]
      exporters: [file]
```
<sup><a href='#snippet-.github/otel-collector.yaml' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

This repository uploads one artifact per target framework. Download the file of a failed run with `gh run download <run id> --name spans-net11.0`, and read it with the MCP server's `ogooreck_import_spans` tool.

## MCP server

`Ogooreck.Mcp` is an [MCP](https://modelcontextprotocol.io) server that runs .NET tests for coding agents and explains each failure with the spans of the test, Ogooreck and the tested app. Install it as a dotnet tool:

```shell
dotnet tool install --global Ogooreck.Mcp
```

Then add it to your agent's config, for example `.mcp.json` for Claude Code:

```json
{
  "mcpServers": {
    "ogooreck": { "command": "ogooreck-mcp" }
  }
}
```

The same for Codex in `.codex/config.toml`:

```toml
[mcp_servers.ogooreck]
command = "ogooreck-mcp"
```

And for OpenCode in `opencode.json`:

```json
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "ogooreck": { "type": "local", "command": ["ogooreck-mcp"], "enabled": true }
  }
}
```

The server sets `OTEL_EXPORTER_OTLP_ENDPOINT` for the test run and receives the spans, so the test project needs an export like the [one above](#exporting-the-traces-of-tests). Its tools:

| Tool | What it does |
|---|---|
| `ogooreck_test_run` | Runs a test project, VSTest or Microsoft.Testing.Platform, with extra arguments such as a filter or `--framework`. Returns the counts, the frameworks the tests ran on, the slowest tests, build errors, and each failed test with its message and the file and line of its When step. `repeat` runs it until the first failure, for a flaky test; `hangTimeout` aborts a hanging run and names the test and the spans that never ended; `coverage` names a library and returns its uncovered lines and partly covered branches, collected with Microsoft's code coverage. |
| `ogooreck_test_spans` | The span tree of a test: Before, Given, When and Then with their catch-ups and expectations, and the app's spans under them. |
| `ogooreck_scenario_diagram` | A Mermaid sequence diagram of a test's steps and the messages they caused, one per test process, so a test run on two frameworks gets two. |
| `ogooreck_find_spans` | Searches the spans by name, error or attribute. |
| `ogooreck_concurrent_spans` | What else ran during a span, also in other tests, for races. |
| `ogooreck_slowest_steps` | The slowest steps, catch-ups, app starts and expectations. |
| `ogooreck_export_spans` | Writes all spans to a JSON file. |
| `ogooreck_import_spans` | Reads the spans of a CI run from the Collector's file, for the other tools. |

## Agent skill

The Ogooreck package ships an [agent skill](https://agentskills.io), `skills/ogooreck-api-tests/SKILL.md`, that teaches coding agents how to write and debug Ogooreck tests, with the examples from this README. `dotnet restore` puts it in the NuGet cache next to the package, matching the version you use. NuGet's [`dotnet-package-skills`](https://github.com/NuGet/Client.Tools/tree/main/dotnet-package-skills) tool copies the skills of your project's packages to where agents read them:

```shell
dotnet tool install --global dotnet-package-skills --prerelease
dotnet-package-skills install --destination .claude/skills
dotnet-package-skills install
```

The first `install` is for Claude Code, which reads `.claude/skills`; the second copies to `.agents/skills`, which Codex and OpenCode read. After you upgrade Ogooreck, run `install` again to replace the copies with the new version's skill, and `dotnet-package-skills uninstall --stale` removes the skills of packages the project no longer references. The tool reads the project's direct package references, so run it in the test project that references Ogooreck.

## Credits

Special thanks go to:
- Simon Cropp for [MarkdownSnippets](https://github.com/SimonCropp/MarkdownSnippets) that I'm using for plugging snippets to markdown,
- Adam Ralph for [BullsEye](https://github.com/adamralph/bullseye), which I'm using to make the build process seamless,
- [Babu Annamalai](https://mysticmind.dev/) that did a similar build setup in [Marten](https://martendb.io/) which I inspired a lot,
- Dennis Doomen for [Fluent Assertions](https://fluentassertions.com/), which I'm using for internal assertions, especially checking the response body.
