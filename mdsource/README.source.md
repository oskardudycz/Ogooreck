[![Twitter Follow](https://img.shields.io/twitter/follow/oskar_at_net?style=social)](https://twitter.com/oskar_at_net) ![Github Actions](https://github.com/oskardudycz/Ogooreck/actions/workflows/dotnet-ci.yml/badge.svg?branch=main) [![blog](https://img.shields.io/badge/blog-event--driven.io-brightgreen)](https://event-driven.io/?utm_source=event_sourcing_net) [![blog](https://img.shields.io/badge/%F0%9F%9A%80-Architecture%20Weekly-important)](https://www.architecture-weekly.com/?utm_source=event_sourcing_net)
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
snippet: BankAccountDeciderTests

See full sample in [tests](/src/dotnet/Ogooreck.BusinessLogic.Tests/Deciders/BankAccountTests.cs).

**F#**
snippet: FSharpBankAccountDeciderTests

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

snippet: StateBasedShoppingCartTests

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

snippet: ApiGetSample

A request step is a plain function of `HttpClient`, so there is no request DSL to learn:

snippet: ApiPostSample

Pass a cancellation token to `Then`. xUnit v3 asks for it with analyzer xUnit1051, so the samples pass `TestContext.Current.CancellationToken`.

### Requests as steps

Write the requests of a module once, as step functions in one class, and reuse them in Given and When. Each test then reads as the scenario it checks:

snippet: ApiRequests

The tests below share one app through the class fixture, and each test uses its own new ids, so they can run in parallel:

snippet: ApiClassFixture

### GET

`Body` matches the fields you give and allows extra ones. Array order and length matter at every level. `ETag`, `Location` and `Headers` check the response headers:

snippet: ApiGetCart

### POST

snippet: ApiPostCreated

### PUT

Set request headers on an `HttpRequestMessage`, for example `If-Match` for an optimistic concurrency check, as `ConfirmCart` in the requests above does:

snippet: ApiPutWithIfMatch

### DELETE

A DELETE is a step like any other, so it can be one of the Given steps:

snippet: ApiDelete

### Using data from results of the previous steps

A step that needs an id the server generated takes a second argument, the step context. `ctx.CreatedId()` reads the id from the `Location` of the first `201 Created` response, and `ctx.CreatedId<T>()` converts it. `ctx.Responses` holds all earlier responses:

snippet: ApiCreatedId

### Errors

`ExpectError` checks an error status, and optionally the problem details, field by field:

snippet: ApiErrors

A failing Given request fails the test, saying which step failed, with its status and body. Wrap a Given request that may fail in `Attempt(...)`.

### Scenarios

Group the Given steps of a scenario into an array named after it, and reuse it across tests. `Given(scenario, more...)` and `Scenario(scenario, more...)` join an array with more steps:

snippet: ApiScenario

### Redirects and pages

Ogooreck doesn't follow redirects and keeps no cookies, so a test sees what the app answered:

snippet: ApiRedirectsAndHtml

`Satisfying(response => ...)` runs your own check on the response.

### Messages and catching up

Ogooreck records what the app publishes and appends through decorators on the app's own services, and after each step it asks each tool whether the app has caught up. You add a tool with one `Use(plugin)` call in the specification's configuration. Keep the plugin in one place, so tests can name its recorder.

`ExpectPublished` checks exactly the messages the When step published, in order, and `ExpectNothingPublished` checks that it published none. `ExpectAppended` and `ExpectNothingAppended` do the same for the events the When step appended. `ExpectCausation` checks which message caused which, through their correlation and causation ids, with `Published`, `Sent` and `Appended` as its nodes.

After each step, Ogooreck runs the plugins' catch-up until a round finds nothing new. So the When step's messages are all there when the expectations run, and the next step sees what the app's consumers did, with no waiting in the test.

#### Wolverine

snippet: WolverinePlugin

The specification uses the plugin, and a [fake clock plugin](#writing-a-plugin) for the tests that move time:

snippet: WolverineShopApi

snippet: WolverineExpectPublished

#### Marten

snippet: MartenPlugin

The specification points the app at the test database. Each copy of the app gets its own schema, so copies don't see each other's data:

snippet: MartenShopApi

snippet: MartenExpectAppended

Catch-up waits for the async projections, so a read right after a write sees it:

snippet: MartenProjectionCatchUp

#### Kafka

snippet: KafkaPlugin

The specification points the app at the broker and gives it its own topic and consumer group. A low `fetch.wait.max.ms` makes the app's consumers answer catch-up fast:

snippet: KafkaShopApi

The When step places an order; the app's shipping consumer handles `OrderPlaced` and publishes `ShipOrder`. Catch-up waits until that consumer is back in `Consume` at the end offsets, so the exact check sees both messages, and the next step sees the order shipped:

snippet: KafkaCatchUp

#### RabbitMQ

snippet: RabbitMQPlugin

The specification points the app at the broker and gives it its own exchange and queue:

snippet: RabbitMQShopApi

Catch-up sends a probe through each queue the app consumes and waits until it comes back with no handler running:

snippet: RabbitMQCatchUp

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

snippet: WolverinePublish

snippet: WolverineAction

#### Message headers

The Kafka and RabbitMQ plugins read the recommended headers without any setup: `messageType`, `messageId`, `correlationId`, `causationId` and `traceparent`. An app with other header names passes its own `MessageHeaders` to `Headers(...)`.

#### Eventually

Use `Eventually(request)` only for an app with no idle signal, for example a read model that a background job updates. It re-sends the When request while its response expectations fail, until they pass or the deadline passes:

snippet: WolverineEventually

### Trace context ties everything to the test

Each step gets a new W3C `traceparent`. Ogooreck sends it with every request and passes it to actions (`ctx.Traceparent`) and publish functions. The app must carry it into the messages and events it writes, as a `traceparent` header. ASP.NET Core already starts the request's `Activity` from it, so the app reads `Activity.Current.Id` when it publishes, as the Kafka, RabbitMQ and Marten samples do.

Ogooreck counts a message or an event as the When step's only when it carries the When step's trace id. Messages of the Given steps, of other tests on the same broker, and late arrivals don't count. That makes parallel tests against a shared broker and database safe. When `ExpectPublished` finds no message that the app did publish, check that the message carries the `traceparent` header.

### Traces and metrics

`ExpectTrace` checks that the spans of the test's steps contain a span tree. `ExpectCorrelatedTrace` checks that all spans of a trace carry the same correlation id:

snippet: ApiTrace

`ExpectMetric` checks how much a counter grew since the baseline that Ogooreck takes after the Given steps:

snippet: ApiMetric

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

snippet: FakeClockPlugin

For another messaging tool, decorate the app's own service with `services.Decorate<TService>(...)`, record its calls with `CallRecorder.Wrap(...)`, and read the recommended headers with `MessageReader`.

### Apps out of process (Aspire)

An app that runs in another process exports its telemetry over OTLP. `OtlpReceiver` is an OTLP/HTTP endpoint in the test: start it first and pass its `Endpoint` to the app. `ForwardTo` sends every export on to another collector, so the app's spans also show up next to the test's:

snippet: AspireSpecification

snippet: AspireTest

Over OTLP, data arrives in periodic exports, so an increase from a Given step can arrive after the When step's baseline. Tag such metrics with something only the When step touches, or start the receiver with `WaitForMetricsExport = true`, which waits for the next export before the baseline and before each check. Other processes may export to the receiver too: when Aspire starts the app with `dotnet run`, the .NET 11 SDK's `dotnet` CLI reads the same `OTEL_*` variables and sends its own metrics. The receiver waits for each process that is exporting, so they don't hide the app's export; set `ServiceName` to the app's `service.name` to wait only for the app.

### Other test frameworks and F#

Ogooreck throws its own `AssertionException`, so it works with any test framework, for example NUnit:

snippet: NUnitTest

From F#, open `Ogooreck.API.ApiSpecification` as a type:

snippet: FSharpApiTest

## Exporting the traces of tests

Ogooreck traces each test with the `Ogooreck` `ActivitySource`: a `Ogooreck Given/When/Then` span with the test's method, file and line, and below it `Before` (starting the app and its plugins), each step with its catch-up, and `Then` with each expectation. Each step's span carries the trace id of the requests it sent, so you can follow the step into the app's spans. The `Ogooreck` meter records how long runs, app starts, steps, catch-ups and expectations take.

To see those spans next to the app's, export them from the test project with OpenTelemetry to any OTLP backend, such as the Aspire dashboard, Jaeger or an OpenTelemetry Collector. This repository's test projects start the export only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set:

snippet: TestTelemetryExport

Use the Batch processor, as above. With the Simple processor, the exporter sends spans in the flow of the test, so its own HTTP calls to the endpoint show up under the test's spans.

In CI, run an OpenTelemetry Collector that writes the spans to a file, and upload the file when a run fails. This repository's .NET workflow does that with this config:

snippet: .github/otel-collector.yaml

Download the file of a failed run with `gh run download <run id> --name spans`, and read it with the MCP server's `ogooreck_import_spans` tool.

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
| `ogooreck_test_run` | Runs a test project, with extra arguments such as a filter. Returns the counts and each failed test with its message and the file and line of its When step. `repeat` runs it until the first failure, for a flaky test; `hangTimeout` aborts a hanging run and names the test and the spans that never ended. |
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
