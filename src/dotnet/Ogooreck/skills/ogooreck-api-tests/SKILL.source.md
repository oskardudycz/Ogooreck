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

snippet: ApiRequests

### A shared app

snippet: ApiClassFixture

### Responses

snippet: ApiGetCart

snippet: ApiPostCreated

snippet: ApiPutWithIfMatch

snippet: ApiDelete

`Body` matches only the fields you give; array order and length matter. Other checks: `Location(...)`, `ETag(WeakETag(n))`, `Headers(...)`, `RedirectsTo(...)`, `HtmlContaining(...)`, `Satisfying(response => ...)`.

### Ids the server generated

snippet: ApiCreatedId

### Errors and scenarios

snippet: ApiErrors

snippet: ApiScenario

## Messages, events and catching up

Keep each plugin in one place, and write one specification class that points the app at the test's broker or database, with its own topic, consumer group, queue or schema, and passes the plugin to `Use(...)`:

snippet: KafkaPlugin

snippet: KafkaShopApi

snippet: MartenShopApi

`ExpectPublished` checks exactly the messages the When step published, in order; `ExpectNothingPublished` checks that it published none. `ExpectAppended` and `ExpectNothingAppended` do the same for events. `ExpectCausation(Published(message, Sent(caused)))` checks which message caused which.

Catch-up waits for the app's consumers after each step, so the When step's messages are all there, and the next step sees what the consumer did:

snippet: KafkaCatchUp

snippet: MartenProjectionCatchUp

Messages from other systems, and time:

snippet: WolverinePublish

snippet: WolverineAction

Only for an app with no idle signal:

snippet: WolverineEventually

What each plugin waits for: Wolverine, its tracked session (run those tests serially); Marten, its async projections; Kafka (`Decorating`), every consumer the app gets from DI back in `Consume` at the end offsets (set a low `fetch.wait.max.ms` in the test app); RabbitMQ, a probe per consumed queue and no running or requeued handler. `KafkaPlugin.Consuming(...)`, for an app out of process, doesn't wait: `ExpectPublished` passes once the messages arrive, and `ExpectNothingPublished` fails. A consumer the plugin can't wait for needs `CatchUp(ct => ...)`; give it to one test with `api.With(spec => spec.CatchUp(...))`.

## Trace context

Each step sends a new `traceparent`. The app must carry it into every message and event it writes, as a `traceparent` header, usually from `Activity.Current.Id`. Ogooreck counts only the messages and events that carry the When step's trace id, which keeps parallel tests on a shared broker apart. When `ExpectPublished` finds nothing although the app published, check that the message carries `traceparent`.

## Traces and metrics

snippet: ApiTrace

snippet: ApiMetric

Measurements without a trace id (observable instruments, counters outside a request, anything over OTLP) can't be tied to a test: filter by a tag unique to the test, such as `cart.id`, or run those tests serially.

## When a test fails

1. Read the failure message first. It names the step (`When request POST /carts/{id}/orders`), what was expected, what was found, and the response body or the numbered list of messages.
2. If the MCP server is configured, run the test through `ogooreck_test_run` with a filter, then read its spans with `ogooreck_test_spans`, `ogooreck_find_spans` and `ogooreck_scenario_diagram`. For a slow test use `ogooreck_slowest_steps`; for a flaky test pass `repeat`; for a hanging test pass `hangTimeout`; for a race between tests use `ogooreck_concurrent_spans`; for a CI run use `ogooreck_import_spans` with the Collector's file.
3. The spans come from the test project's OpenTelemetry export, started when `OTEL_EXPORTER_OTLP_ENDPOINT` is set, with the Batch processor:

snippet: TestTelemetryExport

4. A test that passes through the MCP server but fails with plain `dotnet test` (or the other way round) depends on whether something listens to every `ActivitySource`. Run it both ways.
