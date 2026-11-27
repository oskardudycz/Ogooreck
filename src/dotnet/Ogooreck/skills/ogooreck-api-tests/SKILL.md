---
name: ogooreck-api-tests
description: Write and debug Ogooreck API tests for ASP.NET Core apps in .NET, Given/When/Then tests that check the HTTP response, published messages, appended events, causation, traces and metrics. Use when a project references the Ogooreck NuGet packages, when writing or fixing tests that use ApiSpecification, or when such a test fails, hangs or is flaky.
---

# Ogooreck API tests

Ogooreck tests an app as a black box. Given steps are what the outside world did before, one When step is the action under test, and Then expectations check what the app did in response. After each step, Ogooreck waits until the app has caught up, so a test never sleeps.

## Packages

- `Ogooreck`: `ApiSpecification`, steps, response expectations, traces and metrics.
- `Ogooreck.Wolverine`, `Ogooreck.Marten`, `Ogooreck.Kafka`, `Ogooreck.RabbitMQ`: one plugin per tool, added with `Use(plugin)`. Add only the plugins for the tools the app uses.
- `Ogooreck.Mcp`: a dotnet tool, the MCP server that runs tests and explains failures with spans.

Usings:

```csharp
using Ogooreck.API;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
```

## How to write a test

1. Name the test after the business behavior, as the reader sees it: `PlacingAnOrder_ShipsIt`, `ConfirmingACart_WithAnOutdatedVersion_IsRejected`. Not after Ogooreck or the method it calls.
2. Write each request once, as a step function in one class per module, and reuse it in Given and When.
3. Set up state only through steps: `Request`, `Attempt` (a Given request that may fail), `Action` (code, such as advancing a fake clock) and `Publish` (a message from another system). Never write into the app's storage or call its services from a test.
4. Give each test new ids (`Guid.NewGuid()`), so tests share one app and one database and run in parallel.
5. Pass `TestContext.Current.CancellationToken` (xUnit v3) to `Then` and to each request.
6. Never add `Task.Delay`, polling or retries. If the next step doesn't see a result, the app's consumer isn't caught up: add the plugin, or a catch-up of your own. Use `Eventually` only for an app with no idle signal.

### Requests as steps

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

### A shared app

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

### Responses

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

`Body` matches only the fields you give; array order and length matter. Other checks: `Location(...)`, `ETag(WeakETag(n))`, `Headers(...)`, `RedirectsTo(...)`, `HtmlContaining(...)`, `Satisfying(response => ...)`.

### Ids the server generated

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

### Errors and scenarios

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

## Messages, events and catching up

Keep each plugin in one place, and write one specification class that points the app at the test's broker or database, with its own topic, consumer group, queue or schema, and passes the plugin to `Use(...)`:

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

`ExpectPublished` checks exactly the messages the When step published, in order; `ExpectNothingPublished` checks that it published none. `ExpectAppended` and `ExpectNothingAppended` do the same for events. `ExpectCausation(Published(message, Sent(caused)))` checks which message caused which.

Catch-up waits for the app's consumers after each step, so the When step's messages are all there, and the next step sees what the consumer did:

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

Messages from other systems, and time:

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

Only for an app with no idle signal:

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

What each plugin waits for: Wolverine, its tracked session (run those tests serially); Marten, its async projections; Kafka (`Decorating`), every consumer the app gets from DI back in `Consume` at the end offsets (set a low `fetch.wait.max.ms` in the test app); RabbitMQ, a probe per consumed queue and no running or requeued handler. `KafkaPlugin.Consuming(...)`, for an app out of process, doesn't wait: `ExpectPublished` passes once the messages arrive, and `ExpectNothingPublished` fails. A consumer the plugin can't wait for needs `CatchUp(ct => ...)`; give it to one test with `api.With(spec => spec.CatchUp(...))`.

## Trace context

Each step sends a new `traceparent`. The app must carry it into every message and event it writes, as a `traceparent` header, usually from `Activity.Current.Id`. Ogooreck counts only the messages and events that carry the When step's trace id, which keeps parallel tests on a shared broker apart. When `ExpectPublished` finds nothing although the app published, check that the message carries `traceparent`.

## Traces and metrics

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

Measurements without a trace id (observable instruments, counters outside a request, anything over OTLP) can't be tied to a test: filter by a tag unique to the test, such as `cart.id`, or run those tests serially.

## When a test fails

1. Read the failure message first. It names the step (`When request POST /carts/{id}/orders`), what was expected, what was found, and the response body or the numbered list of messages.
2. If the MCP server is configured, run the test through `ogooreck_test_run` with a filter, then read its spans with `ogooreck_test_spans`, `ogooreck_find_spans` and `ogooreck_scenario_diagram`. For a slow test use `ogooreck_slowest_steps`; for a flaky test pass `repeat`; for a hanging test pass `hangTimeout`; for a race between tests use `ogooreck_concurrent_spans`; for a CI run use `ogooreck_import_spans` with the Collector's file; to find code no test reaches, pass `coverage` with the library name.
3. The spans come from the test project's OpenTelemetry export, started when `OTEL_EXPORTER_OTLP_ENDPOINT` is set, with the Batch processor:

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

4. A test that passes through the MCP server but fails with plain `dotnet test` (or the other way round) depends on whether something listens to every `ActivitySource`. Run it both ways.
