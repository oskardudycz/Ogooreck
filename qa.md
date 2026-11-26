# Q&A: Emmett-style API tests in Ogooreck

Idea: make Ogooreck API tests closer to Emmett's `ApiE2ESpecification` and to `samples/e-commerce/src/test/java/io/eventdriven/testing/api/ApiE2ESpecification.java` in EventSourcing.JVM.

## 1. How should the Emmett-style API land in the .NET package?

Options: new type alongside the existing `ApiSpecification`; reshape the existing one (breaking); reshape and keep obsolete shims.

**Answer:** Reshape the existing `ApiSpecification` to the Emmett shape. This is a breaking major release for current users.

## 2. Beyond HTTP, which parts of the Java version are in scope?

Options: non-HTTP steps (action, publish, attempt); published/appended/causation expectations; traces and metrics; catch-up waiting instead of `Until`/`RetryPolicy`.

**Answer:** Catch-up waiting, traces and metrics, and published/appended. Traces and metrics definitely. For messaging, we need to work out how to integrate smoothly with specific messaging tools without ending up with the lowest common denominator. Non-HTTP steps were not selected (to be clarified).

## 3. How should Ogooreck integrate with messaging and storage tools?

Options: core hook plus adapter packages per tool; core envelope plus mappers; core hook only, users write their own.

**Answer:** Definitely a core hook. Ogooreck should not need, or force, an adapter for each possible messaging tool or queue. Adapters are possible, but users must be able to plug in their own tool easily.

## 4. What does a user write to plug their own bus or event store into the core hook?

Options: a recorder plus a catch-up delegate; an `IProbe` interface to implement; both layers.

**Answer:** Unclear how the recorder option would work; asked for a concrete explanation. Constraint added: a Java version of Ogooreck is planned, so the .NET and Java APIs must be relatable. They should not be a lowest common denominator, but close.

## 5. (Follow-up) Recorder `Recorder<T>` that the tool's hook writes to, plus a catch-up function, same shape in .NET and Java?

Options: recorder plus catch-up; `IProbe` interface; both.

**Answer:** Still unclear how this plugs in for Kafka, RabbitMQ, MassTransit, Spring Boot and similar. Asked whether .NET channels should be used.

## 6. (Follow-up) Channel that Ogooreck creates and the user's observer writes to?

**Answer:** The previous answers still did not explain how it works; asked for a real explanation instead of more options.

Explanation given, as a full walkthrough:

- Ogooreck ships one type, `Recorded<T>`, a pipe over `Channel<T>` in .NET (`LinkedBlockingQueue<T>` in Java) with an `Add(T)` method.
- The user writes one observer per tool and test project. Examples:
  - Kafka: a test-side consumer in its own consumer group that calls `published.Add(consumer.Consume())`.
  - MassTransit: an `IPublishObserver` whose `PostPublish` calls `published.Add(context)`, registered with `AddPublishObserver`.
  - Java: `bus.use(published::add)` or a test-side Kafka consumer.
- At runtime, the Given and When requests run, and the app's messages flow into the channel.
- `ExpectPublished(published, new OrderPlaced(orderId))` reads from the channel and maps each item to a comparable message with a `read:` function (deserialize the Kafka value, or take `context.Message`). It stops when every expected message is found, or fails after a timeout and lists what it saw.
- Open problem: which messages belong to the When step. Messages from the Given steps, from parallel tests on the same broker, and late messages from Given are in the channel too.

## 7. How does Ogooreck tell which recorded messages belong to the When step?

