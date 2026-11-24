# Progress: Ogooreck 1.0 Emmett-shaped API

Plan: `plan.md`. Spec: `spec.md`.

## Step 1. Replace the 0.x API with the in-process ApiSpecification<TProgram>

Done, with small deviations. The 0.x API (builders, `RetryPolicy`, `Until`, `TestApiRequest`, request DSL, response helpers, `TestContext`, `MadeApiCall`, `ApiTestStep`, `RequestDefinition`, `ResponseAssert`, `RequestTransform`, `GetETagValue`, `TryGetETagValue`) is removed. `TryGetCreatedId` stays because `GetCreatedId` uses it. New: non-generic `ApiSpecification` (static factories, private constructor until step 10), `ApiSpecification<TProgram>` (parameterless and protected `configure` constructors, `Host(...)`, lazy host start through `Lazy<StartedHost>`, one client per specification, `IDisposable`/`IAsyncDisposable`), `TestStep` with `Request`/`Attempt`/`Action`, `StepContext`, `Scenario`, `Given`/`When`/`Then`, `ExpectResponse`/`ExpectError` and the response checks. `StructuralComparison.AssertEquivalentInOrder` orders every nested collection. `Body` compares JSON trees: the response JSON is cut down to the expected fields (a missing field shows as "no such field"), then compared in order. A `TraceparentHandler` (outermost client handler, `AsyncLocal`) sets a fresh `traceparent` per step.

Deviations:
- 7.1 checks through `GetCart` that the POSTed product arrived, instead of an endpoint that stores the raw body.
- 1.1 checks one host per class through a `/instance` id endpoint, not a start counter: a static counter is shared by every host in the test process.
- 8.1/8.2 use a server-generated `/carts/{n}` instead of a literal `/carts/42`.
- `JsonSettingsTests.MixedHostDefaultsToMvcAndAllowsMinimalApiOverrides` is renamed `MixedHostDefaultsToMvcSettings` (its minimal-API half is gone until step 4).

Review: first review in `reviews/20261123T174303_step1_review.md` (blocked; Oskar resolved every finding).

### Step 1, second round (spec r4, plan r3, review fixes)

- Client per step (23.13, qa 44): the host and its `TestServer` handler are created once per specification (`StartedHost`). Each step gets a new `HttpClient` over that handler with its own `TraceparentHandler` in front and `disposeHandler: false`. The `AsyncLocal` is gone: the per-step handler carries the step's `traceparent`. No redirects or cookies (23.12).
- `Location(Func<string, bool>)` (23.11); `RegisterProduct` checks `Location(l => l.StartsWith("/api/products/"))`. The sample's products live under `/api/products`, so the predicate uses that prefix instead of the criterion's `/products/`.
- `Body` on a non-JSON body fails with the expected JSON and "the response body is not JSON: {body}".
- qa 43: the `xUnit1051` suppression is removed. Sample tests pass `TestContext.Current.CancellationToken` to `Then(...)` and to every `HttpClient`/`HttpContent` call; the README snippets use a local `ct` to stay within MarkdownSnippets' 80 columns.
- Review fixes: `Disposing_StopsTheHost` uses a client created by the factory (captured through `Host(...)`), so it fails when the host is not stopped (checked by removing the factory disposal). New failure-message tests for `ETag`, `Location`, the `Location` predicate, `RedirectsTo` (wrong path, no `Location`), `HtmlContaining` with a missing fragment, and `Body`'s "no such field". `StructuralComparison.AssertEquivalentInOrder` is renamed `AssertEquivalentWithEveryCollectionInOrder`, and `Body` goes through `Expect.EquivalentWithEveryCollectionInOrder`. `GivenApiSpecification`/`WhenApiSpecification` are renamed `GivenApiSpecificationBuilder`/`WhenApiSpecificationBuilder`, each in its own file, and `StartedHost` has its own file. `AppJsonOptionsTests` moved to its own file.
- New sample endpoints: `/headers` (echoes request headers), `/carts/{id}/summary` (302 to the cart), `/orders/{id}/status` (text/plain).

### Step 1, third round (re-review `reviews/20261123T182805_step1_review.md`)

- Step-9: the `Then` token now reaches the steps. `TestStep.Run` takes it, `RequestStep` buffers the body with `LoadIntoBufferAsync(ct)`, and the per-step handler (renamed `StepHandler`, it sets `traceparent` and links the step token into every request the step sends) cancels a request even when the user's lambda passes another token. When the token is cancelled while a step runs, any failure from the aborted request surfaces as `OperationCanceledException` with the original as inner exception (the in-memory server can report the abort as an `HttpRequestException` before the buffering sees the token). New test `CancellingThen_WhileTheWhenRequestRuns_EndsWithOperationCanceledException` over the new `/slow` (no headers) and `/stream` (headers, then an endless body, sent with `ResponseHeadersRead`) endpoints; red before (hung until its 10-second guard), green and stable after.
- Standards-6: removed the unused `GET /json/minimal` and `GET /json/override` endpoints from `JsonSettingsTests`.
- Standards-7: `ApiSpecification<TProgram>` moved to `Ogooreck/API/ApiSpecification.Generic.cs`; `InProcessApiSpecification.cs` is gone.
- Step-10 (builder names) left for the coordinator and Oskar.

### Step 1, fourth round (re-review `reviews/20261123T184000_step1_review.md`, spec r6, qa 48 and 49)

- Failure messages (qa 48): every failure names where it happened. Steps are described as `{phase} {kind}` plus the method and path of the request the step sent, taken from the per-step `StepHandler` (for example "Given step 2 request GET /failure", "When request POST /carts/{id}/orders", "When action").
  - 35.1: "Given step 1 request GET /carts/{id} failed with 404 NotFound, but a Given request must succeed; wrap it in Attempt(...) if it may fail. Body: {...}".
  - 35.2: "Then was cancelled while the When request GET /stream was running." When the step also failed in another way, that error is the inner exception and the message adds "The step also failed: {its message}". Cancellation before a step or expectation says so ("Then was cancelled before Given step 1 started.", "... before expectation 2 of 3 ran.").
  - Response checks start with the When step's description and say expected vs found: status (with the body), `Body` ("response body {actual} differs from the expected {expected} at: $[path]: expected <x>, but found <y>", empty body, non-JSON body with the parser error as inner exception and in the message), `Headers`, `ETag`, `Location` (missing headers show as "no ... header"), the `Location` predicate, `RedirectsTo` (wrong path, no `Location`), `HtmlContaining`/`HtmlNotContaining` (fragments and HTML, or the content type that is not HTML), and `ExpectResponse` after an action ("ExpectResponse(200 OK) needs a response, but the When step is an action, which sends no request.").
  - `CreatedId` names the calling step and lists the earlier responses ("response 1: 200 OK from POST /carts/{id}/products").
  - `AssertionException` gained a constructor with an inner exception.
- qa 49: `StepContext.CancellationToken` is the `Then` token, so a waiting action stops; new test `CancellingThen_WhileAWhenActionWaits_StopsTheAction`.
- Tests: new `GivenRequestFailure_NamesTheStepRequestStatusAndBody`; the cancellation and failure-message tests assert the full location and expected/found text.
- `Directory.Build.props` was changed by the coordinator (git hooks target only in the Ogooreck project); not touched here.

### Step 1, final round (review `reviews/20261123T185009_step1_review.md`)

- Step-16: `CreatedId()`/`CreatedId<T>()` on a 201 without `Location` fails with "{step}: the first 201 Created response (response n: 201 Created from METHOD path) has no Location header, so there is no created id."; an id that doesn't convert fails with the `Location` value and the target type, keeping the converter error as inner exception and in the message. New sample endpoint `POST /drafts` (201 without `Location`).
- Step-17: new tests for cancellation before the When step and before expectation n of m, the `Location` predicate without a `Location` header, and the "no such header"/"no ETag header"/"no Location header" messages; the `HtmlNotContaining` test asserts the location text.
- Standards-8: the 35.2 test is `CancellingThen_DuringTheWhenRequest_NamesTheRequest`.
- Standards-9: the `AssertionException(string, Exception)` doc says the message is used as given and should include the inner error's message.

## Step 2. Record messages and wait for catch-up (MassTransit sample)

Not started: blocked by the MassTransit 9 license. No code was written for this step.

- What I checked: the stable MassTransit 9 versions on NuGet are 9.2.4 (2026-10-22), 9.2.5 (2026-10-30), 9.2.6 (2026-11-10) and 9.3.0 (2026-11-18). 9.2.5 is the newest one at least two weeks old (qa 41). Its license is https://massient.com/license.
- The docs (https://masstransit.massient.com/configuration/license/) say v9 needs a license key on "Developer laptops (local builds and tests), CI/CD agents". I found no exemption for tests or for the in-memory transport, and no free community license (only 100% discounts for organizations under $1M, which still need a key).
- To confirm it, I ran a throwaway console app in the scratchpad with MassTransit 9.2.5, the in-memory transport and one consumer, without `MT_LICENSE`/`MT_LICENSE_PATH`. `host.StartAsync()` threw `MassTransit.ConfigurationException: The bus configuration is invalid: [Failure] License must be specified with SetLicense/SetLicenseLocation or by setting the MT_LICENSE/MT_LICENSE_PATH environment variables.` The app was deleted afterwards.
- Decision needed (Oskar): get a MassTransit license key and give it to local runs and CI (a GitHub secret `MT_LICENSE` in `dotnet-ci.yml`; contributors and fork PRs would then need their own key), or change qa 41 to use MassTransit 8 (8.x, open source, supported until 2026-12-31), or use another messaging tool for the step 2 sample (plan and spec change).

### Step 2, second attempt (plan r4, spec r7: Wolverine sample)

Stopped before writing code: Wolverine has no idle signal that a catch-up function (`Func<CancellationToken, Task>`, run after a step) can use. No code was written for this step; the step 1 working tree is unchanged.

- Version and license: `WolverineFx` stable versions are MIT on NuGet. On 2026-11-23 18:07 UTC the newest one at least two weeks old is 6.63.0 (published 2026-11-08 18:42 UTC); 6.64.0 was published 2026-11-09 18:56 UTC, a few minutes under two weeks. A throwaway console app with 6.63.0 and a local queue started and handled messages with no license key or environment variable. Core 6.63.0 no longer ships the runtime compiler, so the sample also needs `WolverineFx.RuntimeCompilation` 6.63.0 (or pre-generated code with `TypeLoadMode.Static`).
- Wolverine's "message tracking" is the tracked session (`host.TrackActivity()`, `ExecuteAndWaitAsync`, `InvokeMessageAndWaitAsync`). Its docs say it "tracks only messages sent, published, or scheduled during the tracked session; messages sent before are not included". It must wrap the action that starts the work. A catch-up function runs after the step has returned, so a tracked session opened there sees none of the step's messages and returns at once.
- The tracked session is one per runtime: `WolverineRuntime` has a single `ActiveSession` property (seen in the 6.61.0 assembly metadata). Two tests running tracked sessions in parallel on one shared host (criterion 16.2) would overwrite each other's session.
- Queue depth is not an idle signal either. `IEndpointCollection.CollectEndpointHealth()` / `IHasQueueDepth.QueueCount` count messages waiting in memory only. In the throwaway app (local queue `work`, a `Ping` handler that takes 500 ms and cascades a `Pong` that takes 300 ms), `QueueCount` stayed 0 for the whole run, also while `Ping` and `Pong` were executing.
- The throwaway app was deleted afterwards.

Decision needed (Oskar), each needs a plan/spec change:
1. Catch-up wraps the step instead of following it: for example a Wolverine-specific "tracked step" hook that runs each step inside `ExecuteAndWaitAsync`. This changes the `Recording(..., catchUp)` shape in the spec, and parallel tests on one shared host (16.2) can't use it, because a runtime has one active tracked session.
2. The sample builds its own in-flight counter in the test host (count each envelope when the publish observer sees it, uncount it when the in-app middleware finishes handling it) and the catch-up function waits until the counter is 0. It is not Wolverine's own signal, and it can miss messages Wolverine routes without the hook (retries, scheduled messages).
3. The Wolverine sample uses no catch-up: subsequence `ExpectPublished` (step 6) and `Eventually`, and exact `ExpectPublished`/`ExpectNothingPublished`/catch-up criteria (15.x, 16.x, 17.1-17.2, 22.1-22.3) move to a sample whose tool has an idle signal, such as Marten's `WaitForNonStaleProjectionDataAsync` (step 7).

### Step 2 (plan r7: Part 2a and Part 2b), implemented with the wrapping catch-up (qa 52, spec r8)

Done except criterion 16.2, which Wolverine's tracked sessions can't meet (see below). `./build.sh test` is green.

Library (`src/dotnet/Ogooreck/API/`):
- `Recorded<T>` (unbounded channel, `TryWrite`; Ogooreck drains it into a shared list), `Observed`, `PublishRequest`, and the internal `RecordingSource`, `SpecificationSettings` and `CatchUpRunner`.
- `ApiSpecification<TProgram>`: `Recording` (plain, with `catchUp`, with `aroundStep`), `CatchUp` (plain and `aroundStep`), `Publishing`, `Deadline`. All are repeatable where the spec says so. The throw on a call after the first step is left for Step 3 (group 6), as the plan says.
- Each step gets a fresh trace id. Every step runs inside the `aroundStep` wrappers, then in catch-up rounds until a round adds no items. The deadline bounds both. Failures: "The application kept producing messages for longer than {deadline} after the {step}." (with the tool's error as inner exception when it threw), "The {step} was still running when the deadline of {deadline} passed...", "{step}: catch-up function n of m failed: ...", "{step}: the catch-up around the step failed: ...". A step that throws inside a wrapper fails with its own exception. Cancellation during catch-up says "Then was cancelled while the catch-up after the {step} was running."
- `ExpectPublished` and `ExpectNothingPublished` in `ApiSpecification.Messages.cs`. They attribute by the When step's trace id (malformed or missing `traceparent` is ignored), compare exactly in order (type first, then all fields in ordered mode), and list the expected and published messages, numbered, as JSON. They fail when the recorder is not registered. `ExpectNothingPublished` without catch-up fails at once. `ExpectPublished` without catch-up fails with "not supported yet" until Step 4 adds subsequence matching.
- `StepContext.Traceparent`, the `Publish(params object[])` step ("When publish: Publish(...) needs a publish function..."). `StepContext` moved to its own file.

Sample `Ogooreck.Sample.Wolverine` (WolverineFx 6.63.0 + WolverineFx.RuntimeCompilation 6.63.0, MIT, no key; local queues, events on one sequential local queue `events` so that their order is stable) and `Ogooreck.Sample.Wolverine.Tests` (Microsoft.Extensions.TimeProvider.Testing 10.11.0), added to `Ogooreck.slnx` and to the `test` target (`test-sample-wolverine`).
- `AppApi : ApiSpecification<Program>` passes its configuration to the base constructor. It registers the publish observer (Wolverine middleware `PublishedMessagesMiddleware` on handlers of the `Events` namespace, test host only) and a fake clock through `Host(...)`, `Recording(PublishedMessages, Read, aroundStep)` with `TrackActivity(deadline).ExecuteAndWaitAsync(...)`, and `Publishing(...)`. It is the assembly fixture. `ShopFixture` holds `For<Program>().Recording(...)` (2.2).
- The recorder is fed by the middleware, not by the tracked session's records: 3.1 needs an observer registered through `Host(...)`, and 16.4 needs a late message recorded outside its own step's session.
- Wolverine propagates the trace only when its `ActivitySource` has a listener. `AppApi` registers one for `Wolverine` (Step 3's in-process telemetry will listen to all sources). Actions and the publish function start an `Activity` parented on the step's `traceparent` (`AppApi.Traced`, like Java's `traced(...)`).

Deviations:
- 16.2 not implemented: tracked sessions clash. `WolverineRuntime` holds a single `ActiveSession`. Two scenarios started with `Task.WhenAll` on the shared `AppApi` host failed in 3 of 3 runs: one test's session ended at once, so its `ExpectPublished` saw 0 messages, and the run took 30 s (the tracking timeout). The test assembly therefore runs without parallelization (`[assembly: Parallelization(Mode = ParallelMode.None)]`), as Wolverine's docs run tracked-session tests in one collection. The 16.2 test was removed. Decision needed: drop 16.2 for the Wolverine sample, move it to the Kafka sample (Step 4, no catch-up), or accept another approach.
- 22.2 drives the rounds with a test-side recorder whose catch-up function adds the follow-up items (call count 4, then the next step). A plain Wolverine catch-up function can't wait for one hop of a cascade deterministically, because tracked sessions only wrap work.
- 11.1: the clock action runs inside `AppApi.Traced(ctx.Traceparent)`. The fake clock fires the expiry timer on the action's thread, which has no `Activity`, so without it `CartExpired` carries no trace id. Each clock test uses its own `AppApi` (and host), because advancing the clock expires every cart on the host.
- 15.1/15.2: the expected `OrderPlaced` has all fields (`Total = 5`, `Products = ["socks"]`), because the check is exact on all fields.
- 2.1 checks `ConfigurationRuns == 1` per `AppApi` instance. 2.3 checks one `/instance` id across three test classes that take the assembly `AppApi`.
- 22.1 uses the sample's `CartViews.WaitForNonStaleDataAsync` (the read model updates 200 ms after `ProductAdded`) as the plain catch-up function.
- Test names use the repository's PascalCase_With_Underscores style.

### Step 2, fix round (review `reviews/20261123T194735_step2_review.md`, spec r10, qa 54)

- Step-1: new test `PlainCatchUpThatKeepsAddingItems_FailsAtTheDeadlineSayingSo`. A plain catch-up adds an item on every call, with `Deadline(2 s)`. It fails after at least 2 s with "The application kept producing messages for longer than 00:00:02 after the When action."
- Step-2: cancelling `Then` after the step finished, while catch-up waits (wrapping or plain), now says "Then was cancelled while waiting for catch-up after the {step}." New test `CancellingThen_WhileTheCatchUpAroundTheStepWaits_NamesTheWait`. The plain-catch-up cancellation test now expects the same wording.
- qa 54 (a): the internal `StepCatchUp` (it replaces `CatchUpRunner`, one per step) counts the wrapper's calls to the step. 0 calls fails with "{step}: the catch-up around the step returned without running the step; it must call the step exactly once." A second call is refused, so the step runs only once, and the test fails with "... called the step 2 times; it must call the step exactly once. The step ran once." Both have tests.
- qa 54 (b): one deadline per step, started when the step finishes, covers the rest of the wrapping catch-up and the plain rounds together (`OneDeadline_CoversTheCatchUpAroundTheStepAndThePlainCatchUpTogether`). The step itself is bound only by the `Then` token (`StepSlowerThanTheDeadline_InsideTheCatchUpAroundIt_IsBoundOnlyByTheThenToken`), so the "still running when the deadline passed" error is gone. The deadline counts as passed when its own token fired and the `Then` token did not. `AppApi` gives Wolverine's tracking timeout twice the deadline, so Ogooreck's deadline ends the wait first.
- qa 54 (c): actions and publish functions run under an `Activity` ("Ogooreck action" / "Ogooreck publish") parented on the step's `traceparent` (`StepActivity`). 11.1's test is now the literal `clock.Advance(...)` action. `AppApi.Traced` and the activity in `AppApi`'s publish function are removed. 11.3 still starts its own `Activity`, as the criterion says.
- Standards-1: `Ogooreck.Sample.Wolverine` and `Ogooreck.Sample.Wolverine.Tests` are in the structure trees of `AGENTS.md` and `CONTRIBUTING.md`.
- `Recorded<T>` counts items without copying the list (catch-up rounds read the count).
- Still open: before the step finishes, only the `Then` token bounds a wrapping catch-up. A wrapper that hangs before calling the step waits until `Then` is cancelled.

## Step 3. In-process traces and metrics, then Eventually (plan r8, spec r10)

Done, with small deviations. `./build.sh test` is green.

Part 3a, library (`src/dotnet/Ogooreck/API/`):
- Public: `ITelemetrySource`, `RecordedSpan`, `RecordedMeasurement`, `TelemetrySources` (`InProcess()` creates a new source each call, `Off`), `ExpectedSpan`, `Telemetry(...)`, `Json(...)`, `ExpectTrace`, `Span(...)` (two overloads), `ExpectCorrelatedTrace`, `ExpectMetric`. Every public member added in this step has caller-focused XML docs with examples (qa 55).
- Internal: `InProcessTelemetry` (an `ActivityListener` on all sources with `AllDataAndRecorded`, recording stopped activities by `DisplayName`; a `MeterListener` on all instruments recording the trace id of `Activity.Current`; histograms record 1 per measurement; observable instruments and `Gauge<T>` are cumulative, observable ones without a trace id; values as Java's `String.valueOf`, invariant culture), `TelemetryOff`, `SpanTree` (port of Java `mismatchOf`/`isBelow`/`describe`), `TestTelemetry` (the test's trace id set, baseline index, increase: delta after the baseline plus cumulative latest minus baseline, missing baseline 0, measurements without a trace id by tags), `Waiting` (poll every 10 ms, stopwatch deadline from the expectation's start, Then cancellation says "Then was cancelled while {call} was waiting, after the {When step}.").
- The source is attached when the host starts (first step) and disposed with the specification. Every configuration call (`Host`, `Recording` x3, `CatchUp` x2, `Publishing`, `Deadline`, `Telemetry`, `Json`) throws `InvalidOperationException` after the first step: "{call} was called after the specification ran its first step, but configuration must happen before the first step. ..."
- `Collect` runs once before the When step (baseline) and before each `ExpectMetric` check. Telemetry off fails every telemetry expectation at once, saying so.
- `StepCatchUp`: `StepFinished` is set only when the step completed, so cancelling during a wrapped step names the step, not the catch-up wait.

Part 3a, API sample: the app names its request activity `{method} {route}` after routing (ASP.NET Core 10.0.3 leaves it as `Microsoft.AspNetCore.Hosting.HttpRequestIn`; OpenTelemetry's instrumentation renames it the same way). `POST /carts/{id}/orders` tags `cart.id`, `cart.closed`, `cart.total` (double; price is now 0.75 per product), the correlation id (from `X-Correlation-Id`), counts `orders.placed` (`channel=web`, `cart.id`), and starts `publish OrderPlaced` (message id, correlation id) and a background `handle OrderPlaced` (causation id) that finishes 500 ms later. An order needs an open cart (409 otherwise). `carts.open` is an observable counter on a per-host meter (`IMeterFactory`); `POST /carts/{id}/expiry` queues the cart for `CartExpiryWorker`, which counts `carts.expired` with no current `Activity`. `GET /orders/{id}/export` returns snake_case JSON for 6.4. New tests: `TraceTests`, `CorrelatedTraceTests`, `MetricTests`, `ObservableMetricTests` (serial collection `SerialMetrics`), `ConfigurationTests`, the 6.4 test in `JsonSettingsTests`; `RecordingTelemetrySource` and `TestSpans` helpers. `Ogooreck.Tests/InProcessTelemetryTests` covers per-specification sources, disposal, value conversion and instrument kinds.

Part 3b: `Eventually(TestStep)` (only a `Request`; others throw `ArgumentException`). Each attempt gets a fresh trace id that joins the test's set; the When trace id for messages is the final attempt's. Attempts run back to back while an `ExpectResponse`/`ExpectError` fails; at the deadline (from the first request) it fails with the attempt count, last status and body, and the last failure as inner exception. The whole attempt loop is the step for catch-up, so a wrapping catch-up (Wolverine's tracked session) surrounds all attempts and plain catch-up runs after them. The response expectations are not run again afterwards; the other expectations run once. `Eventually` in Given fails before any step runs. Wolverine sample: `OrderViews` (shows `Placed` from the 3rd read of a placed order), `GET /orders/{id}` publishing `OrderViewed`, `POST /orders/{id}/holds` (200 twice, then 409); `EventuallyTests`.
- Wolverine: `AppApi`'s own `ActivityListener` for `Wolverine` is removed; the in-process source makes it redundant, and all Wolverine tests pass.

Deviations:
- 26.3 uses the test's cart Guid as `cart.id` and another Guid for the failing half, instead of `42`/`43` (cart ids are per test).
- 26.4 closes the cart with `ConfirmCart` in Given (spans of Given steps count, so a Given order would satisfy the expectation).
- 26.5's "no fixed wait" half checks that `ExpectTrace` on spans that already finished returns within 1 s.
- 27.2, 27.3, 27.5 and the "no span handled the message" case emit spans from a test `ActivitySource` inside `Action` steps; 27.1 uses the app.
- 28.4 runs in a serial collection, because observable instruments carry no trace id and so count for every live specification in the process. It does not run on its own hosts: another live host's `carts.open` is summed into the same series (see the Step 3 fix round).
- 13.5: there is no user-made `Expectation` to count, so "ExpectPublished runs once" is shown by the exact match passing with three attempts.
- 13.6/extra: Wolverine's spans are all named `receive`; the first-attempt-only span is matched by `messaging.message_type`.


## Step 4. Kafka, Marten and causation (plan r10, spec r11)

Done, with small deviations. `./build.sh test` is green (Api 143, Wolverine 63, Kafka 9, Marten 8, BusinessLogic 16, F# 5, library 125).

Library (`src/dotnet/Ogooreck/API/`):
- `ExpectPublished` on a recorder without catch-up waits (`Waiting.Until`, polling every 10 ms) until the expected messages are an in-order subsequence of the When step's messages, and fails at the deadline: "after {deadline}, {k} of {n} expected messages arrived in order; message {k+1} ({Type}) did not arrive." plus the numbered expected and published lists. Cancelling `Then` ends with `OperationCanceledException` naming the wait.
- `ExpectAppended` and `ExpectNothingAppended` share the code with the published checks through `MessageWording` (internal, own file); only the wording differs ("events", "appended", "Appended by the When step").
- `Caused` (public, own file), `Sent`, `Published`, `Appended`, `ExpectCausation(params Caused[])` in `ApiSpecification.Causation.cs`. Follows Java `expectCausation`: first root match by all fields, each node must carry its root's correlation id, each child's causation id must be its parent's message id. Once after catch-up when every recorder in the trees has catch-up, otherwise `Waiting.Until`. Missing ids fail at once: "needs message, correlation and causation ids from read, but read returned no ... for {Type}" (root and parents need message id, every node a correlation id, children a causation id). Failures list the When step's items with their ids.
- `RecordingSource.ObservedOf` (internal) returns the When step's `Observed` items. `RecordingOf` takes `object`.
- The Wolverine test `ExpectPublished_WithoutCatchUp_FailsSayingItIsNotSupportedYet` is removed: the "not supported yet" failure no longer exists. Its replacement is in `Ogooreck.Sample.Api.Tests/RecordedMessageTests`.

Samples:
- `Ogooreck.Sample.Kafka` (Confluent.Kafka 2.16.0) and `.Tests` (Testcontainers.Kafka 4.16.0, `confluentinc/cp-kafka:8.4.1`, one partition so order is stable). `KafkaBroker` is an assembly fixture: container, topic, one test-side consumer with its own group that feeds `KafkaApi.PublishedMessages` and waits for its partition assignment before the first test (without it the first test lost its race against the group join). `KafkaApi` is a specification per test, so a test can set its own deadline. Tests: 18.1 to 18.4, 32.1, the three extras and 20.5.
- `Ogooreck.Sample.Marten` (Marten 9.58.0) and `.Tests` (Testcontainers.PostgreSql 4.16.0, `postgres:18.7-alpine`). `traceparent` is a Marten header, correlation and causation ids are Marten metadata. A session listener (`AppendedEventsListener`, test host only) feeds `MartenApi.AppendedEvents`. Each `MartenApi` has its own schema, so a test's async daemon never competes with another test's. `WaitForNonStaleProjectionDataAsync` throws on a store with no events, so the catch-up returns at once when there are none. The projection test fails (404) when the catch-up is a no-op.
- Wolverine: `ShipOrder` event with a no-op handler; `AppApi.Read` returns message, correlation and causation ids (`Envelope.Id`, `CorrelationId`, `ConversationId`); `AppApi.ShippingPlacedOrders()` registers a test-only `ShipOnOrderPlaced` handler, so only that host turns `OrderPlaced` into `ShipOrder` (20.1).
- `Ogooreck.Sample.Api.Tests/RecordedMessageTests` and `CausationTests` hold the in-process tests of the new branches (recorders fed by `Action` steps; no Docker): subsequence, wording, 20.2 to 20.4, trees, two recorders, unregistered recorder, cancellation.
- `Ogooreck.slnx`, `Ogooreck.Build/Program.cs` (`test-sample-kafka`, `test-sample-marten` in `test`), the trees in `AGENTS.md` and `CONTRIBUTING.md`.

Deviations:
- 18.1 measures from the start of the When request and requires under 2.5 s (the sample publishes `CartClosed` after 3 s); host start is excluded.
- 20.2 to 20.4 run in `Ogooreck.Sample.Api.Tests` with test-fed recorders, not in the Wolverine sample: the real app cannot produce a wrong causation id. 20.1 runs in Wolverine, 20.5 in Kafka, as the plan says.
- "About one second" cancellation tests accept 0.9 s as the lower bound: timers fire a fraction early (0.9998 s seen).
- Marten ids: the `ExpectCausation` Marten test uses two sessions (the second one carries `CausationId` = the first event's id), as a handler reacting to a stored event would.

## Step 5. Out-of-process apps: non-generic ApiSpecification, OtlpReceiver and Aspire

Done, with deviations. Review not run (as instructed).

Library (`src/dotnet/Ogooreck/API/`), all additive:
- Non-generic `ApiSpecification` is now instantiable (`ApiSpecification.Host.cs`): the three protected subclass constructors, `For(address)`, `For(address, handler)`, `Host(address)`, `Host(address, handler)`, `Recording` x3, `CatchUp` x2, `Publishing`, `Deadline`, `Telemetry`, `Json`, `Given` x2, `Dispose`/`DisposeAsync`. Defaults: web JSON, telemetry off, 30 s. The only edit in the old file is the removal of the private constructor in `ApiSpecification.cs`. It reuses `Lazy<StartedHost>`, the builders and `SpecificationSettings` unchanged: the address function is awaited with `GetAwaiter().GetResult()` inside the lazy, so it runs once on the first step. A `DelegatingHandler` from the handler factory without an inner handler gets a `SocketsHttpHandler`. No address: "A host is required, ...". An address function that throws: "Starting the host failed: the base address function threw {type}: {message}" with the error as inner exception.
- `OtlpReceiver` (Kestrel on `127.0.0.1:0`, `POST /v1/traces` and `/v1/metrics`, protobuf only: 415 for other content types, 400 for a body that is not protobuf), `OtlpReceiverOptions.WaitForMetricsExport`, internal `OtlpDecoder` (`CodedInputStream`; spans: ids, name, string/int/double/bool attributes, other attribute types are dropped; metrics: sum, gauge, histogram counts, temporality: delta only when the point says delta, so cumulative, gauge and unspecified are cumulative; exponential histograms and summaries are skipped). The receiver feeds every specification it is attached to. `Google.Protobuf` 3.36.2 and `FrameworkReference Microsoft.AspNetCore.App` added to `Ogooreck.csproj`.
- `WaitForMetricsExport`: `TestTelemetry` now wraps every `Collect` in a token that the deadline cancels. At the deadline it fails with "{where}: the telemetry source OtlpReceiver did not finish collecting within the deadline of ...", where `where` is "Before the When step" for the baseline and "{When}: ExpectMetric(...)" for a check. `TakeBaseline`/`Increase` in `TestTelemetry.cs` and the call in `ApiSpecification.Telemetry.cs` changed for this.

Samples: `Ogooreck.Sample.Aspire.Api` (OpenTelemetry OTLP exporter, ASP.NET Core instrumentation, a `cart.products.added` and an `orders.placed` counter, span tags `cart.id`, `cart.closed`, `cart.total`, Kafka producer with a `traceparent` header), `Ogooreck.Sample.Aspire.AppHost` (API + Kafka; `--OtlpEndpoint` configuration sets `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf`, `OTEL_BSP_SCHEDULE_DELAY=200`, `OTEL_METRIC_EXPORT_INTERVAL=500`; `--WithKafka=false` leaves Kafka out), `Ogooreck.Sample.Aspire.Tests` (`AspireApi : ApiSpecification` with a static `OtlpReceiver`, a second `WaitingAspireApi` with `WaitForMetricsExport`, both assembly fixtures; one test collection because the request duration histogram has no test-unique tag). Added to `Ogooreck.slnx`, `Ogooreck.Build/Program.cs` (`test-sample-aspire`, part of `test`), `AGENTS.md` and `CONTRIBUTING.md` trees.

Library tests (`Ogooreck.Tests`, against a real Kestrel `EchoServer`, no Aspire): `OutOfProcessSpecificationTests` (4.1, 4.2, 4.3, 4.5, 4.6, 5.1 in both dispose modes, address failure, configuration after the first step, defaults, handler without inner handler, `Host` twice), `OtlpReceiverTests` (decoder, temporality, skipped fields, content type, deadline cases, `ExpectTrace`/`ExpectMetric` over OTLP, 29.3).

Deviations:
- Aspire packages are 13.6.5, not the 13.6.4 that is two weeks old: 13.6.4 is flagged by NuGet audit (GHSA-f7vh-h5g3-7jrh, GHSA-4g86-54fm-h9h8, fixed in 13.6.5) and warnings are errors. 13.6.5 was published 2026-11-10 17:16 UTC, so it is 13 days old today. `Aspire.Hosting.Testing`, `Aspire.Hosting.Kafka` and `Aspire.AppHost.Sdk` are all 13.6.5; `AspireUseCliBundle=true` avoids the ASPIRE010 warning without suppression. Other pins: Google.Protobuf 3.36.2, Confluent.Kafka 2.16.0, OpenTelemetry 1.20.1.
- The Kafka recorder of the Aspire sample is registered with `Recording(...)` but not asserted with `ExpectPublished`: subsequence matching on a recorder without catch-up is step 4a, which is not in this copy. The test `PlacingAnOrder_PublishesItToKafkaWithTheStepsTraceparent` waits on a queue the consumer also fills. Replace it with `ExpectPublished(App.ConsumedOrders, ...)` when 4a is merged.
- 5.1: out of process, the app belongs to the caller (Aspire's `DistributedApplication`), so disposal stops what Ogooreck owns: the handler, the telemetry source and the receiver. The tests check each (handler disposed, receiver refuses connections, in-process listener stops), not a connection to the app.
- 29.3 is tested against a receiver with no app (library tests), not through Aspire. 4.4/29.x are the Aspire tests.
- The pl-PL test sets `CultureInfo.DefaultThreadCurrentCulture`, because the receiver decodes on server threads.
- Tests run 9 Aspire tests in about 20 s once images are present; Docker is needed.

Checks: see the outcome block.

Step 5 fixes found by the full `./build.sh test` (Release):
- Decoder: a histogram data point's `explicit_bounds` (field 7, packed) has the same tag as a number point's attributes, so real exports were rejected with 400 and no metric arrived. Attribute tags are now chosen by point kind; the test payload histogram points carry bucket counts and bounds.
- `WaitForMetricsExport`: `Collect` waits for two exports, not one. The export in flight may have been read before the Given step's increment, so the baseline missed it once (found 2 instead of 1). The spec says "the next export"; this is a deviation to confirm.

## Steps 4–5 fix round

Fixes for `reviews/20261123T211735_steps4-5_review.md` (spec r12, qa 56). No re-review follows.
- Step-1: the 29.4 histogram test moved to `WaitingAspireApiTests` (the `WaitForMetricsExport` receiver), so the baseline waits for earlier exports.
- Step-2: the Aspire Kafka test uses `ExpectPublished(AspireApi.App.ConsumedOrders, new OrderPlaced(cartId, 1.5))`; the `Consumed` queue, `WaitForOrderOf` and the manual trace id check are gone.
- Step-3: code kept; the XML docs of `OtlpReceiver.Collect` and `OtlpReceiverOptions.WaitForMetricsExport` say it waits for two exports.
- Standards-2 and 5.1: `SpecificationSettings` tracks ownership. A specification disposes its host, handler and its own default in-process telemetry. A source or `OtlpReceiver` passed to `Telemetry(...)` belongs to the caller, so `AspireApi` and `WaitingAspireApi` now dispose their receivers themselves. Dispose tests changed to assert the receiver or source keeps running, plus a test sharing one receiver across two specifications and a test that the default in-process listeners stop.
- Step-4: `ExpectCausation` needs a message id only for a node with children; test with a leaf lacking one.
- Step-5: the missing-ids failure starts with `{When}: ` and the tests assert the full wording.
- Step-6: the `Collect` deadline hint about `WaitForMetricsExport` appears only for an `OtlpReceiver` that has it on; a test covers a custom source.
- Step-7: 18.2 and 18.4 in the Kafka sample assert the "did not arrive" wording.
- Not done: Standards-1 (`<example>` in XML docs), left for step 6.

## Step 6b

Parts 6a and 6b of step 6 (6c, the README and migration guide, is not done here). Review not run.

Part 6a: `Ogooreck.Sample.Wolverine.FSharp.Tests` (xUnit v3, F#, Fantomas, 80 columns). `Shop` inherits `ApiSpecification<Program>` with a `fun spec -> ...`-style configure function (`Host`, `Recording(published, read, aroundStep)`, `Deadline`), and the tests use `open type ApiSpecification`, `Request(fun client -> ...)`, `Observed(message, traceparent)` in `read`, and `ExpectPublished(...)` inside `task { }`, with no annotations on the scenario lambdas. Tests: placing an order publishes `OrderPlaced` (30.1), and an unexpected message fails with Ogooreck's numbered list. No library change was needed. F# friction found, all outside the library: `Observed(nonNull envelope.Message, ...)` because the F# nullness checker sees Wolverine's `Message` as `obj | null`; `ExecuteAndWaitAsync` has Task and ValueTask overloads, so the lambda is wrapped in `Func<IMessageContext, Task>`; the `aroundStep` lambda has to return `Task`, so the `task { }` is upcast with `:> Task`. The sample records the envelopes of the tracked session (`session.Sent.Envelopes()`) instead of the C# sample's Wolverine middleware.

Part 6b: `Ogooreck.Sample.NUnit.Tests` (NUnit 5.0.0, NUnit3TestAdapter 6.3.1), `Ogooreck.Sample.MSTest.Tests` (MSTest 4.5.2) and `Ogooreck.Sample.TUnit.Tests` (TUnit 1.81.10) each run one passing scenario (`POST /api/products` is 201) and one failing scenario (`ExpectResponse(OK)` against 201) against `Ogooreck.Sample.Api`. The failing one asserts with the framework's own throws-assertion that Ogooreck's `AssertionException` message contains 200 and 201, so each project is green. Each passes the framework's cancellation token (`TestContext.CurrentContext.CancellationToken`, `TestContext.CancellationToken`, the `CancellationToken` test parameter). In `Ogooreck.Tests`, `FrameworkIndependenceTests` checks that the referenced assembly names and the package references of `Ogooreck.csproj` contain none of xunit, nunit, mstest, tunit.

Deviations:
- TUnit runs on Microsoft.Testing.Platform, and on the .NET 10 SDK `dotnet test` refuses it unless `global.json` opts in, which would break the xUnit v3 `mtp-off` projects. The `test-tunit` target uses `dotnet run --no-build --project ...` instead.
- TUnit 1.81.10 is pinned, not 1.81.16 (published 2026-11-09, exactly on the two-week boundary).
- Build targets `test-wolverine-fsharp`, `test-nunit`, `test-mstest`, `test-tunit` are part of `test`.
- Not changed: `src/dotnet/Ogooreck/`.

## Step 6a. Enforce a documented public API and full coverage

Done (plan "Step 6, part 0", qa 55, spec r12 "Testing Decisions").
- Documentation gate: all six `#pragma warning disable CS1591` are gone. Every public and protected member of `Ogooreck` has an XML comment; the 86 members that lacked one (`Decider`, `DecideResult`, `Specification`, `DeciderSpecification` and its builders, `HandlerSpecification`, `WhenMapping`, `ObjectFactory`, `HttpResponseMessageExtensions`) are documented, and the members of steps 4-5 that had a summary only (`Sent`, `Published`, `Appended`, `ExpectNothing*`, the non-generic constructors and configuration calls, `Span`, `OtlpReceiver` parts, `TelemetrySources`, `RecordedSpan`, `RecordedMeasurement`, `Observed`, `PublishRequest`, `StepContext`, the builders) got an `<example>` from a real test. The `OtlpReceiver` summary no longer says the specification disposes it (qa 56: the caller does).
- Coverage gate: `Ogooreck.Build/Program.cs` runs every test target with coverlet.msbuild 10.1.0 (`Directory.Build.props` references it in every `*.Tests` project), merges the runs through `MergeWith` into `results/coverage`, and the new `check-coverage` target (a dependency of `test`) fails unless the `Ogooreck` assembly has 100% line and 100% branch coverage, listing the files and lines that are not covered. 10.1.1 is only 8 days old, so the pin is 10.1.0, the latest release at least two weeks old (and the one the two business logic sample projects already use through `coverlet.collector`).
- Unreachable branches removed instead of tested: `StructuralComparison` (`DeclaringType?.`, `is not true` on `ContainsKey`, the record detection now reads the compiler-generated `op_Equality`, the anonymous type check reads the `CompilerGenerated` attribute only, `IsFirstCollectionItem` no longer tests for a leading `[`), `TryGetCreatedId` (a converter result of null cannot happen for a non-empty id), `InProcessTelemetry.StringOf` (`$"{value}"`), `StepCatchUp` (the `deadlineStarted` guard: one `StepCatchUp` runs one step), `SpanTree.IsBelow` (no foreach over `Where`), `OtlpDecoder` (field walking is a `FieldsOf` iterator).
- Tests added to `Ogooreck.Tests` through the public API: `SpecificationTests` (every `Specification.For`, `Decider.For`, `DecideResult.For` overload, every `Then` and handler `When` shape, initial state of string, value type and private constructor), `EquivalenceTests`, `HandmadeResponseTests`, `CatchUpFailureTests` (all `StepCatchUp` paths, deterministic: no test depends on a race with the deadline), `TelemetryMatchingTests` (a custom `ITelemetrySource`), and decoder and disposal tests in the existing classes. One sample test (`DefaultTelemetryDisposalTests`) covers disposal before the first step.
- Check by hand, then reverted: an undocumented public class in `Decider.cs` fails `dotnet build` with CS1591 (error, warnings are errors); deleting `SpecificationTests.cs` makes `./build.sh test` fail in `check-coverage` and list the uncovered lines.
- `AGENTS.md` and `CONTRIBUTING.md` have the section "Tests, coverage, and documentation" (strictland's, adapted to coverlet, XML comments and `<example>`).

Findings:
- Bug, not fixed (out of scope): with a value type state, a decide function that returns only events makes `DecideResult.NewState` the type's default (`TState?` is not nullable for an unconstrained value type), so `state ?? newEvents.Aggregate(...)` never evolves the state. The tests use a state-returning decide function for the value type case.
- `OtlpReceiverTests.Collect_WithWaitForMetricsExport_FailsAtTheDeadlineWhenNoExportArrivesBeforeTheBaseline` failed under coverage by 1.4 ms (timer fired at 1.9986 s of a 2 s deadline); the lower bound is now 1.9 s.
- `Then(params Expectation[])` (no token) trips xUnit1051 in xUnit projects; its test reaches it through a method group.

## Step 7, part 7a

Core plugs, the decorator helper, the message reader, and the Wolverine and Marten plugs. Parts 7b and 7c are not done here.

Core (`Ogooreck/API`): `IPlug`, `PlugRegistration` (`Services`, three `Recording` overloads, `CatchUp`, `Publishing`, `Starting`, `AppServices`, `Deadline`), `Use(IPlug)` on both specifications, `services.Decorate<TService>(...)`, `CallRecorder.Wrap<TService>(...)` for `Task`, `Task<T>`, `ValueTask` and `ValueTask<T>`, `MessageHeaders`, `MessageReader` (instance; options return a new reader; `Read(header, body)`), `UnknownMessage` (with an extra init property `HeaderName` so the failure can name the header), and the failure on an unknown type of a When step message (`RecordingSource.ObservedOf(outcome)`).
- Replacement: a plug made from another one implements `IDerivedPlug` (`Origin` is the first plug of the family). `Use` replaces the earlier plug with the same origin in place: its recordings, catch-ups, around-steps, services and starting functions, and its publish function. Direct calls and plugs share `OwnedItems<T>` lists in `SpecificationSettings`.
- `Starting` functions run once under a gate before the first step; a failure is kept and fails every later step too.
- `Publishing` twice throws, naming both sources; two direct calls throw as well (a change: an earlier direct call used to be overwritten).
- New projects `Ogooreck.Wolverine` (`WolverinePlug`: tracked session, `Published`, `MessageTypes(params Type[])`, `MessageTypes(Func<Type, bool>)`) and `Ogooreck.Marten` (`MartenPlug`: listener, projection wait bounded by `registration.Deadline` and the step's token, `WithoutCatchUp()`). Both are in `Ogooreck.slnx`, `pack` and the coverage run (Include filter for the three assemblies, generated `obj` files excluded). The release workflow packs through `build.sh pack`, so it needed no change.
- Samples: the Wolverine sample setup (`AppApi`, `ShopFixture`) keeps app settings and `Use(...)` calls, with `FakeClock` and `SampleApp` as user plugs; `PublishedMessagesMiddleware` stays only for `EveryPublishedMessage`. The F# sample uses `WolverinePlug`. The Marten sample uses `MartenPlug`; the app gained `POST /carts/{id}/imports`, which appends without a `traceparent`, so the plug's untraced branch has a test.
- Spike result: `qa.md` answer 90.

Deviations:
- 36.1 uses the sample's filtered `Messaging.Wolverine` instead of `new WolverinePlug()`, because a plain plug records the `ExpireCart` command that the app's timer publishes (qa 90, open).
- Tests were written together with the code, not strictly before it, for the core types; each test was run and seen passing, and the build's coverage check shows 100% for the three assemblies.

Checks: `./build.sh test` from `src/dotnet` passes (formatting, all test projects, coverage 1829/1829 lines and 855/855 branches).

## Step 7, part 7b

Kafka plug (review `reviews/20261124T220000_step7b_review.md`, findings left, second review not run).

New project `Ogooreck.Kafka`: `KafkaPlug.Decorating<TKey, TValue>()` and `Decorating()`, and `KafkaPlug.Consuming(bootstrapServers, topics)`. The decorating plug wraps the app's producer, recording each successful `ProduceAsync` (also the `TopicPartition` overload), and its consumer, a `DispatchProxy` that tracks whether `Consume` is running and learns the topics. Before the first step it waits for the assignment, for the fetcher to start (the high watermark is known) and for no group on the broker to be rebalancing; on timeout it names the topics or the group. Each catch-up round lists the broker's groups, fails on a member that is neither decorated nor ignored, reads end offsets through one `DependentAdminClient` per consumer, and waits until each consumer is inside `Consume` with its position at the end. It stops at 0.9 × the deadline: admin calls on a consumer handle complete only when the consumer polls, so when a handler blocks, the failure is built from cached watermarks and still names topic, partition, position and end offset. `Ignoring(groups)` and `CatchingUpWith(...)` return a new plug that shares the recorder. `Consuming` creates missing topics, reads from the end with its own group, and has no catch-up.

Sample: a `shipping` hosted service, a `billing` consumer built outside DI, `ShippingFaults` (a DI singleton that scripts faults per cart id: `FailFirstTryOf`, `DelayShippingOf`), and Java's header names. The tests run on one shared app through an assembly fixture, in parallel. Extra apps: 39.5 (2-second deadline with a blocked handler), 39.4 (a new host with older messages on its topic), 39.7 and 39.8 (`billing` built outside DI), the stuck-rebalance tests, and the option and wiring tests (producer only, consumer only, neither registered, failed produce, `CatchingUpWith`, byte[] and int values, `Consuming` options and disposal). Aspire uses `KafkaPlug.Consuming` with `Headers(new MessageHeaders { MessageType = "message-type" })`; its side consumer and `ConsumedOrder` are gone. The test broker has `KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS=0`.

Fix round (qa 93 and 95): before the first step the plug waits only for the groups of the consumers it wrapped (assignment, fetch position known, group stable), so no test needs a serial collection any more. The check looks only at those groups: a member that isn't a wrapped consumer fails the step naming the group and the member; `Ignoring(groups:)` is removed, and a `billing` consumer in its own group is never looked at (39.8). `KafkaPlug.Consuming` creates no topics; the Aspire app creates its own and sends `message-id` and `correlation-id`, which 38.2 checks. A consumer's `Dispose` waits for its in-flight admin requests (bounded by their 10-second timeout) before freeing the dependent admin client: a catch-up that gave up on the broker left a request running, and disposing the app under it crashed the test host with a native double free.

Checks: `./build.sh test` from `src/dotnet` passes (formatting, all test projects, coverage of the five libraries 2318/2318 lines and 1007/1007 branches). Before the dispose fix, the Kafka tests passed 8 runs in a row (5 plain, 3 with coverage).

## Step 7, part 7c

RabbitMQ plug and sample (review `reviews/20261124T210000_step7c_review.md`).

The RabbitMQ plug is `RabbitMQPlug.Decorating()`, built on three `DispatchProxy` levels: connection, channel and consumer. The probe goes to each consumed queue with the `catchUpProbe` header. It counts when every lower delivery tag on its channel is done, no handler runs, and no requeue is pending. The plug counts a requeue before it sends the nack, and undoes the count if the call throws. At the start of every round, a passive declare checks each consumed queue and each queue the app publishes to through the default exchange. `Management(uri, user, password)` adds the queues bound to the exchanges the app publishes to. The options (`MessageTypes`, `Headers`, `Json`, `Read`, `Ignoring`, `Management`, `CatchingUpWith`) return a new plug that shares `Published`. The sample app is `Ogooreck.Sample.RabbitMQ`, with the broker fixture in `Ogooreck.Sample.RabbitMQ.Tests`. The tests cover 38.1 and 40.1–40.8, and `Ogooreck.RabbitMQ` has 100% line and branch coverage. Testcontainers' default login is `rabbitmq`/`rabbitmq`, not `guest`/`guest`.

Sample changes: `RejectTheFirstTry` and `PublishToAMissingExchange` options; publisher confirms on the publish channel, so a publish to a missing exchange throws. `ConsumerOnItsOwnConnection.StopAsync` closes its connection only once, because a second host stop threw `AlreadyClosed`. The shipping handler has a fixed delay, so the `Ignoring` test can't pass by luck.

Deviations (after the fix round, qa 93 and 95):
- The sample tests use one shared app per test project (assembly fixture, default settings, `Use(Messaging.RabbitMQ)`), in parallel; per-order faults (`ShippingFaults`) script nacks, rejects, delays, blocked handlers and publish failures. Tests about different wiring start their own app with their own queue names: `DispatchConcurrency` 4 (40.3), the probe count (40.5), the 2-second deadline (40.6), a second consumer on its own connection (40.7), publish-only, auto-ack, the multiple-nack and failed-nack tests, and the four plug-option tests (`MessageTypes`+`Json`, `Headers`, `Read`, `CatchingUpWith`), because one host holds one plug. That is 12 own apps against 8 tests on the shared app.
- 40.3 and 40.5 place the orders in the When step; the counts are checked in test code after the Then, because a custom Expectation is not public and `ExpectPublished` is order-exact.
- The plug checks only the queues its decorated consumers read; `Ignoring` and `Management` are removed, 40.8 is rewritten and 40.7 loses its `Ignoring` half. The test broker has no management plugin.
- Pending requeues are tracked per message (routing key and body hash), so a `multiple` nack counts each delivery and the count cannot go below zero. A nack that throws removes its keys again.
- A custom `Read` function sees every message in the shared recorder, including other tests' traffic, so it must tolerate foreign messages.
- The decorated consumer count never goes down when a channel closes.
- Measured on the shared app with idle queues: one step (place an order, `ExpectPublished` of `OrderPlaced` and `ShipOrder`) takes a median of 25 ms; an own app costs about 0.5–0.7 s for its first step; the test project runs 20 tests in 3–4 s.

Package ages: RabbitMQ.Client stays at 7.2.2 (7.3.0 is from 2026-11-23) and Testcontainers.RabbitMq at 4.16.0 (4.17.0 is from 2026-11-16).

Checks: `dotnet build --no-dependencies` for the three projects, `dotnet test Ogooreck.Sample.RabbitMQ.Tests --no-build` (25 of 25, stable over repeated runs), coverage with `[Ogooreck.RabbitMQ]*` (100% lines, branches and methods), and `dotnet format --verify-no-changes` on all three projects.

## Step 8. Variants of the test setup on a shared app

Core: `ApiSpecification<TProgram>.With(configure)` returns a specification on the same running app with the test setup copied and changed; `WithNewApp(configure)` and `WithNewApp(host, configure)` are the explicit ways to start a separate app (the root's host customization first, then the new one; the root's and the variant's plugs; the test setup copied). The host customization moved from the `Host(...)` method to construction, `For<Program>(host)` and the base constructor `(host, configure)`, so a variant can't change the host: the compiler rejects it instead of a runtime error. The one case types can't express, a plug that changes the app's services and isn't in the app's setup, throws: "Plug X changes the app's services, so it can't run on the shared app. Add it to the app's setup, or use WithNewApp(...) to start a separate app for it." A variant skips the `Services` of the app's plugs, runs their `Starting` once through its parent, shares the app's telemetry source and recorders unless it sets its own, may replace an inherited publish function, and disposing it leaves the app running. The configuration error now says "Use With(...) for other test setup."

Plugs: the Kafka plug's wrapped consumers and the RabbitMQ plug's `HostState` live in the app's services as keyed singletons (key: the plug's `Origin`), so a plug made through an option, used in `With`, waits for the app's consumers. Kafka's unused `producedTopics` was removed.

Samples:
- API: `RecordedMessages.Spec()` (31 tests), deadline, telemetry, JSON and catch-up variants use the class's app through `With`. One test reused its cart in a second part that used to run on a fresh app; it now uses its own cart. Kept on their own app: telemetry lifecycle tests, observable-metric tests (global meters, serial), host customization tests (JSON factories, fixed time, reading the factory), and the same-instance test that never starts an app.
- Wolverine: a plain shared app, `BareApp` (assembly fixture), for the core catch-up, recording and publishing tests, through `With`; `AppApi` stays the shared app with plugs. Plug tests that wire services keep their own apps.
- Kafka: a second shared app, `broker.PublishOnly` (the publish-only app on the default topic with the test-side consumer), replaces `KafkaApi.Consuming(broker)` in each test; plug-option tests use options of `Messaging.Kafka` through `With`. A catch-up test asserted exactly one round; on a shared app other tests' messages can cause another round, so it now asserts that each round runs both waits in order. Kept on their own app: other DI wiring (removed services, extra producer types), a new host with older messages, a member outside DI, stuck rebalance, the blocked handler.
- RabbitMQ: the three reading-option tests run on the shared app through `With`. Kept on their own app: concurrency 4, auto-ack, no shipping, a consumer outside DI, and tests asserting app-wide counts (running handlers, pending requeues, handled count), which parallel tests on a shared app would change.

Checks: `./build.sh test` from `src/dotnet` passes (formatting, all test projects, coverage of the five libraries 2414/2414 lines and 1043/1043 branches, 5 min 13 s). Before it: API tests 3 runs, Wolverine 2, Kafka 3, RabbitMQ 2, all green.

## After step 8: `ExpectPublished` as in Java, and an app without `Program`

- Recorders have a role: `Recording(...)` (published messages) and `Appending(...)` (appended events) on `ApiSpecification<TProgram>`, `ApiSpecification` and `PlugRegistration`; the Marten plug uses `Appending`. `ExpectPublished(messages...)`, `ExpectNothingPublished()`, `ExpectAppended(...)`, `ExpectNothingAppended()` and the causation nodes `Published`, `Sent` and `Appended` check the specification's one recorder of that role; none or more than one fails at once, saying what to do. The named form is `ExpectPublishedIn(recorded, ...)`, `ExpectNothingPublishedIn`, `ExpectAppendedIn`, `ExpectNothingAppendedIn`, `PublishedIn`, `SentIn` and `AppendedIn`, with its own name so no language can pick a wrong overload (qa 98). 46 sample calls dropped the recorder; about 100 that need it use the `...In` form (custom recorders, the Kafka publish-only app with two published recorders, the Wolverine plug tests). The F# sample uses plain `ExpectPublished`.
- `ApiSpecification.For(host)` for a host built in the test with `UseTestServer()` (qa 99), tested in `Ogooreck.Tests/TestServerHostTests.cs`.
- Tests: `Ogooreck.Sample.Api.Tests/RecorderByRoleTests.cs` (one recorder, none, two, roles kept apart, causation without recorders, a plug appending around each step), `Ogooreck.Tests/OutOfProcessSpecificationTests.cs` (the three `Appending` overloads).
- Checks: `./build.sh test` from `src/dotnet` passes (formatting with fantomas, all test projects, coverage 2480/2480 lines and 1065/1065 branches, 5 min 35 s).

## Fixes from the step 8 review (`reviews/20261125T130000_step8_review.md`)

- A variant skips the `Starting` and `Services` of a plug that any of its parents already has, not only the root, so `app.With(s => s.Use(plug)).With(s => s.Use(plug))` starts the plug once. A variant keeps the plugs of the variants it was made from, so `WithNewApp(...)` on a variant of a variant re-adds the plugs of every level.
- The named forms check the recorder's role: `ExpectPublishedIn` on a recorder registered with `Appending(...)` fails, saying "Check it with ExpectAppendedIn(...)", and likewise for `ExpectNothing...In`, `PublishedIn`, `SentIn` and `AppendedIn`. The API tests that checked appended events on a `Recording(...)` recorder now register it with `Appending(...)` (`RecordedMessages.AppendingSpec`).
- The "more than one recorder" failure names the form of the call that failed: `ExpectNothingPublishedIn(Messaging.Kafka.Published)`, `PublishedIn(...) or SentIn(...)` inside `ExpectCausation`, and `Events.Marten.Appended` as the example for appended events. The "needs a catch-up function" failure says `Appending(recorded, read, catchUp)` for appended events.
- Tests guard that the app's telemetry keeps recording after a variant is disposed, asynchronously and synchronously.
- Kafka's group check is back to one `DescribeConsumerGroupsAsync` request for all active groups; the per-group requests had no recorded reason.
- `PlugRegistration.Services` documents the host customization of `For<Program>(host)` or the constructor instead of the removed `Host(...)`.
- Samples moved to the shared app: Wolverine `EventuallyTests` deadline test (`api.With(s => s.Deadline(...))`); Wolverine `CausationTests` scripts shipping per cart (`ShipOnOrderPlaced.ShipsOrdersOf(cartId)`, the handler always registered) instead of the "with shipping" app, so `AppApi.ShippingPlacedOrders()` is gone; Marten `AppendedEventsTests` uses one `MartenApi` per class (class fixture) instead of 8 own apps with own schemas.
- No time-based waits in tests (qa 100), first pass: elapsed-time asserts removed from the deadline and "fails at once" tests in the API, Wolverine, Kafka, Marten and RabbitMQ samples (the failure message names the deadline). Messages that arrive later and cancellation while an expectation waits come from a probe in `RecordedMessages`, run by its read function at the first check (`AddingAfterTheFirstCheck`, `Adding(now, afterTheFirstCheck)`, `CancellingAtTheFirstCheck`). `ExpectMetric`'s cancellation comes from a telemetry source that cancels on the first check's `Collect`. The step cancellation tests cancel inside the step; Wolverine's Eventually cancellation cancels after the first attempt. The API sample's handler no longer sleeps 500 ms: `OrderHandling.Hold(cartId)` holds it until the test releases it after the response.
- Kept on their own app, with the reason: Wolverine `CatchUpTests.AppThatKeepsPublishing_FailsAtTheDeadlineSayingSo` starts a heartbeat that republishes itself until the app stops, so on the shared app every later catch-up would never settle; Wolverine `ActionAndPublishTests.ActionAdvancingAFakeClock_ExpiresTheCartAndPublishesCartExpired` advances its app's fake clock by a day, which on the shared app would expire other tests' carts; Marten `WithoutCatchUp` and `WithStalledProjections` run without the async daemon, so their app differs.