Options: trace context (Ogooreck sends a fresh `traceparent` per step, and the read function returns the message's `traceparent`); mark plus quiet period; user filter (`isAbout`); trace context with filter fallback.

**Answer:** Trace context. Constraints: recording must not slow down the messaging tool, and it must not run outside the test environment.

## 8. Which non-HTTP steps should Given and When support?

Options: attempt (a Given request allowed to fail); action (any async function, such as advancing a fake clock); publish through the user's own tool; HTTP only.

**Answer:** Steps work as in the Java version and Emmett, and they are pluggable in the same way as the messaging hook. So: requests, attempts, actions, traced steps and publishing, where the user plugs in how to publish with their own tool.

## 9. How is a request step written?

Options: a lambda over `HttpClient`, as in Emmett and Java; keep the `SEND(URI, POST, BODY)` DSL; both.

**Answer:** A lambda over `HttpClient`, for example `client => client.PostAsJsonAsync($"/carts/{cartId}/orders", new { })`. The `SEND`/`URI`/`BODY` DSL goes away.

## 10. Where do `ExpectTrace`, `ExpectCorrelatedTrace` and `ExpectMetric` read spans and metrics from?

Options: built-in `ActivityListener` and `MeterListener`; the OpenTelemetry in-memory exporter; a pluggable source.

**Answer:** A dependency on OpenTelemetry is fine if it is not cumbersome and makes things simpler.

Resulting decision (stated back, open to objection): use the built-in `ActivityListener` and `MeterListener`, because they need no setup from the user, while the in-memory exporter must be registered in the test host's pipeline. OpenTelemetry packages are allowed where they save work, such as semantic-convention names.

## 11. How do later steps use server-generated ids from earlier responses?

Options: steps see prior responses (`(client, ctx) => ...` with `ctx.CreatedId()`); test-generated ids only, as in Java and Emmett; a separate setup call.

**Answer:** Asked which is better. Recommendation accepted into the spec: steps see prior responses. A request lambda has two overloads, `client => ...` and `(client, ctx) => ...`, and `ctx` exposes the earlier responses and `CreatedId()`. This keeps the documented `GetCreatedId` pattern for apps with server-generated ids (EF identity, Marten CombGuid, `Location` headers). Tests that make their own ids ignore `ctx`, and the same shape ports to Java.

## 12. How strict is the JSON body comparison in `ExpectResponse(status, Body(expected))`?

Options: partial, as in Java (extra response fields allowed, array order matters); exact; both, as `Body` and `BodyContaining`.

**Answer:** Partial, as in Java: the fields in `expected` must match, extra fields in the response are allowed, and array order matters. It uses Ogooreck's existing `StructuralComparison`.

## 13. What do `ExpectPublished` and `ExpectAppended` mean when messages arrive over a channel?

Options: exact after a short settle window, or after a plugged catch-up function; contains, in order, passing as soon as found; both, named.

**Answer:** No time-based waits or retries that always run. A deadline is fine as the bound for failing.

## 14. With no fixed waits, how do exact checks such as "nothing else published" or `ExpectNothingPublished` know the app is done?

Options: an optional catch-up function per recorder; a required catch-up function; contains-only checks.

**Answer:** An optional catch-up function per recorder, for example MassTransit's `InactivityTask`, Marten's `WaitForNonStaleProjectionDataAsync` or Java's `bus.whenCaughtUp`. With it, `ExpectPublished` compares exactly and `ExpectNothingPublished` works. Without it, `ExpectPublished` passes as soon as the expected messages have arrived in order, and `ExpectNothingPublished` fails with "needs a catch-up function". In both cases the deadline only bounds the failure.

## 15. What are the entry point and its methods called?

Options: `ApiE2ESpecification`, as in Emmett and the Java sample (PascalCase in .NET, camelCase in Java); keep `ApiSpecification`.

**Answer:** `ApiE2ESpecification`. The old `ApiSpecification` name is removed.

## 16. Which real tools prove that the hooks are pluggable?

Options: MassTransit in-memory; Kafka with Testcontainers; Marten with PostgreSQL; Wolverine.

**Answer:** All four: MassTransit in-memory, Kafka with Testcontainers, Marten with PostgreSQL, and Wolverine.

## 17. Where do the MassTransit, Kafka, Marten and Wolverine integrations live?

Options: samples only; optional NuGet packages; samples now, packages later.

**Answer:** Samples now, packages later. This release ships each integration as a sample test project, with README snippets taken from it. The spec lists the packages as a follow-up for when the hook shape settles.

## 18. How does the breaking change ship, and what do current users get to migrate?

Options: 0.11.0 with a migration guide; 1.0.0 with a migration guide; 0.11.0 with release notes only.

**Answer:** 1.0.0 with a migration guide. New requirement: it must work with Aspire.

Consequences of Aspire (`DistributedApplicationTestingBuilder`, where resources run as separate processes and the test gets an `HttpClient` from `app.CreateHttpClient("api")`):

- `ApiE2ESpecification` takes any `HttpClient` factory and must not depend on `WebApplicationFactory`.
- Broker-side observers, such as a Kafka consumer, work. In-app hooks, such as a MassTransit observer or Wolverine middleware, don't, because they would run in another process.
- In-process `ActivityListener`/`MeterListener` (answer 10) can't see spans and metrics from Aspire resources, which export them over OTLP.

## 19. Under Aspire, how do `ExpectTrace` and `ExpectMetric` get spans and metrics from resources in other processes?

Options: a pluggable source with built-in in-process listeners and an OTLP receiver hosted in the test; in-process only for now; an OpenTelemetry Collector container that exports to a file.

**Answer:** A pluggable source, like messaging, with two built-ins. In-process `ActivityListener`/`MeterListener` cover `WebApplicationFactory`. For Aspire, Ogooreck hosts an OTLP/HTTP receiver in the test, and the Aspire sample points each resource's `OTEL_EXPORTER_OTLP_ENDPOINT` at it. This adds an OpenTelemetry proto dependency. It refines answer 10.

## 20. Which of these are in this release's scope?

Options: an F#-friendly API and sample; test-framework agnostic; a .NET-to-Java mapping in the spec; reshaping the business logic specs too.

**Answer:** In scope: an F#-friendly API with an F# sample; test-framework agnostic, with failures as plain exceptions and no framework dependency; a .NET-to-Java name and shape table in the spec (no Java code in this release). On business logic specs, asked whether they need reshaping.

The check showed they already match the Java `EventSourcedSpecification` and Emmett's `DeciderSpecification`: given events or state, when a command, then events, state, assertions or throws. The only gap is a named "nothing happened" check, which an empty `Then()` covers. Decision: business logic specs are out of scope and unchanged.

## 21. How does a test class set up its `ApiE2ESpecification`?

Options: a builder whose lifetime the user owns in their framework's fixture; an abstract base class, as in Java.

**Answer:** As much as possible predefined, with safe defaults, and a pluggable host. Unsure whether a base class is needed.

Decision (stated back): a builder, with no base class. A base class adds nothing that defaults can't, and it fits F# and per-framework fixture lifetimes poorly. Defaults:

- `ApiE2ESpecification.For<TProgram>()` starts a `WebApplicationFactory<TProgram>`. JSON options come from the app's services, in-process trace and metric listeners are on, each step sends a fresh `traceparent`, and the deadline is 30 seconds, as in Java.
- `.Host(...)` plugs any host: a customized `WebApplicationFactory`, or `() => aspireApp.CreateHttpClient("api")`. For a non-in-process host, telemetry is off until `.Telemetry(...)` adds a source such as the OTLP receiver.
- `.Recording(recorded, read, catchUp?)` adds message and event sources, which are always opt-in.

## 22. Metrics carry no trace id, so `ExpectMetric` can count other parallel tests. How is that handled?

Options: a tag filter plus documentation; exemplars by trace id; an at-least increase.

**Answer:** Unsure; the guess was option 1 or 2. Decision (stated back): both, by source.

- In-process, measurements count only when made inside the test's traces. `MeterListener` callbacks run inline on the recording thread, so `Activity.Current` is the request's activity.
- Out of process (OTLP) and for observable instruments, a tag filter applies. OTLP metrics arrive pre-aggregated, and exemplars are samples, not counts. The docs say to filter by a test-unique tag or run metric tests serially.

## 23. What replaces `Until(...)` for apps with no catch-up signal?

Options: an opt-in `Eventually(request)` step; catch-up only.

**Answer:** An opt-in `Eventually(request)` step. It re-sends the request only while its When response expectations fail, and stops at the first pass or at the deadline. It is never on by default, and the docs prefer a catch-up function.

## 24. (spec r1) Which test seam does the spec use?

Proposed: one seam, the public specification API called from test projects against real sample apps. Passing scenarios plus scenarios that assert the thrown failure message; no unit tests of internal types; the existing `StructuralComparison` tests stay as they are.

**Answer:** One seam, as proposed. Oskar also said the `ApiSpecification` name had been agreed, which conflicts with answer 15 (`ApiE2ESpecification`); asked to confirm.

## 25. (spec r1) Which name does the reshaped entry point have?

Options: `ApiSpecification` (reverses answer 15); `ApiE2ESpecification` (keeps answer 15).

**Answer:** `ApiSpecification`. This reverses answer 15: the existing type keeps its name and gets the Emmett-shaped API. The Java mapping notes that Emmett and the Java sample call it `ApiE2ESpecification`.

## 26. (spec r1) Where does the OTLP/HTTP receiver live?

Options: a separate `Ogooreck.OpenTelemetry` package; core `Ogooreck`.

**Answer:** Core `Ogooreck`. Every user gets the protobuf dependency.

## 27. (spec r1) Do the stated-back decisions from answers 10, 19, 21 and 22 stand?

**Answer:** It depends on how the builder API handles defaults. Shown:

- `For<TProgram>()` defaults to a `WebApplicationFactory<TProgram>`, JSON from the app's services, in-process telemetry on, a 30-second deadline, and no recorders, catch-up or publishing.
- `For(Func<HttpClient>)` defaults to `ApiJsonSettings.Web`, telemetry off, and a 30-second deadline.
- Every builder call is optional and replaces its default without merging: `.Host`, `.Recording`, `.CatchUp`, `.Deadline`, `.Telemetry(Telemetry.Off | OtlpReceiver.Start() | custom)`.
- Rule: what Ogooreck can do safely by itself is on; what needs the app or infrastructure is opt-in.

## 28. (spec r1) Do the builder defaults work?

Examples shown inside the question: zero config, overrides, and Aspire.

**Answer:** OK. The decisions from answers 10, 19, 21 and 22 stand. Follow-up question: would people always need to provide that setup, and how is it accessible?

## 29. (spec r1) How do tests get the configured specification?

Explained: setup lives in one class per test project (for example `AppApi`). Every test class reuses it through `IClassFixture<AppApi>`, or the whole project shares one host through a collection fixture, xUnit v3 `AssemblyFixture`, NUnit `SetUpFixture` or MSTest `AssemblyInitialize`. Zero config stays as today: `IClassFixture<ApiSpecification<Program>>`. Adapter packages (follow-up) would shrink the setup later.

Options for that one class: a subclass that passes its configuration to the base constructor (tests call `api.Given(...)`); or a fixture that holds a built specification (tests call `api.Spec.Given(...)`).

**Answer:** Both. `ApiSpecification<TProgram>` has a constructor that takes a configuration function, for subclasses, and `ApiSpecification.For<TProgram>()` returns the same type, for fixtures. The host starts lazily on the first step, so async hosts such as Aspire need no async fixture hook.

## 30. (spec r1) What does the user plug in for Aspire and other out-of-process hosts?

Context: each step must send its own `traceparent`. Ogooreck can add it only through a handler on a client it builds itself. A finished `HttpClient` from `app.CreateHttpClient("api")` can't get a handler afterwards, and `DefaultRequestHeaders` races when tests share the client in parallel.

Options: a base address, with an optional `HttpMessageHandler` factory; a client plus a header the request lambda adds by hand.

**Answer:** A base address, for example `Host(async () => app.GetEndpoint("api"))`. Ogooreck builds the client with its trace handler. An overload also takes an `HttpMessageHandler` factory for custom TLS or resilience. This refines answer 18 ("takes any `HttpClient` factory").

## 31. (spec r2) How are `Body` and message comparisons ordered, given that `StructuralComparison` ignores order in nested collections?

Explained: today, decider specs compare the top-level event list in order, while nested collections (and today's `RESPONSE_BODY`) ignore order. Answer 12 and the Java version (JSONAssert `STRICT_ORDER`, AssertJ recursive comparison) compare nested arrays in order.

**Answer:** Standing rule: in general, when something is unclear, do what the Java version does. Applied here, `Body` and message expectations compare nested collections in order, like Java, through an ordered mode of `StructuralComparison`. Business logic specs keep today's behavior (answer 20).

Resulting decisions, from the standing rule and the Java `ApiE2ESpecification`:

- Span attribution uses the trace ids of all the test's steps, Given and When (Java's `traceIds` set). In-process measurements count when made inside any of the test's traces (answer 22: "the test's traces"). Message attribution stays with the When step (answer 7).
- Configuration is class-level, like Java's `catchUpTimeout` field: configuration calls are allowed only before the first step, and calling one afterwards throws. A test that needs other settings uses another specification instance.
- `Scenario(TestStep[] scenario, params TestStep[] more)` stays as Java's `scenario(...)` helper that joins step arrays. The removed 0.x `Scenario` is the response-chaining one.
- A catch-up round repeats while any recorder received any new item, like Java's `published.size() != seen`.
- `ExpectCorrelatedTrace` checks once, after catch-up, like Java. `ExpectCausation` follows its recorders' mode: once after catch-up when they have catch-up, otherwise waiting until the tree is found or the deadline is reached.
- Metric increase: a missing baseline counts as 0 (Java's `before.getOrDefault(id, 0.0)`). Observable instruments are read at the baseline and at check time and treated as cumulative (Java's `FunctionCounter.count()`).
- The deadline applies to each catch-up (Java's deadline per `whenCaughtUp`) and to each waiting expectation separately.
- `Then` keeps a `CancellationToken` overload: `Then(Expectation[] expectations, CancellationToken ct)`. The existing 0.x `Then(thens, ct)` supports it; Java has no counterpart.

## 32. (spec r2) Where do the OTLP protobuf types come from?

Options: vendored `.proto` files with `Grpc.Tools` code generation; an existing NuGet package.

**Answer:** No custom build step unless it is justified. The check showed that no maintained NuGet package ships OTLP proto types: the official exporter keeps its serializer internal and write-only. The .NET OTLP exporter can't send OTLP/JSON. Follow-up options: hand-decode the subset Ogooreck needs, or use `Grpc.Tools` code generation.

**Answer:** Hand-decode the subset with `Google.Protobuf`'s `CodedInputStream`. Spans need trace id, span id, parent span id, name and attributes; metrics need name, sum and gauge data points, attributes and temporality. No code generation and no build step.

## 33. (spec r2) How is "works under any test framework" proven?

Options: narrow the criterion to "no framework reference, plain exception"; a tiny project per framework.

**Answer:** A tiny project per framework. NUnit, MSTest and TUnit test projects each run one passing and one failing scenario.

## 34. (spec r2) The spec-bug findings from the r1 review

**Answer:** Fix as suggested:

- Add a non-generic `For(...)` for out-of-process hosts.
- Add the docs guidance on metric isolation, and the preference for catch-up over `Eventually`.
- `Observed` gets two constructors and no default parameters.
- Rename to avoid clashes: `TelemetrySources.Off`/`InProcess`, and recorders named `PublishedMessages`/`AppendedEvents` in the samples.
- Rewrite the attribution criteria so they fail without attribution.
- Add criteria for `Json(...)`, async `Satisfying`, how `Eventually` handles the other expectations, and a custom `ITelemetrySource`, and define that interface's members.

## 35. (spec r3) How do out-of-process hosts get their address? (r2 review Q&A-1)

Options: both creation-time and fluent; fluent `.Host` only; at creation only (r2).

**Answer:** Both. `ApiSpecification.For(address)` serves fixtures, and the constructor serves subclasses. In addition, a fluent `.Host(address)` on the non-generic type matches the example in answer 30. Configuration stays a `Func`, so `spec => spec.A().B()` works in C# and F# without `ignore`.

## 36. (spec r3) How does `ExpectMetric` treat an in-process measurement with no current `Activity`? (r2 review Quality-4)

Options: count by tags (as observable and OTLP measurements, and as Java); drop it.

**Answer:** Count by tags. The docs warn that parallel tests may add to it.

## 37. (spec r3) What happens over OTLP when a Given step's metric export hasn't arrived before the baseline? (r2 review Quality-5)

Options: accept it as a known limitation and document it; wait for an export before the baseline.

**Answer:** Both. By default it is accepted and documented: OTLP metrics should be tagged with something only the When step touches. The wait is opt-in on the OTLP receiver: before the baseline and before each check, its `Collect` waits for the next metrics export, bounded by the deadline.

## 38. (spec r3) The other r2 review findings

**Answer:** Yes, as proposed.

- From the Java rule (answer 31):
  - Histograms count measurements, not values, and the OTLP decoder also reads histogram counts.
  - Every `Eventually` attempt's trace id joins the test's set for spans and measurements; messages count only from the final attempt.
  - AC 27.5 is dropped: causation is checked within a trace, as with Java's `isBelow`.
  - `Body` serializes the expected value with the specification's options and compares JSON, as Java does. AC 6.4 uses snake_case.
  - Span attribute values become strings as `String.valueOf` does: lowercase booleans and invariant-culture numbers.
- Spec fixes:
  - Each public type declares its configuration calls with its own return type.
  - `TelemetrySources.InProcess()` creates a new source for each specification.
  - `ITelemetrySource.Start` is renamed `Attach`.
  - The Aspire sample creates the OTLP receiver before the specification and uses its `Endpoint` when it starts the app.

## 39. (plan r2) What happens to the 0.x types and helpers the spec doesn't name (`TestContext`, `MadeApiCall`, `ApiTestStep`, `RequestDefinition`, `ResponseAssert`, `RequestTransform`, `GetETagValue`, `TryGetETagValue`, `TryGetCreatedId`)?

Options: remove the types and keep the helpers; remove all of them.

**Answer:** Keep only those the new code uses; remove the rest. The two `TestContext` tests in `ApiJsonSettingsTests` go with `TestContext`, unless the new code still uses it.

## 40. (plan r2) What do message expectations do with an `Observed` whose `traceparent` is present but malformed?

**Answer:** Ignore it, the same as when there is no `traceparent`.

## 41. (plan r2) Which package versions do the samples use?

Options: MassTransit 8 (Apache-2.0) with the rest at their latest stable version; MassTransit 9 (commercial).

**Answer:** MassTransit 9. The other packages use their latest stable version, at least two weeks old, pinned exactly.

## 42. (plan r2) Where does the migration guide live?

**Answer:** `MIGRATION.md` at the repository root, linked from the README.

## 43. (plan r3) xUnit v3's analyzer xUnit1051 flags every `Then(...)` call without a `CancellationToken`, because `Then(Expectation[], CancellationToken)` exists. What does Ogooreck do?

Options: pass the token in the samples; suppress the analyzer and document it; take the token through configuration.

**Answer:** Pass the token in the samples. Both overloads stay (AC 32.1). The sample tests pass `TestContext.Current.CancellationToken`, as xUnit advises, with no suppression. The README notes the analyzer for xUnit users.

## 44. (spec r4, plan r3) How long does a request step's `HttpClient` live? (step 1 review, Step-4)

Options: a new client per step; one shared client.

**Answer:** Be careful about performance, and don't recreate the client or host unless needed. Check best practice and how Alba does it.

Findings:

- Alba creates the host once and shares it ("expensive. Create it once and share it"), and sends each scenario through `TestServer` with its own request context, without an `HttpClient`.
- Microsoft's guidance is that the handler is the costly part, while an `HttpClient` is a thin wrapper. Short-lived clients over a reused handler with `disposeHandler: false` are the documented pattern.

Resulting decision (stated back):

- The host is created once per specification.
- One handler is shared per specification: the `TestServer` handler in-process, and one `SocketsHttpHandler` (or the user's handler) out of process.
- Each step gets a fresh, lightweight `HttpClient` over that handler, with its `traceparent` handler in front and `disposeHandler: false`.

A step can then change the client's defaults without affecting other tests, and no host or connection is rebuilt.

## 45. (spec r4, plan r3) How is a server-generated `Location` checked? (step 1 review, Step-8)

Options: drop the check from that test; add a predicate overload.

**Answer:** Add a predicate overload: `Location(Func<string, bool>)`, for example `Location(l => l.StartsWith("/products/"))`.

## 46. (spec r4) Redirects, cookies and response buffering (step 1 review, Step-5 and Step-7, settled by the standing rule in answer 31)

- The 1.0 client doesn't follow redirects or keep cookies, like Java's `RestTestClient`, which is why `RedirectsTo` checks the 3xx. The migration guide gets a row: 0.x tests that expected the final `200` after a redirect now see the `302`/`303`, and should use `RedirectsTo(...)` or request the target.
- Every response body is read into memory, as Java's `returnResult()` does. "Work added per operation" lists it.

## 47. (spec r5) Step 1 re-review, Step-10: the spec lists `GivenApiSpecificationBuilder` and `WhenApiSpecificationBuilder` as removed, but step 1 kept those names for the new Given/When chain, following the `*SpecificationBuilder` convention

**Answer:** Oskar didn't see the issue. Explained: it is only a wrong sentence in the spec, which would make the migration guide say the types are gone. Fixed by wording (open to veto): both types keep their names with the new shape, their 0.x members are removed, and the migration table gets a row for them. No code changes.

## 48. (spec r6) Standing requirement on error messages

**Answer:** "We need to have clear and precise error messages telling what's the issue." Raised after the step 1 cancellation deviation, where another error was rethrown as `OperationCanceledException`.

Resulting rules (stated back):

- Every failure Ogooreck throws says what failed and where: the phase (Given step n or When), the step kind, and for requests the method and URI. It says what was expected and what was observed.
- A wrapped error keeps the original as the inner exception and repeats its message in the outer one.
- Cancellation: the `OperationCanceledException` message says that `Then` was cancelled, and while which step or wait. If the step also failed in another way, the message names that error too.

## 49. (spec r6) Step 1 re-review, Step-11: how does cancellation reach a running action?

Settled from the spec text "Cancelling `ct` stops steps", which a running action can't follow without the token: `StepContext` gets `CancellationToken`, the `Then` token. Publish functions already get it as an argument.

## 50. (plan r3) MassTransit 9 needs a license key even for the in-memory transport (step 2 blocker)

Verified with 9.2.5: the bus refuses to start without `MT_LICENSE`/`MT_LICENSE_PATH`.

**Answer:** Already answered in qa 41: MassTransit 9. The sample pins 9.2.5 and reads the license from `MT_LICENSE` or `MT_LICENSE_PATH`. Local runs need one of them set, and CI passes the repository secret `MT_LICENSE` to the test job. Oskar provides the key and the secret.

## 51. (spec r7, plan r4) Can Ogooreck get a free MassTransit license?

Checked: v9+ has no free open-source, individual or dev/CI tier. Organizations under $1M may get a 100% discount on request. The key must be on every developer laptop and CI agent, may not be committed, and is confidential, so outside contributors and fork PRs couldn't run the sample.

**Answer:** "Ok, then drop mass transit." This replaces answers 16, 41 and 50 for MassTransit: no MassTransit sample and no MassTransit dependency. Its scenarios move to the Wolverine sample, which was already in scope (answer 16). Wolverine covers the in-app hook, catch-up through its message tracking, exact `ExpectPublished`, `ExpectNothingPublished`, `ExpectCausation`, `Publish`, `Eventually` and parallel tests on one shared host. The F# sample takes a Wolverine scenario.

## 52. (spec r8, plan r5) How does the Wolverine sample catch up?

Wolverine's tracked sessions must wrap the code that sends.

**Answer:** Use Wolverine's native test support, not our own bus or counter. It must stay pluggable. Resulting decision: catch-up gets a wrapping form, `aroundStep`, which surrounds each step. The Wolverine sample plugs `TrackActivity().ExecuteAndWaitAsync(...)` through it.

## 53. (spec r9, plan r8) Criterion 16.2: parallel tests on one shared Wolverine host

Wolverine has one tracked session per host (`ActiveSession`). In 3 of 3 runs, parallel scenarios clashed, and Wolverine's docs run tracked-session tests in one collection.

**Decision (stated back, open to veto, given Oskar's "just simples option"):** remove 16.2. Criterion 18.4 already shows parallel attribution on a shared Kafka topic. The Wolverine sample runs serially, as Wolverine's docs do.

## 54. (spec r10) Step 2 review: wrapping catch-up edge cases, and the trace context of actions

**Decision (stated back, simplest consistent rule, open to veto):**

- A wrapping catch-up must call the step exactly once, and otherwise fails with a clear message.
- One deadline covers a step's whole catch-up, wrapping and plain functions together, as in Java. The step itself is bounded only by the `Then` token.
- Ogooreck runs actions and publish functions under an `Activity` parented on the step's `traceparent`, so 11.1 works as written and users don't need a `Traced(...)` helper.

## 55. (spec r11, plan r9) Strictland's rules on documentation and coverage, missed in the restructuring

**Answer:** Oskar: "I don't see any docs for public methods and members enforced and added." Strictland's `check` fails on a missing comment on any public or protected member (`javadoc -Xwerror`; each comment says what the member is for and shows a short example from a real test) and requires 100% line and branch coverage (`jacocoTestCoverageVerification`), reached through the public API.

Decision: Ogooreck does the same.

- Remove the `#pragma warning disable CS1591` suppressions. `GenerateDocumentationFile` plus `TreatWarningsAsErrors` then fail the build on any undocumented public member.
- A coverlet gate requires 100% line and branch coverage of the `Ogooreck` library, from all test projects, in `./build.sh test` and CI. An unreachable branch is removed, not covered artificially.
- AGENTS.md and CONTRIBUTING.md get strictland's "Tests, coverage, and documentation" section, adapted to .NET.
- This lands as its own step after step 3, documenting and covering steps 1–3. Every later step must keep both gates green.

## 56. (spec r12) Steps 4–5 review: the `WaitForMetricsExport` wait, and who owns a receiver passed in

**Decision (stated back, simplest consistent rule, open to veto):**

- `WaitForMetricsExport` waits for two more exports. The export in flight may have been collected before the latest increment, so only the second one is certain to carry it.
- A specification disposes only what it created. A telemetry source or `OtlpReceiver` passed in with `Telemetry(...)` belongs to the caller, as an out-of-process app does. This lets one receiver be shared by several fixtures.

## 57. (spec r13) The per-sample setup classes (`MartenApi`, `KafkaApi`, `AppApi`) don't compose and differ from Java. How should a tool plug in?

Context: Java plugs in through decorators on the app's own services (`RecordingEventStore` wraps the event store, `bus.use(recorded::add)` adds middleware to the bus), and each of its own tools implements `whenCaughtUp`. Alba has one generic extension hook; Wolverine's docs wrap Alba calls in `ExecuteAndWaitAsync`, whose tracked session also records the messages. Oskar: take Alba as inspiration, not a clone.

**Answer:** "I'd like to have some sort of decorators plugable and unified way with helpers so it was really easy to plug them also to things like Kafka, RabbitMQ etc. and have it easily done."

Proposal shown: a core decorator helper over the app's DI service (`DispatchProxy`, recording only the named calls), helpers `Observed.FromHeaders(...)` and native broker catch-ups (Kafka consumer-group lag, RabbitMQ empty queues with no unacked messages), and ready-made decorators for Kafka, RabbitMQ, Marten and Wolverine that compose.

**Addition:** "and also be able to do it in Java and later Node.js in a moderatily similar way."

## 58. (spec r13) How is a recorded broker message read into an `Observed`? Can Ogooreck require a convention such as CloudEvents?

Context: spikes against real brokers showed that recording through a DI decorator works (the Kafka sample passed with a `DispatchProxy` on `IProducer`), while catch-up from Kafka consumer-group lag or RabbitMQ probes is correct only under conditions the test can't control (offsets stored after handling; one ordered consumer per queue). Catch-up from OpenTelemetry messaging spans worked for RabbitMQ with concurrent dispatch and for Kafka through Aspire and OTLP, and is being spiked further. Spans carry the message id, destination and conversation id, but no message type or body, so `ExpectPublished` still needs recorded messages and a way to read them.

**Answer:** "I think that we can't enforce they will follow cloud events" and "We may have some recommendation and built in for that, but we can't force anyone".

Decision: Ogooreck recommends one header convention and ships a built-in reader for it, so apps that follow it need no `Read` code. The same reader takes the app's own header names and type lookup. A custom `Read` function is always allowed, as now. Which headers make up the recommended convention is still open.

## 59. (spec r13) Is the decorator proposal of answer 57 actually doable, and is it reliable?

Context: Oskar asked "And did you check if its actually doable and easy or you're just making things up? Stop swinging back and forth, focus, do proper research and assesment". Correction to answer 57: the proposal said RabbitMQ catch-up waits for empty queues with no unacked messages. Java does not do that. Its `RabbitMQMessageBus.whenCaughtUp` sends one probe message per queue and waits for it to come back through the queue's single consumer. Its `KafkaMessageBus.whenCaughtUp` waits until each consumer group's committed offsets reach the end offsets taken at the start.

Spikes, run in scratchpad copies against real brokers:

- Recording through a DI decorator: a `DispatchProxy` on the Kafka sample's `IProducer<string, string>`, added with `ConfigureTestServices`, replaced the test's side consumer. All 9 Kafka sample tests passed. RabbitMQ needs three levels of wrapping (`IConnection`, `IChannel`, the consumer passed to `BasicConsumeAsync`). Recording worked (3 of 3).
- Kafka consumer-group lag: with Confluent's default `EnableAutoOffsetStore = true`, the offset is stored when `Consume` returns, before handling. The lag check reported caught up with 2 of 3 messages handled. With the offset stored after handling, 3 of 3.
- RabbitMQ probe: with `ConsumerDispatchConcurrency = 1`, 3 of 3. With 4, the probe overtook the messages: 0 of 3.
- Decorators reach only clients that the app gets from DI and runs in the test process. Oskar: "I don't think that's entirely true that we can't override some stuff". The Aspire testing builder can set environment variables on the project, and `ASPNETCORE_HOSTINGSTARTUPASSEMBLIES` can add services to the app's DI from another assembly; the recorded messages would then have to travel back to the test. Not tested.

Oskar: "So why you said it's a good choice if lots doesn't work and is unreliable? What else can we do?" The proposal had been made before testing. Options shown: catch-up from OpenTelemetry messaging spans, Java's own model (the app's bus implements recording and catch-up), and the native tracking of frameworks such as Wolverine and MassTransit.

**Answer:** "then spike the telemetry catch up".

## 60. (spec r13) Does catch-up from OpenTelemetry messaging spans work?

Rule spiked: after each step, wait until every message sent in the test's traces has a finished consumer span, as its child or through a span link. Traces reached through links count as the test's traces.

Findings:

- RabbitMQ.Client 7 has built-in `RabbitMQ.Client.Publisher` and `RabbitMQ.Client.Subscriber` sources. The consumer's `deliver` span is a child of the publish span and lasts until the handler returns (1008 ms for a 1 s handler), also with `ConsumerDispatchConcurrency = 4`.
- Confluent.Kafka has no built-in tracing. `OpenTelemetry.Instrumentation.ConfluentKafka` exists only as an alpha (0.5.0-alpha.1 used), and the app must opt in: instrumented builders, and handling inside `ConsumeAndProcessMessageAsync`. By default the process span only links to the send span and starts its own trace; `UseCreationContextAsParent` makes it a child.
- Through Aspire and OTLP, with a two-hop chain (`orders` handler sends to `shipments`, a second consumer handles those), the rule reported caught up after 2.1 s with both hops handled, in both span modes. The OTLP decoder needed link decoding (Span field 13) for the default mode.
- Chains: a handler's send span ends in the same process before its own consumer span ends, so it is exported first. Reasoning, not tested across services.
- MassTransit 8.5.12 (Apache-2.0; 9.x has a commercial license): a producer `send` span, then a consumer `receive` span as its child that covers the handling. It uses the older attribute `messaging.operation`. The rule therefore has to use span kind (producer, consumer), which `RecordedSpan` and the decoder don't carry yet.
- Wolverine 6.63 with local queues: no producer spans, so a message not yet picked up is invisible to the rule.
- Retries: a failed first attempt (nack with requeue, or an exception followed by redelivery) ended a span with status `Unset`, no error tag and no exception event. The rule reported caught up before the retry handled the message.
- Spans carry the message id, destination and conversation id, but no message type or body.

Oskar asked "Can we take convnetional message info?" Spans give enough for causation, correlated traces and the destination, not for comparing message content. Oskar: "Also how would then the regular plugging work? We're kinda back to square one..." Shown: with catch-up in core, a plug only has to say where recorded messages come from (a DI decorator, a side consumer, or the tool's native hook); reading them is answer 58.

## 61. (spec r13) Fan-out: one message read by several consumers. "A consumer span" is satisfied by the first one.

**Answer:** "As an option, maybe."

Decision: declaring the number of consumers of a destination may come as an option.

## 62. (spec r13) Apps without messaging tracing, and tools with their own catch-up

**Answer:** "Fall back to the tool's own catchup, or if they have the tool then jus tuse it (is should be the most precise, right?"

Decision: when the app uses a tool that owns its consumers (Wolverine's tracked session, MassTransit's test harness, Marten's projection wait), Ogooreck uses that tool's catch-up. Telemetry catch-up is for raw clients with tracing. Raw clients without tracing fall back to Kafka consumer-group lag or the RabbitMQ probe, with their conditions documented. Agreed that a tool owning its consumers is the most precise; whether Wolverine's and MassTransit's tracking waits for retries is not tested. The raw-client lag and probe were less reliable than telemetry in the spikes.

## 63. (spec r13) Retries: catch-up from telemetry can't see that a failed attempt will be retried

Example: the shipping handler fails on its first try and RabbitMQ redelivers the message. Catch-up says done after the failed try, so a next-step GET sees "not shipped". Message expectations keep checking until the deadline and are not affected.

Oskar: "And what's the question here?"

**Open:** is it acceptable that catch-up doesn't wait for retries, documented, with such tests using `Eventually`, a message expectation or the tool's own catch-up? Or must catch-up wait for retries? No reliable signal for it was found in the spans.

## 64. (spec r13) A message nobody consumes never gets a consumer span, so catch-up would wait until the deadline

**Answer:** "Then they should be caught up right after if they don't have consumers?"

Decision: a message whose destination has no consumer needs no wait.

**Open:** how Ogooreck knows a destination has no consumer. From spans, "nobody listens" and "not picked up yet" look the same. Options shown: ask the broker (Kafka consumer-group assignments; RabbitMQ queue consumer count, plus exchange bindings through the management API; Ogooreck's own side consumer left out), not tested; or declare the consumed destinations in the plug.

## 65. (spec r13) Back to Java's model

On answer 63, Oskar: "I don't understand why." Why telemetry misses retries: a consumer span says only that a handler ran, and a failed try looks the same as a success. Java has no such gap. Its buses retry inside the consumer before acknowledging: RabbitMQ through a stateless retry interceptor (`RabbitMQMessageBus.java:390-400`), so the probe waits behind the message; Kafka through `DefaultErrorHandler` with a backoff (`KafkaMessageBus.java:374-407`), so the offset is not committed until the retries end.

On answer 64, Oskar: "Dude, why are you bs me like that. We want to have a consistent way to support what we have in java and easily pluggable".

Java's model: recording through decorators and middleware on the app's own services (`RecordingEventStore`, `bus.use(recorded::add)`), and `whenCaughtUp` per integration, checking only the consumers that integration knows. "A destination with no consumer" doesn't arise.

**Proposal shown (not yet agreed):** Ogooreck defines the same two hooks (record, catch up) and ships integrations for Kafka, RabbitMQ, Marten and Wolverine, each catching up as Java does: Kafka consumer-group lag, RabbitMQ probe, Marten projection wait, Wolverine tracked session. Integrations compose. Telemetry catch-up is dropped as the main mechanism. The gap to Java: Ogooreck doesn't own the app's consumers, so each integration states its conditions (offsets stored after handling, retries before acknowledging, one ordered consumer per queue) and checks them where it can reach the consumer's configuration.

## 66. (spec r13) Should catch-up use telemetry at all?

Oskar: "Then if telemetry has more issues than solutions then the question is whether we should use it. And I don't understand what's the question you need answer from me".

Recommendation shown: no telemetry for catch-up. It misses retries, can't tell "no consumer" from "not picked up yet", needs the app instrumented, and Wolverine's local queues emit no send spans. Telemetry stays for `ExpectTrace`, `ExpectMetric` and `ExpectCorrelatedTrace`.

**Open:** yes or no on Java's model from answer 65 (two hooks, record and catch up; integrations for Kafka, RabbitMQ, Marten and Wolverine catching up as Java does; one integration per tool).

## 67. (spec r13) If not for catch-up, what is telemetry for?

Oskar: "If not for catchup then for what besides thejava example does? Dude, we're running in circles, could you focus reread qa.md and stop bs me?"

Answer given after rereading answers 1–66: for what Java uses it for, and nothing more: `ExpectTrace`, `ExpectCorrelatedTrace` and `ExpectMetric` (answers 10, 19, 22), already built. Telemetry catch-up (answers 60–64, 66) is dropped.

What answers 3, 14, 31, 52, 57 and 58 already settle: the core hook stays `Recording(recorded, read, catchUp | aroundStep)`, repeatable, which matches Java's recorder plus `whenCaughtUp`. What answer 57 asks for is helpers that produce what this hook takes, each catching up as Java does (answer 31):

- Kafka: a decorator on `IProducer` for recording, consumer-group lag for catch-up (Java's `KafkaMessageBus`).
- RabbitMQ: a decorator on `IConnection` for recording, a probe per queue for catch-up (Java's `RabbitMQMessageBus`).
- Marten: its listener and projection wait. Wolverine: its tracked session.
- The conditions are Java's (offsets stored after handling, one ordered consumer per queue) and are documented. `MartenApi`, `KafkaApi` and `AppApi` become helper calls, and helpers for several tools compose.

**Pending:** spec r13 and the plan get written from this unless Oskar objects.

## 68. (spec r13) The five conclusions

Conclusions shown: (1) the hook `Recording(recorded, read, catchUp)` stays as it is; (2) ready-made helpers for Kafka, RabbitMQ, Marten and Wolverine replace `MartenApi`, `KafkaApi` and `AppApi`; (3) recording uses decorators on the app's clients; (4) catch-up copies Java: Kafka consumer-group lag, RabbitMQ probe per queue, Marten projection wait, Wolverine tracked session; (5) telemetry is not used for catch-up, only for `ExpectTrace`, `ExpectCorrelatedTrace` and `ExpectMetric`.

**Answer:** "ok"

Decision: the five conclusions above. Spec r13 and the plan are written from them.

## 69. (spec r13) Where do the Kafka, RabbitMQ, Marten and Wolverine helpers live?

Options: separate packages (`Ogooreck.Kafka`, `Ogooreck.RabbitMQ`, `Ogooreck.Marten`, `Ogooreck.Wolverine`), with core keeping the hook and the DI decorator helper; files in the samples; the main package with every broker client as a dependency.

**Answer:** Separate packages.

Decision: one package per tool. The core `Ogooreck` package keeps the hook and the DI decorator helper and references no broker client.

## 70. (spec r13) Which header names does the built-in reader recommend? (answer 58)

Options: Java's names (`messageId`, `correlationId`, `causationId`, `traceparent`, plus a `messageType` header, since Java's `__TypeId__` is a Spring class name); the Kafka sample's names (`message-id`, ...); CloudEvents names.

**Answer:** Java's names.

Decision: `messageId`, `correlationId`, `causationId`, `traceparent` and `messageType`. The reader takes other names and a type lookup.

## 71. (spec r13) Does 1.0 add a RabbitMQ sample?

**Answer:** Yes.

Decision: a small in-process app on RabbitMQ.Client with a Testcontainers RabbitMQ proves the RabbitMQ helper, tested like the Kafka sample.

## 72. (spec r13) How does a test project plug a helper into its specification?

Options shown with code: one repeatable `Use(plug)` call, with a small plug interface in core that each helper package implements (it decorates the app's services in-process and adds its `Recording` and catch-up); or explicit pieces, a repeatable `Services(...)` call plus the helper's decorator, recorder, reader and catch-up passed to `Services` and `Recording` by hand.

**Answer:** One `Use(plug)` call.

Decision: core gets a plug interface and a repeatable `Use(plug)` configuration call. Each helper package ships plugs. The `Recording`, `CatchUp` and `Publishing` calls stay, and a plug uses them underneath.

## 73. (spec r13) Details chosen while writing spec r13, not yet confirmed by Oskar

**Open:** each of these is in spec r13 and plan r11 and waits for Oskar's yes or no.

1. `PlugRegistration` mirrors the configuration calls (`Services`, `Recording`, `CatchUp`, `Publishing`) and adds `Starting` (for test-side consumers) and `AppServices` (for catch-ups that need the app's services, such as Marten's projection wait).
2. `Services(...)` on an out-of-process specification throws at `Use(...)`.
3. One publish function per specification: a second `Publishing(...)` throws, naming both sources.
4. Plugs that hold resources (`KafkaPlug.Consuming`, `RabbitMQPlug.Consuming`) belong to the caller, as an `OtlpReceiver` passed to `Telemetry(...)` does (answer 56).
5. `messageType` holds the type's `FullName` or `Name`, matched against types the user lists, or the user's own lookup. Bodies are JSON with web defaults.
6. Kafka catch-up needs the consumer groups listed (`groups: [...]`); the plug can't discover the app's groups. Without groups, no catch-up.
7. Kafka records `ProduceAsync` only; synchronous `Produce` is out of scope.
8. The catch-up conditions (Kafka offsets committed after handling; RabbitMQ one in-order consumer, retries before ack) are documented, not checked, because the plugs can't see the app's consumer setup.
9. Marten and Wolverine plugs are in-process only. RabbitMQ `Consuming` has no catch-up.
10. All five packages ship as 1.0.0 from the `dotnet-1.0.0` tag.

Untested parts, to be spiked at the start of plan parts 7b and 7c: reading Kafka group assignments and committed offsets through `IAdminClient`; the RabbitMQ probe recognized inside the decorated consumer.

## 74. (spec r13) Are there blockers, or cases that won't work?

Oskar: "Are there blockers, or unsuported cases that won't work?"

Answer given: no hard blocker found; two parts are unspiked (qa 73). Cases the plugs don't support, with what is tested and what is reasoning:

- Clients the plug can't reach: producers or connections created inside the app's own classes instead of resolved from DI, concrete classes rather than interfaces, keyed services, an `IConnectionFactory` the app uses to open its own connection, and tools that own their client (MassTransit, NServiceBus, EasyNetQ). Nothing gets recorded; those apps use the tool's own harness or a custom plug. Reasoning from how `Decorate` works.
- Kafka with Confluent's default `EnableAutoOffsetStore = true`: catch-up reports done before the handler finishes. Tested in the spike (2 of 3). Most apps run with this default.
- Kafka retries through retry topics or dead-letter topics: the offset is committed when the message moves, so catch-up ends early unless the retry topic's group is listed. Reasoning.
- Kafka groups not yet joined when a step runs: if assignments come from the broker's group description, an empty group looks caught up. Reasoning, part of the unspiked `IAdminClient` path.
- RabbitMQ with more than one consumer per queue, dispatch concurrency above 1, or retries through nack and requeue or delayed-retry exchanges: the probe comes back before the work ends. Tested for concurrency 4 (0 of 3).
- RabbitMQ queues consumed by another process: not discovered, so not waited for. RabbitMQ.Client 6 (`IModel`) is not supported. Reasoning.
- Out-of-process apps: no RabbitMQ catch-up, no Marten or Wolverine plug, so `ExpectNothingPublished` and exact checks are unavailable there except Kafka with groups (untested).
- Wolverine tests run serially. Wolverine over an external broker, and an app that both Wolverine and a broker plug would record, are untested.
- Java has none of these gaps because it owns its consumers and checks its own conditions.

## 75. (spec r13) Ways to get correct catch-up in default configuration

Oskar: "Then think on other ways to get proper outcomes. We can't handwave them if it won't work in default config"

Proposal shown, all untested until spiked:

- Rule: a setup the plug can't handle fails loudly, never passes early. A plug that never saw the app resolve its client (producer, consumer, connection) fails the first step and says why; a Kafka group the plug can't observe fails catch-up the same way.
- RabbitMQ: probe plus an in-flight counter in the decorated consumer. The probe is dispatched only after every earlier delivery was dispatched; catch-up then waits until handlers that started before it finish. That covers dispatch concurrency above 1. A `BasicNack`/`BasicReject` with requeue through the decorated channel counts as pending until the redelivery is handled. The client's default (`ConsumerDispatchConcurrency = 1`, no retries) already works with the plain probe. Retries through dead-letter exchanges with a TTL stay invisible.
- Kafka, option 1: decorate the app's `IConsumer` from DI. With a consume loop that handles each message before the next `Consume` (Confluent's documented pattern), "the consumer is back inside `Consume` and its position reached the end offsets taken at the round start" means handled. Works with `EnableAutoOffsetStore = true` and with retries inside the loop. Needs the consumer resolved from DI; apps that call `new ConsumerBuilder(...).Build()` need a one-line change.
- Kafka, option 2: no code change. Produce a probe to each partition the group consumes and wait until the group's committed offset passes it; with a sequential loop and default auto-store, that proves the earlier messages were handled. Costs up to `AutoCommitIntervalMs` (5 s by default) per catch-up round, and the probe reaches the app's handler.
- Apps that hand messages to other tasks or channels: no outside signal exists; the app supplies its catch-up function through the core hook, or tests use subsequence checks and `Eventually`.

**Open:** which spikes to run: RabbitMQ probe plus in-flight counter with concurrency 4 and with nack-requeue; Kafka option 1 and option 2 with Confluent's default consumer config.

## 76. (spec r13) Answers on qa 75

**Answer:** "1-2 ok." Decision: plugs fail loudly on setups they can't observe, never pass early; RabbitMQ catch-up is the probe plus an in-flight counter in the decorated consumer, with nack/reject-requeue counted as pending. Both still to be spiked.

**Answer on Kafka:** "Can we have that configured? With default to 1?" Decision: the Kafka plug's catch-up is an option, defaulting to option 1 (decorated `IConsumer` from DI), with option 2 (probe plus committed offsets, no code change) as the alternative. Both still to be spiked.

**On apps that hand messages off:** Oskar: "I'm not sure what does it mean". Explained with an example of a consume loop that passes each message to a background task and calls `Consume` again at once, so being back in `Consume` no longer means handled. Open: whether this case is supported, and how.

## 77. (spec r13) Does Confluent's default way work? How would an app's own idle signal work?

Oskar: "I'm not sure how would it work with this orderworker waituntilidle. So the default way confluent does would work?"

Answer given: yes, by reasoning, not yet spiked. Confluent's documented loop handles each message before the next `Consume`, so option 1 (decorated consumer back in `Consume` at the end offsets) and option 2 (probe passed by committed offsets) both hold with Confluent's default consumer settings. The 2-of-3 failure in answer 59 came from checking committed offsets without a probe, which neither option does.

`WaitUntilIdle` is not Ogooreck code. It is code the app would need: a counter of background handlers it started, and a wait for zero. Ogooreck can't provide it, because only the app knows about its own background work. Recommendation: hand-off loops are out of scope for the Kafka plug and documented as such; the core `CatchUp(...)` hook remains for apps that have such a signal.

## 78. (spec r13) Kafka catch-up scope

**Answer:** "Then document and support 1 and allow some extensions, although I don't think that someone should write such code tbt"

Decision: the Kafka plug supports and documents option 1 (decorated `IConsumer` from DI, Confluent's sequential loop). Its catch-up is an extension point, so a user can plug in another strategy. Hand-off loops are not supported by the plug; the docs don't show code like `OrderWorker.WaitUntilIdle`, they only say such apps need their own catch-up through the extension point.

**Open:** whether option 2 (probe plus committed offsets) still ships as a built-in alternative, or is left for users to write through the extension point.

## 79. (spec r13) Option 2 for Kafka

Recommendation shown: drop option 2 (slow, the probe reaches the app's handler) and leave it to the extension point.

**Answer:** "ok, let's start"

Decision: option 2 is not built in. Spikes start: RabbitMQ probe plus in-flight counter (concurrency 4, nack-requeue), Kafka option 1 with Confluent's default loop and settings.

## 80. (spec r13) Spike results: RabbitMQ probe plus in-flight tracking, Kafka option 1

Setup: RabbitMQ.Client 7.2.2 against `rabbitmq:4.4.0-management`, Confluent.Kafka 2.16.0 against `confluentinc/cp-kafka:8.4.1`, both through Testcontainers. 20 messages per round, handlers with a random 0–150 ms delay, 10 rounds per case, run twice with the same results.

RabbitMQ, a decorated `IConnection` → `IChannel` → `IAsyncBasicConsumer`, one consumer on a durable queue, manual ack, unlimited prefetch:

- Plain probe, dispatch concurrency 1: 10 of 10.
- Plain probe, concurrency 4: 0 of 10 (17–18 of 20 handled when it reported done).
- Probe plus tracking, concurrency 4: 10 of 10, catch-up 0.35–0.5 s.
- Plain probe, concurrency 1, first try nacked with requeue: 0 of 10.
- Probe plus tracking, concurrency 4, first try nacked with requeue: 10 of 10, catch-up 0.7–0.9 s.

Tracking: per channel, the decorated consumer marks each delivery tag done when its handler returns; the probe counts only when every lower tag on its channel is done, no handler is running, and no `BasicNack`/`BasicReject` with requeue is waiting for its redelivery. RabbitMQ 4.4 refuses transient non-exclusive queues, so the sample declares durable queues.

Kafka option 1, a decorated `IConsumer<string, string>`, Confluent's default consumer settings (auto commit, auto offset store, `AutoOffsetReset = Latest`), a sequential loop, 3 partitions:

- Sequential loop: 10 of 10, catch-up 0.6–1.1 s (mostly the handlers' own time).
- Retries inside the loop (first try fails): 10 of 10.
- Catch-up right after the consumer starts, before the group joined: failed at the deadline. With `Latest`, messages produced before the assignment are skipped by the app itself, and the consumer's positions stay unset.
- Fix: before the first step, wait until the decorated consumers are assigned and take the end offsets then. A partition whose position is unset counts as caught up when its end offset hasn't moved since. With 9 older messages on the topic and messages produced right after the assignment: 10 of 10.

Caught up means: the consumer is inside `Consume`, it has every partition of its subscribed topics assigned, and each position has reached the end offset read at the start of the round.

Not covered by the spikes: several consumers on one RabbitMQ queue; several decorated Kafka consumers in one group; consumers the app builds outside DI.

## 81. (spec r13) Spec and plan updated from answers 75–80

Spec r13 and plan r11 now describe: Kafka catch-up through the decorated `IConsumer`, with the wait for the assignment before the first step and a `catchUp:` option; RabbitMQ catch-up through the probe plus delivery tracking, with a `catchUp:` option; `KafkaPlug.Consuming` without catch-up. This supersedes items 6 (Kafka groups listed) and 8 (conditions documented, not checked) of answer 73.

**Open:** a consumer the app builds outside DI (`new ConsumerBuilder(...).Build()`, `new ConnectionFactory().CreateConnectionAsync()` in its own class) is invisible to the plug, so its catch-up would end early, against the rule of answer 76. Detection is not designed or spiked.

## 82. (spec r13) Spike: detecting consumers the app builds outside DI

Oskar: "Yes, spike that"

Setup as in answer 80. Kafka check: list and describe the broker's consumer groups, and report any member assigned to a partition of the app's topics whose member id is not a decorated consumer's or the plug's own. RabbitMQ checks: passive declare of each queue the decorated consumers consume, comparing its consumer count; and the management HTTP API, from the exchange the app published to, through its bindings to each queue's consumer count.

Kafka:
- Decorated consumer, the plug's own test-side consumer, and a consumer on another topic: nothing reported (no false alarm), 52 ms.
- Hidden consumer in its own group, once assigned: reported with group and client id, 14 ms.
- Hidden consumer in the decorated consumer's group: reported after the rebalance, 2.9 s after it started.
- Hidden consumer right after it starts, before its assignment: not reported. The broker shows no assignment while a group joins or rebalances.
- Consumer using `Assign()` without group membership: not reported. The broker doesn't know it.

RabbitMQ:
- Passive declare, only the decorated consumer: nothing reported.
- Passive declare, hidden consumer on the decorated consumer's queue: reported (2 consumers, 1 decorated).
- Passive declare, hidden consumer on another queue bound to the app's exchange: not reported; the plug doesn't know that queue.
- Management API: reported both queues, but its consumer counts settled only after about 4 s (the broker collects statistics every 5 s by default). It needs the management plugin, its port and credentials.

**Open:** how to treat the gaps: consumers that are joining or rebalancing (seconds), Kafka consumers with `Assign()`, and RabbitMQ consumers on other queues without the management API.

## 83. (spec r13) Spike: wait for stable consumer groups before the first step

Oskar: "spike" (on the gap 1 proposal of answer 82).

Check before the first step: wait until the decorated consumer has an assignment, then until no consumer group on the broker is in `PreparingRebalance` or `CompletingRebalance`, then report members assigned to the app's topics that are not decorated. 5 runs per case, against `confluentinc/cp-kafka:8.4.1`:

- No hidden consumer: correct 5 of 5 (nothing reported), wait 79–705 ms; one earlier run waited 2.1 s while groups of earlier runs settled.
- Hidden consumer in its own group, started with the host: reported 5 of 5, wait 86–93 ms.
- Hidden consumer in the decorated consumer's group, started with the host: reported 5 of 5, wait 92–3099 ms (the rebalance).
- Hidden consumer started 500 ms or 5 s after the host: not reported before the first step, 0 of 5. Answer 82 showed such a consumer is reported once it is assigned, so a check in every catch-up round finds it from then on; steps before that can still pass early.
- Waiting for the full assignment of the decorated consumers times out when a hidden member of the same group holds some partitions; the plug must wait for any assignment, then check.

**Open:** whether consumers started later than the host are accepted as a documented gap, with a check in every round.

## 84. (spec r13) What the three remaining gaps mean

Oskar: "I don't understand what do you mean by those gaps?"

Explained with examples: each gap is an app consumer that Ogooreck can't see, so it can't wait for it and can't warn about it. (1) A Kafka consumer the app starts some time after startup, for example on first use; until the broker gives it partitions, it is invisible. (2) A Kafka consumer that picks its partitions itself with `Assign()` instead of joining a group; the broker never lists it. (3) A RabbitMQ consumer the app creates on its own connection, on a queue the decorated code never touches; plain AMQP can't list which queues an exchange feeds. In all three, a test can pass while that consumer is still working.

## 85. (spec r13) Conclusion on consumers the plug can't see

Oskar: "I still don't get it, I'm not sure how would you even document it And how likely is this issue etc. Testing tool should be predictable and consistent and follow the least surprise. I think that we're going in circles instead of coming to conlculsion"

Conclusion proposed: one fixed contract instead of detection. A plug waits for exactly the clients it decorates, the ones the app resolves from DI, and for nothing else. Detection (answers 82, 83) is dropped: it catches a hidden consumer only depending on timing, which is the opposite of predictable. Building a Kafka consumer with `new ConsumerBuilder(...)` inside a `BackgroundService` is common, so the Kafka plug's docs start with registering the consumer in DI; an app that can't do that passes `catchUp:`. RabbitMQ apps usually register `IConnection` in DI, and consumers on channels from it are covered.

**Open:** yes or no on this conclusion.

## 86. (spec r13) Detection, stated precisely, with a way out

**Answer:** "Dude, then just be precise on what you detect and give some way out for user"

Decision: the plugs keep the detection of answers 82–83, documented as exactly what it checks, and each failure names the way out.

- Kafka checks, before the first step (after the decorated consumers have an assignment and no group on the broker is rebalancing) and at the start of every catch-up round: every member of every consumer group that holds a partition of a topic the decorated clients produce to or consume. A member that is not a decorated consumer and not the plug's own fails the step, naming the group and the client id.
- RabbitMQ checks at the start of every catch-up round: each queue the decorated consumers consume, and each queue the app publishes to through the default exchange, by passive declare. A queue with more consumers than the decorated ones fails the step, naming the queue and both counts. With the management API opted in, it also checks the queues bound to the exchanges the app publishes to.
- Not checked, and documented as such: Kafka consumers that use `Assign()` without a group, and Kafka members that join after a check until the next one; RabbitMQ queues fed by an exchange, without the management API.
- Ways out, named in every failure: resolve the consumer from DI so the plug decorates it; `Ignoring(...)` for consumers that are known and not the app's concern (another service on a shared broker, the user's own test-side consumer); `catchUp:` to wait in the user's own way.

## 87. (spec r13) Ways out that don't touch the app

**Answer:** "It'd be best if it's somehow not intrusive and best configurable through overrides in tests"

Decision: every way out is configured in the test project, never in the app. The failure messages name only test-side options. Plug options are methods that return a new plug sharing the same recorder, so a shared setup class defines the plug once and a test class overrides it in its own `Use(...)`, for example `spec.Use(Messaging.Kafka.Ignoring(groups: ["billing"]))`. The options are `Ignoring(...)`, `CatchUp(...)` (the user's own wait, replacing the plug's catch-up and check) and, for RabbitMQ, `Management(...)`. Resolving consumers from DI stays the case the plug covers by itself, mentioned in the docs, not demanded in failures.

## 88. (spec r13) Go-ahead for the spec review

Oskar: "I don't understand the options you outlined, but well, do it, I'll see the outcome"

Next: the `review-specification` skill on spec r13. The plug options (answer 87) were not understood as explained; the review and the implementation will show them in code.

## 89. (spec r13) Spec r13 review resolutions

The review `reviews/20261124T173512_spec_r13_review.md` had 21 findings. Oskar: "Seriously I need to answer all of that?"; only the one that changes how users write tests was put to him: two copies of the app in one Kafka group or on one RabbitMQ queue split the messages, so one test can read another copy's state. Oskar: "I don't know what do oyou mean, by parallel test hosts", explained with the four-step example, then **Answer:** "pl" (read as "ok", one key to the right).

Decisions, all as recommended:
- Answer 73's items 1–5, 7, 9 and 10 stand as written in the spec.
- Answer 76's "fail if the plug never saw a client" is replaced by answer 86's precise check.
- One app per test project through an assembly fixture, or own group or queue names per app through test settings; documented, and the samples use the assembly fixture.
- The Kafka plug decorates the registrations that exist; RabbitMQ `Ignoring` skips the check only; a plug made from another through an option replaces it in `Use(...)`; `PlugRegistration.Deadline`; `UnknownMessage` fails only the When step's messages; `MessageReader.Read(header, body)` with UTF-8 headers; `CallRecorder` handles `ValueTask`; the plug option is `CatchingUpWith(...)`; `WolverinePlug` gets `MessageTypes(...)` and its hook is spiked first in plan part 7a; proof-of-publication checks stay as test code; `RabbitMQPlug.Consuming` is dropped; the plug packages follow the same documentation and coverage rule; Node.js is a later follow-up.

## 90. (plan part 7a) Spike: recording Wolverine messages from the tracked session

Spike (step 7, part 7a), run on the Wolverine sample with its 63 tests: `ExecuteAndWaitAsync` returns a session, and `session.Sent.Envelopes()` has one envelope per message published while the step runs. Each carries what `read` needs: `ParentId` is the `traceparent`, `Id` is the message id, `CorrelationId` is the correlation id and `ConversationId` is the id of the message that caused it. `WolverinePlug` uses this hook: inside its wrapping catch-up, after the tracked session returns, it adds the sent envelopes to `Published`. With it, the existing sample tests pass with the same assertions: `ExpectPublished` exact, `ExpectNothingPublished`, `ExpectCausation`, `Publish` steps, a late message of a Given step, `Eventually`, and the deadline cases.

`MessageTypes(...)` is needed because the sent list holds every message the app publishes, not only the sample's events: the app's timer publishes the `ExpireCart` command, and a plain `new WolverinePlug()` records it next to `CartExpired`, so an exact check of `CartExpired` alone fails. The plug records every sent message by default (a default that left out Wolverine's assembly had no test, so it was dropped), and `MessageTypes(params Type[])` or `MessageTypes(Func<Type, bool>)` keeps only the types you choose, returning a new plug that shares `Published`. The sample setup uses `MessageTypes(type => type.Namespace == typeof(OrderPlaced).Namespace)`. I did not see a Wolverine-own message in the sent list of any sample test.

Criterion 36.1 says `.Use(new WolverinePlug()).Use(clock)`. A plain plug records `ExpireCart` as well (above), so the test uses the sample's filtered plug, `Messaging.Wolverine`, with the clock plug. **Open:** change the wording of 36.1 to a filtered plug, or give `WolverinePlug` a default that keeps commands out (no way found to tell a command from an event in the sent list).

## 91. (spec r13) What does `WolverinePlug` record by default?

Question from answer 90: change 36.1 to a filtered plug, or give the plug a default that keeps commands out.

**Answer:** "my guess is all, right?"

Decision: `WolverinePlug` records every message the app publishes by default, as Java's `bus.use(recorded::add)` does. `MessageTypes(...)` narrows it. Criterion 36.1 uses the sample's filtered plug.

## 92. (plan part 7c) Open points from the RabbitMQ plug

Found while building part 7c (review `reviews/20261124T210000_step7c_review.md`). **Open**, for Oskar:
- A nack with `multiple` set counts as one requeue, even when it requeues several deliveries.
- `Ignoring` on a queue with an outside consumer can lose the probe to that consumer, which then also acks it. The test avoids this by giving the outside consumer `x-priority -10`.
- The count of decorated consumers never goes down when a channel closes.
- A `Management` failure surfaces as a bare `HttpRequestException`, without a message that names the plug.
- The plan's "short statistics interval" sentence can be dropped: 40.8 doesn't need it.
- If the nack call throws after the broker already requeued, the pending count can go to -1. A clamp would add a branch that no test can reach.

Facts: the default-exchange check sees only the queues the app publishes to through `""`. The sample needs durable queues (answer 80).

## 93. (plan part 7c) The RabbitMQ probe, an app per test, and requeue

Oskar on 7c's report: "Each test starts its own app ... we discussed that creating app is costly, so why are we doing that. This mean that this will be also a recommended setup for users, which seems like a no go." and "wtf our tests has to do with requeue", "why do we need to count consumers", "Ogooreck shouldn't be publishing anything to queue". The probe had been agreed as "catch-up copies Java", but I never said plainly that it means Ogooreck publishes a marker message into each of the app's queues. Requeue tracking had been added inside a longer proposal and never asked as its own question.

Explained with the example "Given place order, When cancel order": without a catch-up, nothing waits for the `shipping` consumer between the steps, so the result depends on timing; `ExpectNothingPublished` can't know nothing more will come. `ExpectPublished` alone works without a catch-up (it waits for the expected message). Oskar on random results: "The app is correct, but the test fails at random, depending on timing. - not acceptable". Alba was checked: it has no catch-up for plain RabbitMQ (its sample polls the queue until the message shows up) and uses Wolverine's tracked session with Wolverine. Telemetry catch-up had been spiked and dropped (answers 60–66).

**Answer:** "ok, then finish fixes and the work finally..."

Decision: the RabbitMQ plug keeps the probe with handler and requeue tracking and the consumer count, as built. The sample tests run on one shared app per test project, in parallel; only tests about different app wiring start their own app. Per-test behavior is scripted per order through the shared app. Open: `ExpectPublished` without the recorder argument, as in Java (`ExpectPublished(new ShipOrder(id))`), recommended, not answered.

## 94. (plan part 7b) Findings from the Kafka plug

Facts found while building part 7b: admin calls made on a consumer's handle complete only when that consumer polls, so a blocked handler also blocks the end-offset query; the catch-up therefore stops at 0.9 × the deadline and builds the failure from cached watermarks. A consumer with `auto.offset.reset=latest` loses a message produced before its fetch position resolves, so the wait before the first step also waits until the high watermark is known.

**Open**, for Oskar: should "rebalancing" mean any group state other than `Stable`, or only the two rebalancing states; should `KafkaPlug.Consuming` create missing topics (it does now, because the Aspire consumer otherwise never got an assignment); should `Ignoring(...)` add to earlier ignored groups or replace them; should `KafkaPlug<,>`, `KafkaConsumingPlug` and `KafkaMessage<,>` be public.

## 95. (plan part 7b) What the plugs check, and what Ogooreck must not do

Oskar on the Kafka open points: "What rebalancing has to do with Ogooreck?", "Ogooreck shouldn't setup and resources.", "I don't even know what Ignoring does.", "What three helpers?!", then on the check of groups the plug didn't wrap: "I don't understand what group we don't use has to do with checks".

Explained: before the first step the plug must wait until the app's own consumers have their partitions, or a consumer with Confluent's default `latest` skips the first step's message; the agent made it wait for every group on the broker. The check of groups the plug didn't wrap failed every test, also tests that don't depend on that group.

**Answers:** "1,2,4. ok" and, on the narrowed check, "yes".

Decisions:
- Before the first step, the Kafka plug waits only for the groups of the consumers it wrapped, not for every group on the broker.
- Ogooreck creates no resources: `KafkaPlug.Consuming` doesn't create topics; the app or its AppHost does.
- The Kafka plug checks only the groups of the consumers it wrapped: another member in such a group splits the partitions, so the wait would miss its messages, and the step fails naming the group and the member. Other groups are never looked at. `Ignoring(groups:)` is removed.
- The RabbitMQ plug checks only the queues its wrapped consumers read: another consumer on such a queue can take the probe. Queues read only by others are never looked at. `Ignoring(queues:)` and `Management(...)` are removed.
- The docs say the plugs wait only for consumers the app gets from DI.
- `KafkaPlug<TKey, TValue>`, `KafkaConsumingPlug` and `KafkaMessage<TKey, TValue>` stay public (the types users hold) and are named in the spec. A second call of an option adds to the first.

## 96. (after step 7) A variant of the test setup shouldn't start a new app

Found after step 7: one specification owns one app, so a test class that changes only test setup (a plug option such as `Headers` or `CatchingUpWith`, the deadline, `CatchUp`, `Recording`, `Publishing`, `Telemetry`, `Json`) builds a second app. About 70 places in the samples did that. Oskar: "setup should create app at the end, right? So it should be more of a builder pattern", then "shouldn't this be .with as part of the setup?", then "We should be explicit and clear when test create new app, it shouldn't happen behind the scenes", and "have a balance of accessability vs explicit. So dangerous operation (like slowing down tests) could be clear". Asked whether there were unknowns: "Can't you just do it? Are there any unknowns? That shouldn't tak elong".

Decision:
- The entry point creates the one app, as now: `ApiSpecification.For<Program>()` or a subclass.
- `spec.With(configure)` returns a specification that shares the running app and changes only test setup: deadline, recordings, catch-ups, publishing, telemetry, JSON and plug options. Disposing it leaves the app running.
- `Host(...)` inside `With`, or a plug that wraps services the app doesn't have, fails at once, naming `WithNewApp(...)`.
- `spec.WithNewApp(configure)` is the explicit way to start a separate app: the same host customization and plugs, then the changes.
- The Kafka and RabbitMQ plugs keep their per-app state in the app's services, keyed by the plug family, so a plug made through an option finds the app's consumers.
- Out of process, a second specification costs only an HTTP client, so `With` is in-process only. Building a host without `Program` (a `UseTestServer()` host) is possible in .NET and stays a separate follow-up.

## 97. (step 8) Typed instead of runtime errors

Oskar on the first version, where `Host(...)` inside `With` threw at runtime: "Api shouldn't allow the unwanted cases, we should have a proper typing and avoid runtime exception, if we cannot always achieve it then we should clearly refernece what should be done alternative."

Decision: the host customization is given at construction (`For<Program>(host)`, the base constructor `(host, configure)`, `WithNewApp(host, configure)`), and `Host(...)` is no longer a method, so a variant can't change the host. A plug's effect on services can't be expressed in types, so that case stays a runtime error whose message names both alternatives: add the plug to the app's setup, or use `WithNewApp(...)`.

## 98. (after step 8) `ExpectPublished` without naming a recorder

Open since step 7: Java writes `expectPublished(new ShipOrder(orderId))`, and Ogooreck took the recorder as the first argument because it was designed before plugs. Oskar, asked why it was still open: "Why is it open?", then "do it".

Decision, built: recorders have a role, as Java's two lists: `Recording(...)` for published messages and `Appending(...)` for appended events, on both specifications and on `PlugRegistration`; the Marten plug uses `Appending`. `ExpectPublished(messages...)`, `ExpectNothingPublished()`, `ExpectAppended(...)`, `ExpectNothingAppended()`, `Published(...)`, `Sent(...)` and `Appended(...)` check the one recorder of that role, failing at once with none or more than one. The named form has its own name, `ExpectPublishedIn(recorded, ...)` and the other `...In` methods. Two tries were dropped: overloads with the recorder first (F# picked the non-generic overload and treated the recorder as an expected message, silently) and a `Recorder` argument made with `In(recorded)` (F# couldn't choose between two `params` overloads). Oskar's rule from step 8 decided it: no case the API allows by mistake.

## 99. (after step 8) An app without a `Program` class

Oskar in step 8: "we shouldn't require to have program or so (I'm not sure what .NET now allows)"; left as a follow-up by me, then "do it".

Decision, built: `ApiSpecification.For(host)` for a started host built in the test with `UseTestServer()`. It uses the host's test server, the app's JSON options and in-process telemetry, plugs can read `AppServices`, and the caller owns the host. A host without a test server throws, naming `UseTestServer()` and `For(async () => address)`.

## 100. (step 8 fix round) No time-based waits in tests

Context: a catch-up deadline test failed at 1.9997 s of a 2 s deadline, and I lowered its lower bound to 1.9 s. Oskar: "Man, we shouldn't be adding any time based waits". After I listed the existing ones (elapsed-time asserts, `CancelAfter`, fixed `Task.Delay`/`Thread.Sleep` before a message arrives, polling loops): "Dude, that's why we have catch up not to do any time based or sleeps, wtf", and "especially such long like >=100ms".

Decision: tests don't sleep, don't cancel on a timer and don't assert elapsed time. Something that must happen later is released by a signal the test controls (a `TaskCompletionSource`, the catch-up or the read function), and cancellation comes from inside the code under test once it runs. A test of `Deadline(...)` failing keeps a short deadline, because the deadline is the behavior under test, but asserts the failure message, not the elapsed time.

## 101. (step 8 fix round) Removing the remaining waits, test project names, feature folders

Oskar, on my list of remaining time-based waits: "1. Man, wolverine gives catchup, so I don't understand. Yes, add TieProvider if that helps. Use it also in other places if that helps. 2. Why after 500ms? 3. What do you mean by I'd signal from the fakes? 4. can't we change it? or make it shorter?"

Oskar: "Also if we have tests, then Ogooreck.Sample.Wolverine.Tests shouldn't have sample prefix, just Ogooreck.Wolverine.Tests so it's eaisly discoverable. Same for other tests"

Oskar: "Plus the codebase should be structured in feature folders, no hundreds of files in a single folder."

Open: the `TimeProvider` design is to be spiked; the folder layout and the test project names are proposed to Oskar before moving files.

Oskar, on my answers and proposal (TimeProvider spike, sample holds, signals from the fakes, OTLP changes, dropping `.Sample` from test projects, feature folders): "1,2,3, 4 - ok", and "also nest those folders, I don't want to end up with flat 100 folders instead. Plus not create folders just for the sake of it, but for logical split. The main public api can remain unnested".

Decision: folders are nested by logical area, only where a split helps the reader; the main public API (`ApiSpecification` and what users call first) stays at the project root. The namespace stays `Ogooreck.API`, so users' code doesn't change (no answer to that question; keeping it is the non-breaking default).

## 102. (step 8 fix round) Tracing of our own test runs

Oskar, while I was moving the deadline tests to test clocks: "Could you add otel to tests so you could easier diagnose what took time?", then "(i mean to tests internally)".

Decision: our test projects export traces of their own runs (each test, its steps, catch-ups and waits) over OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set, so a slow test shows where its time went. Design in progress.

Oskar: "ok, so will you use those added telemetry info? Also maybe it'd be useful to define some metrics?", then "Are you using those telemetry from Ogooreck?" and "Remember to use tracing", "I mean, the ogooreck tracing we just added".

Decision, built: Ogooreck itself traces each run (`ActivitySource` "Ogooreck": the run, each start, step, catch-up and expectation) and records their durations as histograms (`Meter` "Ogooreck": `ogooreck.run.duration`, `.start.duration`, `.step.duration`, `.catch_up.duration`, `.expectation.duration`). The test projects export both when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. A plug's start and the catch-up run inside their spans, so a plug's own spans nest under them; the Kafka plug traces its waits (`ActivitySource` "Ogooreck.Kafka"), and the wait for assignment and fetching gets an event each time the problem it waits on changes.

## 103. (step 8 fix round) Slow test suites

Oskar, on the suite durations: "3s sounds like a lot for 20 tests" (RabbitMQ), "35s - a lot" (Kafka), "even 3 and 7 s for wolverine shows potential to cleanup".

Findings from the traces: a plug registered on a shared app read the clock and deadline of that app, not of the variant running the step, so `app.With(spec => spec.Clock(...).Deadline(...))` didn't reach the Kafka, RabbitMQ and Marten catch-ups (fixed, with a test). RabbitMQ went from 3 s to 1 s with holds released by the test instead of delays. Marten went from 11 s to 5 s: the plug turns on `UseListenNotifyForEventAppends`, so the daemon sees an append at once instead of at its 1 s poll, and calls `WaitForNonStaleProjectionDataAsync` repeatedly with a 50 ms timeout, because one call checks only every 250 ms.

Oskar, while I was reading librdkafka debug logs: "I think that you're going to far down the rabbit hole, we need to check if ogooreck could improve something, not fix kafka, what's the status?"; on my proposal (revert the Kafka experiments, add spans for the plug's waits, use a test clock in `BlockedHandlerTests`, keep the fetching check as an open point, move on): "Ok, for suggestion, then do".

Open: the Kafka plug's start waits until each assigned partition has a high watermark (`GetWatermarkOffsets`), which librdkafka fills about 3 s after assignment, while its fetches already return 0.1 to 1.1 s after assignment. A signal that turns true at the first fetch would save about 2 s of each fresh app's start; no such signal is confirmed yet, so it needs a spike.

Open: `BlockedHandlerTests` takes about 20 s: a warm-up app on the topic, then the blocked app, then a real 2 s deadline. A passing test clock would race the plug's own "the broker did not answer" timer, which then fires before the end offsets arrive and fails with another message. The reason for the warm-up app isn't recorded.

## 104. (step 8 fix round) Parallel Wolverine tests

Oskar, on the Wolverine tests running with `ParallelMode.None` (from section 53): "Not acceptable to run it serially.", then "about wolverine, I don't think that wolverine doesn't allow running tests in parallel, do a  proper research".

Findings, from the WolverineFx 6.63.0 source and its `main` (7.0 rc): Wolverine allows parallel tests; its docs suggest a host per test "to maybe get better test parallelization", or one host shared through an xUnit collection fixture. A tracked session is one per host: `WolverineRuntime.ActiveSession` is a single internal property, `ExecuteAndTrackAsync` sets it to itself and `cleanUp()` sets it back to `null`, with no check for another session. So two scenarios at once on one host replace each other's session. `IMessageTracker` is public, but the runtime returns itself (`MessageTracking => this`), so it can't be replaced.

Finding, from a temporary probe that logged the host's active session in a Release run: the same mechanism hangs the serial suite. `Eventually_CancelledThroughTheToken_EndsWithOperationCanceledException` cancels `Then` while its step runs; Ogooreck ends the scenario without waiting for the step (on purpose: a step that ignores its token must not block `Then`), so the next test, `Eventually_ResendsUntilTheReadModelShowsPlaced`, starts its session while the cancelled step's session still runs. 11 ms later the cancelled step fails, its `cleanUp()` sets `ActiveSession` to `null`, and the next test's session never completes, until the 30 s deadline. Release runs fast enough for that overlap; Debug doesn't.

My proposal to catch up on the step's trace (Wolverine's spans) instead of the tracked session, withdrawn after Oskar's "I think there's areason why Wolverine does it this way, or not?": the tracked session hooks the runtime's own message lifecycle (retries, requeues, dead letters, messages outside the step's trace, other hosts), and spans can be turned off per endpoint.

Oskar: "I want to use the idiomatic way in Wolverine". On my proposal of a host per test class: "Give each test class its own app. - why? This is anti-pattern of what I know, what does app mean even? IHost or what? How alba does it?"

Finding: Alba shares one `IAlbaHost` through a fixture, and for parallel tests (Alba 9.0.0) offers `AlbaHostPool`: up to `MaxHosts` hosts (the processor count by default), started as tests need them, each lent to one test at a time.

Oskar: "No, I won't use Alba." On building that pool into Ogooreck: "Let's keep it out for now, then", "As the follow up".

Decision: the Wolverine tests keep running serially for now; a pool of hosts in Ogooreck, so scenarios can run in parallel, is a follow-up. The hang is fixed in the plug: it opens one tracked session per host at a time, so a step that outlives its scenario ends before the next scenario's step starts (test `StepStillRunningAfterItsThenWasCancelled_EndsBeforeTheNextScenarioOnTheAppRunsItsStep`). The Wolverine suite takes 3 s in Release instead of 36 s.

## 105. (step 8 fix round) Slow Kafka app start and catch-up

Oskar: "Why is app taking so long to start? This is super weird", "and why do they create 15 times app?", "Dude, don't debug kafka, bu our setup, seriously?!", "is the docker compose kafka and other containers fine tuned for testing like test container?", "Dude, so are you sure that only single container is started? Shouldn't we have it done like https://github.com/oskardudycz/EventSourcing.NetCore/tree/main/Core.Testing/Fixtures?", "Can't we have different configs or scenarios in a single app?", "then how to diagnose it?", "And also update settings do the research on how people set it up. I'm surprised that Kafka test container doesn't have it".

Findings: the Ogooreck spans show the shared app's start takes 7.2 s (5.2 s waiting for assignment and fetching, 0.5 s reading end offsets, 1.5 s checking the groups), and each catch-up about 3.5 s, because each round repeats the group check (1 to 2 s) and the end-offset read (0.5 s). The same admin calls on a fresh consumer take 1 to 30 ms, so the time is in our setup. Ogooreck has no Docker Compose; each suite starts its own Testcontainers container, one per test assembly. Testcontainers' `KafkaBuilder` (4.16.0) already tunes the broker for tests (offsets topic with one partition and replication 1, transaction log replication 1, no forced flush, `KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS=0`); the clients use production defaults. Under the consumer group protocol of KIP-848 (`group.protocol=consumer`, production-ready since Confluent.Kafka 2.12, needs Confluent Platform 8.0+), the broker computes assignments, and heartbeat and session timeouts are broker settings.

Proposal: (1) a span per broker request in the Kafka plug, to find where the 0.5 to 2 s go; (2) `group.protocol=consumer` on the sample's and the consuming plug's consumers, with a short heartbeat on the test broker, measured before and after; (3) then whether one app can cover several app shapes. If `StuckRebalanceTests` must change under the new protocol, ask first. Oskar: "Ok".

Open: reusing a running Docker Compose container, as `Core.Testing/Fixtures` in EventSourcing.NetCore does, isn't answered yet.

Finding, from a span per broker request (built, with test `Decorating_TracesEachBrokerRequestInsideTheWaitThatMadeIt`): our code adds nothing between requests; each ListOffsets, ListConsumerGroups and DescribeConsumerGroups takes 500 or 1000 ms. The plug's admin client is built on the app consumer's handle (`DependentAdminClientBuilder`), so its requests share the consumer's broker connection and wait behind its open fetch, which the broker holds for `fetch.wait.max.ms` (500 ms by default). With `FetchWaitMaxMs = 10` on the sample's consumers (tried, then reverted): requests take 6 to 47 ms, a catch-up 40 to 100 ms instead of about 3.5 s, the shared app's start 2.3 s instead of 7.2 s (the rest is group assignment), and the whole Kafka suite 5 s instead of 43 s.

Open: fix it in the sample's settings (every Ogooreck user's app then needs the same setting), or in the plug, with an admin client that doesn't share the app consumer's connection.

Oskar, on fixing it in the plug or in the settings: "1" (in the plug).

Spike: with the consumer's fetch open, ListOffsets and ListConsumerGroups take about 500 ms each through the dependent admin client, and 1 to 2 ms through an admin client of the plug's own, built from the brokers in the consumer's metadata (`GetMetadata` through the dependent client, about 430 ms, once). So the decorating plug needs no new API to find the broker. Open: the plug's own client doesn't carry the app's security settings (SASL, SSL).

Oskar, on falling back to the dependent admin client when the plug's own can't reach the broker: "makes sense".

Spike, with a broker whose client listener needs SASL PLAIN (Testcontainers' `KafkaBuilder` with `KAFKA_LISTENER_SECURITY_PROTOCOL_MAP` mapping `PLAINTEXT` to `SASL_PLAINTEXT`; clients need the address without the `plaintext://` scheme): an authenticated consumer gets its assignment; the plug's own unauthenticated admin client fails its request only at the 10 s request timeout, but its error handler reports `Local_AllBrokersDown` after 4 ms, so the plug can fall back at once.

Oskar, on the second broker with SASL and `Kafka:Client` settings in the sample: "ok".

Finding: with the plug's own admin client and the fallback built, the SASL test crashes the test process (exit 134, `free(): double free detected in tcache 2`) while the plug's own unauthenticated client is refused by the SASL listener. Without the plug's own client there's no crash, and the error handler isn't involved, so it's in librdkafka (Confluent.Kafka 2.16.0). Not debugged further, per "don't debug kafka". The test for requests not waiting behind the app consumer's open fetch passes with the plug's own client.

Open: how the plug should avoid an unauthenticated client on a secured broker.

Oskar, on the options (settings; the plug's own client with security settings passed in; the plug's own client as an opt-in): "1. but don't create any issues".

Decision: the plug keeps the admin client on the app consumer's handle, which works on any broker. Test apps set a low `fetch.wait.max.ms`, and the README tells users to. The per-request spans stay. The SASL broker, its test and the test for requests not waiting behind an open fetch are removed. No issue is reported upstream.

Oskar, on a config option so the plug could set `fetch.wait.max.ms` for the user (asked: a plug option naming the app's setting, a low fetch wait on Ogooreck's own consumer, or a general host setting): "and also give some config option so we could set it for user", then "ah, ok, understood, then drop this idea but show in sample tests".

Decision: no plug option. The sample Kafka app reads extra client settings from `Kafka:Client`, and its tests set `Kafka:Client:fetch.wait.max.ms` low for every test app, as the example users follow.

## 106. (step 8 fix round) An agent-first feedback loop: test telemetry over MCP

Oskar: "Reusing a running Docker Compose container, - yup, we could also add this to Ogooreck.TestContainers package, thought?", "I'm not sure what Step 2 is" (step 2 of my Kafka proposal: Kafka's consumer group protocol of KIP-848, `group.protocol=consumer`, to shorten the group assignment that still takes about 2.3 s of each Kafka app's start).

Oskar: "is it possible to add some local ingester that wouldn't need tempo and expose it as MCP for you or users using Ogooreck? Could you do research if other testing frameworks (not only .NET) gives such option? maybe we could add some custom aspire dashboard or plug it there? So in general, making ogooreck and agent-first testing tool giving quick feedback for agentic tools like you, both on tests and also on tested and instrumented app. Both performance and debugging instead of custom logging. We shouldn't require someone to run docker, we should ofc allow easy export. Also tracing should add singificant delay and be configurable. Also think that we'll also build Java and maybe node.js version".

Research: Aspire's MCP server runs against a standalone dashboard (`aspire agent mcp --dashboard-url`) with `list_traces`, `list_structured_logs` and `list_trace_structured_logs`, plus `aspire otel traces --format Json`; the standalone dashboard runs in a container. Local OTLP-to-MCP servers exist (otel-mcp: an OTLP HTTP collector on localhost; otlp-mcp: one Go binary with an OTLP receiver and a ring buffer; Traceloop's opentelemetry-mcp only queries backends), none of them aware of tests. Vitest has built-in OpenTelemetry with a span per test, pytest-opentelemetry exports with `--export-traces`, and Playwright has community reporters; none serves per-test telemetry to an agent.

My first sketch wrote each run's telemetry as OTLP JSON files. Oskar: "Those files could be annoying, don't you think?" My second sketch had the MCP server host an OTLP receiver that tests export to when it listens. Oskar: "it it how playwright mcp does it?"

Research: Playwright MCP (`@playwright/mcp`) drives a browser and writes traces and screenshots to files only when asked (`--save-trace`, `--output-dir`, `.playwright-mcp`). Playwright's test agents (1.56) add `npx playwright run-test-mcp-server` to `.mcp.json`; its tools `test_list`, `test_run` and `test_debug` run the tests inside the MCP server's own process, and `test_debug` pauses a failing test so the agent inspects live state (`browser_snapshot`, `browser_console_messages`, `browser_network_requests`). The healer agent runs everything, debugs each failing test, fixes it and runs again.

Proposal: as Playwright's test agents do, the MCP server owns the run: `test_list` and `test_run(filter)` start the language's test command with `OTEL_EXPORTER_OTLP_ENDPOINT` set to the server's in-memory receiver for that run, then query tools answer over that run (slowest tests and steps, a failed test's trace with the app's spans and logs, where a catch-up spent its time). No files, no listener and no overhead for people without an agent; export to Aspire or lgtm stays `OTEL_EXPORTER_OTLP_ENDPOINT`. A pause-and-inspect tool like `test_debug` is left for later.

Oskar: "and how would we collect this? would we run docker or what?" Answer: no Docker; the MCP server is one local process with an OTLP receiver (Ogooreck's `OtlpReceiver`, OTLP/HTTP protobuf); Ogooreck turns on OTLP export in the test process when the variable is set; out-of-process apps (Aspire) need the endpoint forwarded, to be spiked.

Oskar: "ok, how hard would it be? Could it be distributed with nuget?" Research: NuGet.org hosts MCP servers built on the official C# SDK (`ModelContextProtocol`, 2.4.0 of 2026-11-07), with a `.mcp/server.json`; clients run them with `dnx`, which ships with the .NET 10 SDK. Estimate: a few days for a first version without Aspire.

Oskar: "i'm fine using .net 11 if that helps". Research: .NET 11 shipped on 2026-11-10 (standard-term support); its CLI starts through a Native AOT host and keeps the MSBuild server warm, and `dotnet new mcpserver` ships in the SDK. `dnx` already works on .NET 10.

Oskar: "yes, we target 10 and 11, record all of that in qa and do the spike".

Decision: the MCP server targets .NET 10 and .NET 11; Ogooreck itself stays on .NET 10. Spike: `test_run` on one test project and one query tool, the slowest steps and where they waited, on Ogooreck's own tests.

Open: an `Ogooreck.TestContainers` package that reuses a running Docker Compose service or starts a container (one core package for the Compose lookup plus one per tool, or one package); KIP-848 for the Kafka sample; Aspire forwarding; Java and Node distribution of the MCP server.

## 107. (step 8 fix round) MCP spike findings

Spike: `src/dotnet/Ogooreck.Mcp`, outside `Ogooreck.slnx`, targeting net10.0 and net11.0, on `ModelContextProtocol` 2.4.0 over stdio. Tools: `test_run(project, filter)` starts `dotnet test` with `OTEL_EXPORTER_OTLP_ENDPOINT` set to the server's in-memory OTLP/HTTP receiver, `slowest_steps(top, depth, children)` lists the slowest Ogooreck steps with the spans they waited on and the app spans of the request the step sent (joined on `ogooreck.step.trace_id`), and `export_spans(path)` writes the spans to a JSON file. Driven over stdio JSON-RPC from a script.

Results: `Ogooreck.Kafka.Tests` 36 passed, 548 spans; both slowest steps are "start before When" at 2.3 s, of which 2.24 s is "Kafka wait for the consumers' assignment and fetching" (the group assignment that KIP-848 targets). `Ogooreck.Api.Tests` 203 passed and 4 failed, 2457 spans.

Finding: 4 tests of `Ogooreck.Api.Tests` fail whenever `OTEL_EXPORTER_OTLP_ENDPOINT` is set, even to a closed port, and pass without it: `ExpectTrace_WithNoSpansOfTheTest_FailsListingNone`, `ExpectCorrelatedTrace_FailsWhenNoSpanHasACorrelationId` and both cases of `Disposing_ASpecification_StopsItsDefaultInProcessListeners`. `Shared/TestTelemetryExport.cs` listens to every source (`AddSource("*")`), so spans exist that these tests expect not to exist. Not fixed yet.

Finding: the net11.0 build failed to run the net10.0 tests: `Process.Start("dotnet")` on Linux looks in the folder of the running executable before `PATH`, so the server started with the .NET 11 host ran the tests with the .NET 11 install, which has no 10.0 runtime. With `dnx` there is one `dotnet`, so the test projects' runtimes must be installed with the SDK that runs the server.

Oskar, on the hand-written text report: "Seriously? Don't we have tools to do such stuff or better ways?" Change: the tools return records with `UseStructuredContent = true`, so the SDK serializes them and advertises an output schema. Also found: no official NuGet package carries OTLP model types; the native way is generating them from `opentelemetry-proto` with `Grpc.Tools`, as the Aspire dashboard does. Ogooreck's `OtlpDecoder` hand-decodes the same way as the spike, so it is one decision for both. Open.

Oskar: "mcp should also generate spans on its own to be debuggable". Change: the server traces the SDK's source `Experimental.ModelContextProtocol` and its own `Ogooreck.Mcp` source (a `dotnet test` span), exports them to its own receiver, and also to `OTEL_EXPORTER_OTLP_ENDPOINT` when it is set. Verified: a `traceparent` sent in the JSON-RPC `_meta` becomes the parent of the SDK's `tools/call test_run` span, which is the parent of `dotnet test`.

Oskar, on parsing the console lines of `dotnet test`: "also we don't have a proper tool to parse .NET test result? Seriously we need to parse lines? there's no nuget or what? I thought you'll be getting that from OLTP?!"

Research: `Microsoft.Testing.Extensions.OpenTelemetry` (Microsoft, 2.5.3 of 2026-11-11; 2.5.2 of 2026-10-27) emits a span per test on the `Microsoft.Testing.Platform` source with `test.case.name`, `test.case.result.status`, `test.suite.name`, `code.stacktrace`, `error.type`, an `exception` event and an `Error` status on failures. It reads `TRACEPARENT`, so the whole run nests under the caller's trace. With `<TestingPlatformOpenTelemetryMode>HostOwned</TestingPlatformOpenTelemetryMode>` it only turns the source on and leaves the provider to the app, which `TestTelemetryExport` already is. It works only on Microsoft.Testing.Platform (MTP); Ogooreck's test projects use `xunit.v3.mtp-off`, so they run on VSTest. For VSTest projects, a TRX file (`--logger trx`) is the structured fallback.

Open: move Ogooreck's test projects to MTP so `test_run` reads test results from OTLP, and what `test_run` does for user projects still on VSTest.

## 108. (step 8 fix round) MCP spike review: code shape, test runner, span nesting

Oskar, on `Ogooreck.Mcp/TestTools.cs`: "this look super slop". Agreed problems: one class runs the process, scrapes console lines, builds span trees and writes files; the tree logic is local functions rebuilt on each call; Ogooreck's span conventions (scope names, `ogooreck.step.trace_id`, "a step is an Ogooreck span under an Ogooreck span") are magic strings copied from `Diagnostics`; `Process.Start("dotnet")` picks the wrong `dotnet`; `Clear()` races with spans still arriving. Proposed shape: a test run that gets results from per-test spans, one span tree built per run and shared with Ogooreck's internal `SpanTree`, thin tools. Not rewritten until the test runner and the nesting are settled.

Oskar: "what is this decision for me?" Explained: the test projects run on VSTest (`xunit.v3.mtp-off`, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`), which has no telemetry for test results, so `test_run` reads console lines and each test's spans start their own trace. Moving to Microsoft.Testing.Platform (`xunit.v3`, MTP in `global.json`, `Microsoft.Testing.Extensions.OpenTelemetry` in `HostOwned` mode) gives a span per test with name, status, stack trace and exception, and nests the run under the caller's trace. Cost: every test project changes (xUnit, F#, NUnit, MSTest; TUnit is on MTP already), and the 100% coverage gate in `Ogooreck.Build` uses `coverlet.msbuild` options that work only under VSTest, so it moves to an MTP coverage extension. User projects on VSTest need a `.trx` fallback.

Oskar: "Is given when then nested? nicely? I see when spans". Finding, from a real Kafka run: no. The run's root is renamed after its When step ("Ogooreck When request POST …"); the Given step, the When step, the catch-ups, the "start before …" spans (0.0 to 0.1 ms when nothing starts) and the expectations are flat siblings under it; catch-ups are not under their step; `Diagnostics.Start` clears `Activity.Current`, so a run never nests under its test; the app's span shows as `Microsoft.AspNetCore.Hosting.HttpRequestIn` because the test export does not add the ASP.NET Core instrumentation. Proposed: Given, When and Then group their steps and expectations, each catch-up sits under its step, and the run nests under the test's span once tests emit spans.

Oskar: "All makes sense but start app should be some sort of before?" Answer: yes. The app and its plugs start lazily but always at a run's first step, so a Before span opens the run, holding "start app" and "start plug …" with their children, and only when something starts in that run. Runs on an app started by an earlier test get no Before span.

Open: VSTest with `.trx` or a move to MTP; approval to change the span names and nesting in Ogooreck core, test-first, as a design change.

Oskar, on changing the span names and nesting in Ogooreck core: "Yes, with also what I said on before". On moving the test projects to MTP: "yes".

Decision: Ogooreck's run spans nest as Before (only when the app or a plug starts in that run), Given, When and Then, with each catch-up under its step. The test projects move to Microsoft.Testing.Platform with `Microsoft.Testing.Extensions.OpenTelemetry`, and the coverage gate moves with them. The MCP spike is reshaped on top of both.

Oskar: "Could this MCP also create tests with BDD as we have given when then?" Answer: yes, as Playwright's test agents do: the server does not write code itself, it gives the agent an MCP prompt (`[McpServerPrompt]`) with Ogooreck's test conventions, an `api_endpoints` tool (the app's routes and request and response shapes from the app hosted in the test process), a `try_scenario(given, when)` tool that sends real requests to the hosted app and returns the responses, recorded messages and spans, and a `scenario_to_test` tool that turns a tried scenario into Ogooreck code; `test_run` with a filter then checks it. Caution: a test written from observed behaviour locks in today's behaviour, bugs included, so the agent writes the scenario from the user story or spec and uses `try_scenario` to check it. Not spiked; planned after `test_run` reads results from MTP spans.

Oskar: "also what if user has vstest instead of the new tests?", then "We need to support both right? What's trx?" Answer: both are supported. The server asks MSBuild per project (`dotnet msbuild <project> -getProperty:IsTestingPlatformApplication`). MTP projects with `Microsoft.Testing.Extensions.OpenTelemetry` give a span per test, with Ogooreck's runs under their test; without the package, `test_run` names it. VSTest projects give a TRX file (VSTest's built-in XML test results format, `dotnet test --logger trx`, read by Visual Studio, Azure DevOps and CI viewers) with each test's name, outcome, duration, error message and stack trace; the server writes it to its own temp folder and deletes it after reading. On VSTest there is no span per test, so runs are shown by their When step, not by test name. MTP without the OpenTelemetry extension can write TRX through `Microsoft.Testing.Extensions.TrxReport`. A custom VSTest logger is not used: it runs in `vstest.console`, not in the test process, and adds a component on the runner Microsoft is moving away from.

## 109. (step 8 fix round) Test identity on Ogooreck's run spans, on any runner

Oskar: "I mean I want spans in both", then "Can't ogooreck export telemetry while running in VSTest?" Answer: it already does; the Kafka spike ran on VSTest and exported all 548 spans over OTLP. Only the test's identity was missing, because VSTest has no span per test.

Proposal: Ogooreck names the calling test itself, with the compiler's caller-info attributes, so it works on VSTest and MTP and with any test framework. Catch: `Then(params Expectation[])` cannot take optional caller-info parameters after `params`. `When(TestStep)` has no `params`, and every scenario passes through it, so the caller info goes there. Oskar: "ok, do it".

Spike result: `When(step, [CallerMemberName] test, [CallerFilePath] file, [CallerLineNumber] line)` tags the run span with `code.function.name`, `code.file.path` and `code.line.number`. A failed run also gets an OpenTelemetry `exception` event (`exception.type`, `exception.message`, `exception.stacktrace`) through `Activity.AddException`. Run through the MCP spike on VSTest, all 257 run spans of `Ogooreck.Api.Tests` carried the three tags, for example `ExpectCausation_WithTwoRoots_ChecksBoth`, `Messages/CausationTests.cs`, line 196.

Limits: a helper method that calls `When` is named instead of the test (`SharedAppTests.InstanceOf` named 16 runs), while file and line point to the helper's `When` call. Lambdas and local functions report the test that holds them. `code.function.name` is the bare method name, not the fully qualified one OpenTelemetry's semantic conventions ask for, because caller info gives no type. The exception event's stack trace ends in Ogooreck's `Then`, because it is recorded before the exception reaches the test; the `code.*` tags point to the test.

Oskar: "You need to expand on limits, and are there hard limits or can we fix them", and "Also how does it relate to MCP?" Spike (console app, net10.0, Release): from inside a synchronous `When`, `new StackFrame(1)` is always the direct caller, an async test's state machine `<Test>d__0.MoveNext` nested in the test class, so the class is recoverable. When a helper calls `When` after an `await`, the test's frame is no longer on the stack (only the helper's `MoveNext` and thread pool frames), so the stack cannot find the test. An exception caught in `Then` has only `Then`'s frames in `StackTrace`; the test's frames are appended while it propagates, after Ogooreck has let go of it. Findings: the missing class is fixable (one stack frame per `When`); a helper or a theory row needs the test framework's own async-local test context (xUnit v3 and TUnit `TestContext.Current`, NUnit `TestContext.CurrentContext`) or the MTP test span, which Ogooreck core cannot reference directly; the exception stack trace is a hard limit inside Ogooreck, covered by the runner's results (TRX or MTP spans).

## 110. (step 8 fix round) MCP server: solution, coverage, Playwright-inspired tools, F# export

Oskar, on adding `Ogooreck.Mcp` to the solution: "It should have 100% cc and be added." On the F# projects exporting no spans: "Can you fix it?" On a shared `ogooreck_` prefix for the tools: "fine". On a CLI plus a skill: "also that's also fine, but you didn't tell me anything about the features inspired and what did you added besides the basic one i told you".

Done: `Ogooreck.Mcp` and `Ogooreck.Mcp.Tests` are in `Ogooreck.slnx`, `build.sh` runs them in the `test-mcp` target, and the coverage gate includes `[Ogooreck.Mcp]*`. The tests start the server as a separate process over stdio, as an agent does; coverlet counts that process, because it loads the instrumented `Ogooreck.Mcp.dll` from the test output and exits cleanly. `Ogooreck.Mcp.Fixture` stays out of the solution, because one of its tests fails on purpose. Coverage reached 100% (353/353 lines, 162/162 branches) by covering real cases through the fixture (a skipped test, a span outside any Ogooreck run, bool, double and array attributes, an `Ok` status, a deterministic CI build with `/_/` paths) and by removing fallbacks for data no producer sends (a TRX without `TestMethod`, a run span without `code.file.path`).

Found while testing: `dotnet test` started by the server left MSBuild nodes running that inherited its output pipe, so `test_run` hung when no nodes were warm. The server now sets `MSBUILDDISABLENODEREUSE=1` for the processes it starts. The server's own spans went into the same collection as the test run's, so tool calls after a run changed its span count; they now go only to a collector named by `OTEL_EXPORTER_OTLP_ENDPOINT`.

Tools, all prefixed `ogooreck_`, with titles and read-only or destructive hints: `ogooreck_test_run`, `ogooreck_slowest_steps`, `ogooreck_find_spans` (a regular expression over span names, errors and attributes, as Playwright's `browser_find` searches a page; each match comes with its path, test and `When` line, also for spans of the tested app through `ogooreck.step.trace_id`), `ogooreck_scenario_diagram` (a Mermaid sequence diagram of a test: the steps it sent and the messages the app consumed from each destination, built from consumer spans; Wolverine emits no producer spans for local queues, so sends are not drawn), and `ogooreck_export_spans`. `ogooreck_slowest_steps` and `ogooreck_find_spans` take a `filename`, as Playwright's tools do, and write to that file instead of the response only when asked. Client roots were dropped: the MCP specification deprecated them on 2026-07-28 (SEP-2577) and the C# SDK marks them obsolete.

F# export: `Shared/TestTelemetryExport.fs`, linked into `.fsproj` test projects by `Directory.Build.props`, starts the same OTLP export as the C# module initializer, from an xUnit v3 assembly fixture. Verified: `Ogooreck.Wolverine.FSharp.Tests` runs carry `PlacingAnOrderTests.placing an order publishes OrderPlaced` with file and line.

CLI and skill: built, then removed. The first version saved each run's spans to `.ogooreck/last-run.json`. Oskar: "Dude, what does ogoorek stores spans to file? We already discussed that" (answer 106: no files). A CLI without the file has to either print everything at once, so the agent picks its questions before seeing the failure, or keep the spans in a background process. Oskar: "Why would it print it? What's the value?" and, on the background process, "why? what would be the value?" Answer: the value is asking follow-up questions about the same run without running the tests again, which for Kafka or Marten suites takes minutes and may not reproduce a flaky failure; the MCP server already keeps the last run in memory, so a CLI adds nothing for agents that use MCP.

Oskar, on the skill: "skill is unclear to what ogooreck does and what is it and why LLM should even care", "For Ogooreck you have the readme with information", and "This is not debugger in general, it's one of the Ogooreck features". Agreed: Ogooreck is a library for readable Given/When/Then tests of business logic and APIs; telemetry is one of its features, and the MCP server gives an agent access to that feature, not to any test's internals.

Oskar: "also plugs are unclear and the span names with app starts and plugs, what does it even mean?" and "plug is weird, why not pluggin or something more common in other tools?" The shape, one `Use(...)` call per tool, is answer 72; the word "plug" was my proposal. Options: "integration" (Aspire's word for its Kafka, RabbitMQ and PostgreSQL packages), "plugin", "extension". Proposed span names: the run named after the scenario instead of "Ogooreck Given/When/Then", "start app and tools" instead of "Before", "start Kafka" instead of "start plug kafka", "wait for Kafka to catch up" instead of "catch-up", and what an expectation checks instead of "expectation 1".

Oskar: "Maybe also something around BDD? So maybe adding also the scenario name or params to given when name?" and "Dude, I asked you to research playwrithg, and suggest other features. Same you could do with BDD and analysing what actually Ogooreck does and why is it special". Proposed from Playwright (`browser_snapshot`, `browser_network_requests`, `browser_console_messages`, the planner, generator and healer test agents, `test_debug`) and from what only Ogooreck records: the effects of each step (messages published, events appended, causation), a list-then-detail view of the steps a test sent, the app's logs per step, failure types with the next thing to check, steps that passed close to their deadline, the scenario diagram, living documentation, and Ogooreck agents. An "unchecked effects" proposal was withdrawn after Oskar's "wot?": `ExpectPublished` on a recorder with catch-up already fails when a step published more than expected.

Oskar: "fine but how would you do it? same for Living documentation (BDD). ? Ok, man finish to some usable state and add low hanging fruits and finish it..." The diagram was built from existing spans. Living documentation needs words Ogooreck does not export yet: expectations are bare functions, so each `Expect...` helper would have to describe itself, and theory-row values need the per-test span.

Oskar, on the first full build failing in the Kafka tests: "Are you using tracing that ogooreck gives you do analyse what's wrong or just blindly restarting?" I had restarted. Analysed through `ogooreck_test_run` and `ogooreck_find_spans "^start plug"` on `Ogooreck.Kafka.Tests`: `start plug KafkaConsumingPlug` took 3,017 ms on a fresh broker, and `KafkaPlug` starts took 2.0–2.4 s. The broker fixture shares one `Consuming` plug across test classes. The plug waits for its partition assignment within the deadline of the specification that starts it first, and keeps a failed start. When a test with a 2-second deadline (`PublishedMessagesTests`, `CausationTests`, `PlugOptionsTests`) started it first, it failed after 2 s, and the 11 tests after it failed in 1–4 ms with the same "plug KafkaConsumingPlug failed to start: The operation has timed out". Running `ExpectPublished_WithoutCatchUp_FailsAtTheDeadline*` alone reproduced it. Fixed in the test fixture: `KafkaBroker` starts the shared consumer with one `GET /carts/{id}` run under the 30-second deadline before any test. The Ogooreck tests' own build exports no spans, so a failed `build.sh` run leaves nothing to analyse. Also fixed: `DecorationTests` expected the type name from before commit 10fad19 moved it to `Ogooreck.Tests.Messages`.

Coverage gate: after the Kafka fixture warmed the broker up, branches that only cold or slow runs had reached by chance showed up as gaps, next to ones no test reached at all. Covered through the public API: the `AppendedIn(...)` wording when `ExpectCausation` finds two recorders of appended events; a plug read while another specification runs, and outside any run, keeps its own specification's deadline; a Marten catch-up on an empty event store, and on projections stale until the deadline (the cause is Marten's `TimeoutException`); a Kafka catch-up while the broker is paused (`The broker did not answer within 1.8 seconds.`); a Kafka plug waiting while nothing traces, which the broker fixture now does first, because Ogooreck's in-process listener is process-wide once any specification starts. `TestCaller` lost null checks a stack frame can't need. `Waiting` now picks its last poll with `Math.Min` instead of a conditional: a test of that last, shorter poll measured the passing clock, and its timers move it only when they fire before they are disposed, so the test was flaky and was dropped.

**Open:** a shared plug's start bounded by the deadline of the first specification that starts it, and a failed start kept for every later test; the name for "plug"; the span names; descriptions for expectations and living documentation; step effects exported from the test process; the close-to-deadline report; failure types; Ogooreck agents; logs per step; a paused debug session.

## 111. CI failures, start timeout, docker-in-docker, MCP tools for local work

Oskar: "CI is failing" and "Analyse the failure using exported spans". On the RabbitMQ catch-up test: "why did you filtered messagetypes in the test instead of just awaiting it to settle?" and "what I'm saying is that it should be enough to say: just wait until all is caught up". Done: `CatchingUpWith_ReplacesTheProbeAndTheCheck` waits until the shipping queue is empty (`RabbitMQBroker.UntilEmpty`) and counts no calls. It had counted the calls of the shared static recorder, which other tests running at the same time also fed.

Oskar, on the dev container: "I'd try docker-in-docker first. - try it". With docker-outside-of-docker, Aspire's Kafka advertised `localhost`, which the dev container can't reach, so the Aspire tests hung in `StartAsync`. Done: the dev container uses the `docker-in-docker:2` feature with `"moby": false` (moby isn't available on Ubuntu resolute), without `TESTCONTAINERS_HOST_OVERRIDE` and `--add-host`. Every target passed in it.

Oskar, on the Aspire hang that left no spans: "can we fix it?" and "A separate start timeout, default 3 minutes, configurable (StartTimeout(...))… Do it". Done: `ApiSpecification.StartTimeout(...)`, 3 minutes by default, fails a host that doesn't start in time with "Starting the host did not finish within …" and ends the `start app` span as an error. `OtlpReceiverOptions.ForwardTo` sends every export the receiver records on to another OTLP endpoint, and the Aspire fixtures forward to `OTEL_EXPORTER_OTLP_ENDPOINT`, so the app's spans reach the MCP server too. `AspireApp` keeps its own `WaitAsync`, because its start is shared by several specifications, and without it each would wait its own 3 minutes.

Oskar, on the Wolverine `Eventually` race: "add to agents.md to be sure you do the next time", after "Are you using telemetry for investigations?". Done: AGENTS.md has "Investigating a failing or hanging test": start from the spans, then the code.

Found from the spans: `ScenarioDiagramTests` expected the old local queue name of `ExpireCart`, which moved to `carts.expiring`; `Run_NestsUnderTheSpanOfTheTest` failed in a plain run because its listener compared the test's `ActivitySource` while the static field was still being initialized, and passed under the MCP server, whose export listens to every source.

Oskar: "what could we do to make ogooreck mcp better for you to test and work on implementation locally?" Proposed: 1. register the server in `.mcp.json`; 2. spans that ran at the same time as a span; 3. the span tree of one test; 4. repeat until a run fails; 5. a hang report; 6. a note that a run under the MCP server listens to every `ActivitySource`; 7. a CLI mode for CI. Oskar: "1-6 All sounds good. Could you add it?" and "What does 7 mean?"

Done: `.mcp.json` starts the server with `dotnet run --project src/dotnet/Ogooreck.Mcp --framework net10.0` (spike: stdout stays JSON-RPC only, also when it rebuilds). `ogooreck_test_spans` returns the run tree of the tests whose name matches, with the app's spans under each request step. `ogooreck_concurrent_spans` lists the spans that overlap the first span whose name matches, without its parents and the spans under it, each with its test and location. `ogooreck_test_run` takes `repeat`, which stops at the first run with a failure and keeps its spans, and `hangTimeout`, which passes `--blame-hang-timeout` to a VSTest project and returns `RunningWhenAborted` (the tests VSTest names) and `NeverEnded` (Ogooreck spans grouped by the parent that never arrived). The run's description and the server instructions say that a test that passes here but fails in CI should run with plain `dotnet test` too.

Spike for the hang report: a test hanging in its When step left `Before`, `start app`, `Given`, its step and catch-up, and the app's `POST` span; the run span and the When step never arrived, because OpenTelemetry exports a span only when it ends. So the report names the open run by its ended children, `Before` and `Given`, which shows that the When step hung. For a Microsoft.Testing.Platform project, `hangTimeout` is an error that says to reference `Microsoft.Testing.Extensions.HangDump` and pass `--hangdump-timeout`. The fixture has an explicit test, `WaitingForAResponseThatNeverComes`, which runs only with `-- xUnit.Explicit=on`; the TRX counts it as skipped, so the fixture now counts 5 tests with 2 skipped.

**Open:** 7, the CLI mode for CI; the OpenTelemetry Collector in CI; `--blame-hang-timeout` and `timeout-minutes` in the CI workflows; whether the catch-up loop should count only the current step's messages.
