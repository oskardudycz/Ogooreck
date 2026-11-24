# Plan: Ogooreck 1.0 Emmett-shaped API

Revision: 13. Built from spec r15.

Spec: `spec.md`. Q&A: `qa.md`.

## Terms

- **Step (test step)**: one item of Given or When: `Request`, `Attempt`, `Action`, `Publish` or `Eventually`.
- **When step**: the single step given to `When`. Messages and events count for the test only when they carry its trace id.
- **Test's trace ids**: the set of trace ids of every Given step and every attempt of the When step. Spans and in-process measurements count when their trace id is in it.
- **Recorded<T>**: the one type Ogooreck ships for plugging in a messaging tool or event store; user code calls `Add(T)`, Ogooreck reads. It is an unbounded channel written with `TryWrite`.
- **Observed**: what the user's `read` function returns for an item: the message, its `traceparent`, and optionally message, correlation and causation ids.
- **Plug**: an `IPlug` passed to `Use(...)`; it wires one tool into a specification through `PlugRegistration` (services in the test host, recorders, catch-up, publishing).
- **Catch-up function**: a function that tells Ogooreck the tool is idle. After each step Ogooreck runs the catch-up functions in rounds until a round adds no items.
- **Deadline**: the bound that makes a waiting operation fail (30 seconds by default). It never makes a passing test wait.
- **Eventually**: an opt-in When request that is re-sent while its response expectations fail.
- **Telemetry source**: an `ITelemetrySource`: in-process listeners, an `OtlpReceiver`, `TelemetrySources.Off`, or a custom source.
- **Baseline**: the metric values taken before the When step; `ExpectMetric` checks the increase since it.
- **0.x API**: the API of Ogooreck 0.10 that step 1 removes.

Plan conventions: every step keeps `./build.sh test` (from `src/dotnet`) green, including `dotnet format --verify-no-changes` and `dotnet fantomas --check .`. Tests are written first, name the observable behaviour and use the real sample apps; no mocking library. A phrase like "about 2 seconds" in a test means at least 2 seconds and under 10 seconds. New sample projects are added to `Ogooreck.slnx` and to `Ogooreck.Build/Program.cs` in the step that creates them, and follow the naming of `Ogooreck.Sample.Api` and `Ogooreck.Sample.Api.Tests`. The spec's examples write tag sets as `{ ["k"] = "v" }`; tests pass a `IReadOnlyDictionary<string,string>` in whatever C# form compiles.

Criteria group 26.6 has an in-process half (step 3a) and an Aspire half (step 5). The group is listed in step 3a; step 5 holds the Aspire half as an extra test.

---

## Step 1. Replace the 0.x API with the in-process ApiSpecification<TProgram>: requests and response expectations (done)


```text
Use the `implement-step` skill.

What to build
A test class with `IClassFixture<ApiSpecification<Program>>` writes `Given(steps).When(step).Then(expectations)`. A request step is a plain function of `HttpClient`, optionally with a `StepContext` that exposes earlier responses and `CreatedId()`. A failing Given request fails the test, unless it is wrapped in `Attempt`. `Scenario(...)` and `Given(scenario, more)` join step arrays. `ExpectResponse(status, Body/Headers/ETag/WeakETag/Location/Satisfying/HtmlContaining/HtmlNotContaining/RedirectsTo)` and `ExpectError(...)` check the response, with `Body` comparing JSON in the ordered mode of `StructuralComparison`. Every request carries a fresh `traceparent` per step. The 0.x API is removed in this step, in one change, because the old and the new `ApiSpecification` types cannot coexist. The business logic specifications stay untouched.

Scope inside this step (all from "Implementation Decisions" in the spec): `ApiSpecification<TProgram>` with the parameterless constructor, the `configure` constructor, `For<TProgram>()`, `Host(Func<WebApplicationFactory<TProgram>, WebApplicationFactory<TProgram>>)` (needed to migrate `JsonSettingsTests`), `IDisposable`/`IAsyncDisposable`, lazy host start on the first step; `TestStep`, `Request` (both overloads), `Attempt`, `Action` (only as far as 23.10 needs it: an action step produces no response; criteria group 11 stays in step 2b), `StepContext` (`Responses`, `CreatedId()`, `CreatedId<T>()`; `Traceparent` is added in step 2b with the criteria that read it), `Scenario(...)`, `Given(params TestStep[])`, `Given(TestStep[], params TestStep[])`, `When`, `Then(params Expectation[])`, `Then(Expectation[], CancellationToken)`; the ordered mode of `StructuralComparison`; the static factories on non-generic `ApiSpecification` (the type is created here as the holder of static factories; its host-by-address role comes in step 5). Removed: everything in the spec's list under "Package and release" (builders, `RetryPolicy`, `Until`, `TestApiRequest`, 0.x `Scenario(...)`/`And(...)`, the request DSL, the response helpers). Kept: `ApiJsonSettings`, `StructuralComparison`, `GetCreatedId`, `GetResultFromJson`. Other 0.x types and helpers the spec does not name (`TestContext`, `MadeApiCall`, `ApiTestStep`, `RequestDefinition`, `ResponseAssert`, `RequestTransform`, `GetETagValue`, `TryGetETagValue`, `TryGetCreatedId`) stay only if the new code uses them; the rest are removed (qa 39).

The sample API gains the cart and order endpoints the criteria name (`POST /carts/{id}/products`, `GET /carts/{id}`, `POST /carts/{id}/orders`, an order page, a redirect, an HTML page, an enum serialized as a string, an ETag/Location/`X-Version` endpoint, a `409` ProblemDetails endpoint, a server-generated id `201` endpoint). The existing products endpoints stay.

Context
- `src/dotnet/Ogooreck/API/ApiSpecification.cs`, `ApiSpecificationExtensions.cs`, `TestBuilders.cs`, `RetryPolicy.cs`: the 0.x types; replaced.
- `src/dotnet/Ogooreck/API/ApiJsonSettings.cs`: kept; `FromServices` supplies the default JSON options for `Body`.
- `src/dotnet/Ogooreck/API/HttpResponseMessageExtensions.cs`: keeps `GetCreatedId`, `GetResultFromJson(HttpResponseMessage)`; the `GetResultFromJson(this ApiSpecification.Result)` overload goes with `Result`.
- `src/dotnet/Ogooreck/Assertions/StructuralComparison.cs`: `AssertEquivalent` (unordered nested collections, default) and `AssertOrderedEquivalent` (only the top-level list is ordered today). Add the ordered mode that orders every nested collection.
- `src/dotnet/Ogooreck.Sample.Api/Program.cs`: the products API; gets the endpoints listed above.
- `src/dotnet/Ogooreck/Ogooreck.csproj`: references `Microsoft.AspNetCore.Mvc.Testing`; no test framework reference may be added.

Similar code
- `src/dotnet/Ogooreck.Tests/ComparisonTests.cs`, `ComparisonCases.cs`, `ComparisonCharacterizationTests.cs`: how `StructuralComparison` is tested; follow them for the ordered mode.
- `src/dotnet/Ogooreck.Sample.Api.Tests/ApiTests.cs`: the class-fixture usage and the `#region ApiGetSample` / `ApiPostSample` snippets that the README pulls in; keep the region names.
- `src/dotnet/Ogooreck.Sample.BusinessLogic.Tests/Deciders/BankAccountTests.cs` and the F# `Deciders` folder: business logic specifications, unchanged.
- Java reference: `EventSourcing.JVM/samples/e-commerce/src/test/java/io/eventdriven/testing/api/ApiE2ESpecification.java` (`given`, `when`, `then`, `scenario`, `expectResponse`, `expectError`, `htmlContaining`, `redirectsTo`). When something is unclear, do what the Java version does (Q&A answer 31).

Blocked by
None

Existing tests this step breaks, and how it updates them
- `Ogooreck.Sample.Api.Tests/ApiTests.cs` (`GetProducts`, `RegisterProduct`, `CanRegisterAndReadAProductUsingAClassFixture`): all use the removed `GET`, `URI`, `BODY`, `OK`, `CREATED`, `RESPONSE_*`, `AndWhen`. Rewrite with `Request(client => client.GetAsync("/api/products"))`, `Request(client => client.PostAsJsonAsync(...))`, `ExpectResponse(...)`, `Location(...)`, and a `Request((client, ctx) => client.GetAsync($"/api/products/{ctx.CreatedId()}"))`; keep the `ApiGetSample` and `ApiPostSample` regions and the `Then([...], ct)` form.
- `Ogooreck.Sample.Api.Tests/JsonSettingsTests.cs`: built on `ApiSpecification<Program>.Setup(factory)`, `SEND`, `Until`, `RESPONSE_BODY`, `context.Calls`, `GetResponseBody`. Migration, test by test: `InheritsHostSettingsThroughoutTheScenario` becomes a theory over `mvc` that sends the body through `PostAsJsonAsync` with the options the test builds itself and asserts `Body(Example)` and the raw response text; the request-side assertions on `RequestBodies` stay (the test serializes with its own options, so they now check the host's deserialization of that input). `UsesActualMvcInputAndOutputFormatterOptions` keeps the output-formatter assertions with `Body`. `ConcurrentHostsAndScenariosDoNotShareSettings` keeps its shape with `ApiSpecification.For<Program>().Host(f => ...)` per instance. `DefaultsWorkWithoutAnyExplicitJsonConfiguration` moves into `ApiTests` (created id read with `ctx.CreatedId<Guid>()`). `FailedExpectationsAndPredicatesStillPropagateWithHostSettings` is deleted: its checks are covered by the 23.x tests (`RESPONSE_BODY_MATCHES` has no 1.0 counterpart). `MixedHostDefaultsToMvcAndAllowsMinimalApiOverrides` keeps its MVC half; its minimal-API override half and `ResponseOverridesApplyToEveryAssertionAndExtractionWithoutLeaking` need `Json(options)`, which step 3a adds, so they are deleted here and rebuilt in step 3a as 6.4 tests.
- `Ogooreck.Tests/HttpExpectationsTests.cs`: `ResponseUsesWebDefaultsForRecords`, `InvalidOrEmptyJsonFails`, `JsonNullFailsAnExpectation`, `StandardConstructorAttributesAreRespected` stay (they test the kept `GetResultFromJson`). `BothResponseOverloadsHonorCustomOptions` keeps only its `HttpResponseMessage` half (rename it to drop "Both"). `BodyExpectationChecksActualValuesAndInvokesCallbacks` and `StatusAndHeaderExpectationsRejectMismatches` test removed helpers (`RESPONSE_BODY`, `OK`, `RESPONSE_ETAG_HEADER`, `RESPONSE_LOCATION_HEADER`); they are deleted and replaced by the 23.x tests. Their `GetETagValue`/`GetCreatedId<int>` assertions: the `GetCreatedId<int>` one moves to a test of the kept helper; the `GetETagValue` one moves to a test of that helper if the new code keeps it (for example for `ETag(...)`), otherwise it is deleted (qa 39).
- `Ogooreck.Tests/AsyncScenarioTests.cs`: tests the removed `And(...)` continuations on `Result`. Deleted. The behaviours it guarded that still exist (a failed earlier step prevents later steps and keeps its exception) are covered by the 9.1 test and the extra test on a failing Given at each position.
- `Ogooreck.Tests/ApiJsonSettingsTests.cs`: tests 1 and 2 stay. Tests 3 and 4 use the 0.x `TestContext`: if the new code no longer uses `TestContext`, drop the `TestContext` line from test 3 and delete test 4 (qa 39).
- `README.md` and `mdsource/README.source.md`: hand-written code blocks that show the 0.x API stay until step 6c rewrites the README; snippet regions that still exist keep generating.

Acceptance criteria
- 1. A user tests an in-process app with no configuration, so they can start testing with one line.
  - 1.1 GIVEN a test class with `IClassFixture<ApiSpecification<Program>>` WHEN it runs `Given(AddProduct(cartId, "socks")).When(GetCart(cartId)).Then(ExpectResponse(OK, Body(new { products = new[] { "socks" } })))` THEN the test passes, and only one `WebApplicationFactory<Program>` starts for the class.
  - 1.2 GIVEN `ApiSpecification.For<Program>()` with no further calls AND an app that serializes enums as strings WHEN the response contains `"status":"Placed"` THEN `Body(new { status = OrderStatus.Placed })` matches, because the app's JSON options are used.
- 7. A user sends requests as plain `HttpClient` calls, so they need no Ogooreck request DSL.
  - 7.1 GIVEN `Request(client => client.PostAsJsonAsync($"/carts/{cartId}/products", new { product = "socks" }))` as a Given step WHEN the test runs THEN the app receives that POST with that body.
- 8. A user uses a server-generated id from an earlier response, so apps with server-generated ids stay testable.
  - 8.1 GIVEN a Given step whose response is `201 Created` with `Location: /carts/42` AND the When step `Request((client, ctx) => client.GetAsync($"/carts/{ctx.CreatedId()}"))` WHEN the test runs THEN the When request goes to `/carts/42`.
  - 8.2 GIVEN the same Given step WHEN the When step calls `ctx.CreatedId<int>()` THEN it gets `42`.
  - 8.3 GIVEN no earlier `201` response WHEN a step calls `ctx.CreatedId()` THEN the test fails, saying that no created response was found.
  - 8.4 GIVEN two Given steps WHEN the When step reads `ctx.Responses` THEN it gets both responses in step order.
- 9. A failing Given request fails the test, so a broken setup isn't mistaken for a passing scenario.
  - 9.1 GIVEN a Given request that returns `404` with body `{"title":"Cart not found"}` WHEN the test runs THEN it fails before the When step, and the message shows the status `404` and that body.
- 10. A user expects a Given request to fail, so they can set up a rejected attempt.
  - 10.1 GIVEN `Attempt(PlaceOrder(unknownCartId))` returning `404` as a Given step WHEN the test runs THEN the When step runs, and the test result depends only on the expectations.
- 14. A user reuses a named group of steps, so scenarios read as the business story.
  - 14.1 GIVEN `TestStep[] cartWithSocks = [AddProduct(cartId, "socks")]` WHEN a test calls `Given(cartWithSocks, ConfirmCart(cartId))` THEN both steps run, in that order.
  - 14.2 GIVEN `cartWithSocks` WHEN a test builds `TestStep[] confirmedCart = Scenario(cartWithSocks, ConfirmCart(cartId))` and calls `Given(confirmedCart)` THEN both steps run, in that order.
- 23. A user checks the response, so they test the app's HTTP contract.
  - 23.1 GIVEN the When step returns `201` WHEN `ExpectResponse(HttpStatusCode.OK)` runs THEN it fails with the expected `200`, the actual `201` and the response body.
  - 23.2 GIVEN the response body `{"id":"…","status":"Placed","total":20}` WHEN `ExpectResponse(OK, Body(new { status = "Placed" }))` runs THEN it passes, because extra fields are allowed.
  - 23.3 GIVEN the response body `{"items":["a","b"]}` WHEN `ExpectResponse(OK, Body(new { items = new[] { "b", "a" } }))` runs THEN it fails, because array order matters.
  - 23.4 GIVEN the response body `{"cart":{"lines":[{"sku":"a"},{"sku":"b"}]}}` WHEN `Body(new { cart = new { lines = new[] { new { sku = "b" }, new { sku = "a" } } } })` runs THEN it fails, because nested array order matters too.
  - 23.5 GIVEN the response header `ETag: W/"2"` WHEN `ExpectResponse(OK, ETag(WeakETag(2)))` runs THEN it passes.
  - 23.6 GIVEN a `201` with `Location: /carts/42` WHEN `ExpectResponse(Created, Location("/carts/42"))` runs THEN it passes.
  - 23.7 GIVEN a response header `X-Version: 3` WHEN `ExpectResponse(OK, Headers(("X-Version", "4")))` runs THEN it fails, naming the header, `4` and `3`.
  - 23.8 GIVEN any response WHEN `ExpectResponse(OK, Satisfying(response => ...an assertion that throws "boom"...))` runs THEN the test fails with "boom".
  - 23.9 GIVEN a response with body `{"status":"Placed"}` WHEN `ExpectResponse(OK, Satisfying(async response => ...reads the body asynchronously and asserts "Cancelled"...))` runs THEN the test fails with that assertion's message, after the async check completes.
  - 23.10 GIVEN a When step that is an action WHEN `ExpectResponse(OK)` runs THEN it fails, saying that the When step got no response.
  - 23.11 GIVEN a `201` with a server-generated `Location: /products/7` WHEN `ExpectResponse(Created, Location(l => l.StartsWith("/products/")))` runs THEN it passes, and `Location(l => l.StartsWith("/carts/"))` fails, showing the actual `Location`.
  - 23.12 GIVEN a When step whose response is `302` with `Location: /carts/42` WHEN `ExpectResponse(Found, RedirectsTo("/carts/42"))` runs THEN it passes, because the client doesn't follow redirects.
  - 23.13 GIVEN a test whose Given step sets `client.DefaultRequestHeaders.Add("X-Tenant", "a")` inside its request lambda WHEN a parallel test's When step on the same specification echoes its request headers THEN `X-Tenant` is absent, because each step gets its own client.
  - 35.1 GIVEN a Given request that returns `404` WHEN the test runs THEN the failure message names "Given step 1", the request's method and URI, the status and the body.
  - 35.2 GIVEN a When request to an endpoint that never finishes its response WHEN `ct` is cancelled THEN `Then` ends with `OperationCanceledException`, and its message names the When request with its method and URI.
- 24. A user checks an error response, so they test rejections.
  - 24.1 GIVEN a `409` with a ProblemDetails body `{"title":"Cart already closed","status":409}` WHEN `ExpectError(Conflict, new { title = "Cart already closed" })` runs THEN it passes.
  - 24.2 GIVEN a `409` WHEN `ExpectError(NotFound)` runs THEN it fails with both statuses.
- 25. A user checks a server-rendered page, so they test HTML endpoints.
  - 25.1 GIVEN a `text/html` response containing `Order placed` WHEN `ExpectResponse(OK, HtmlContaining("Order placed"))` runs THEN it passes, and `HtmlNotContaining("Order placed")` fails.
  - 25.2 GIVEN a JSON response WHEN `HtmlContaining("x")` runs THEN it fails, saying that the content type is not HTML.
  - 25.3 GIVEN a `303` with `Location: /orders/7;jsessionid=1?x=1` WHEN `ExpectResponse(SeeOther, RedirectsTo("/orders/7"))` runs THEN it passes.
- 34. Business logic specifications stay as they are, so 1.0 doesn't break domain tests. This protects existing behavior.
  - 34.1 GIVEN the existing business logic sample tests in C# and F# WHEN run against 1.0 THEN they pass unchanged.
  - 34.2 GIVEN a decider whose resulting state has `Products = ["socks", "shoes"]` WHEN `Then(expectedState)` expects `["shoes", "socks"]` THEN it still passes, because business logic specs keep comparing nested collections without order.

Tests
- 1.1: getCartAfterAddingProduct_returnsTheProduct_withOneHostPerClass - asserts class fixture with two tests; both pass; a start counter in the sample app (incremented in service registration) equals 1 after both ran
- 1.2: body_matchesEnumSentAsString_usingTheAppsJsonOptions - asserts sample endpoint returns `{"status":"Placed"}` with the enum converter registered in the app; `For<Program>()` with no calls; `Body(new { status = OrderStatus.Placed })` passes
- 7.1: postRequestStep_sendsTheBodyToTheApp - asserts sample endpoint stores the received body; after the Given step the stored body equals `{"product":"socks"}` and the method is POST
- 8.1: createdIdFromAnEarlierResponse_isUsedInTheNextRequestUrl - asserts Given returns 201 with `Location: /carts/42`; the When request reaches `/carts/42` (the sample echoes the path it was called with)
- 8.2: createdIdOfT_parsesTheLocation - asserts `ctx.CreatedId<int>()` equals 42
- 8.3: createdId_withoutACreatedResponse_failsSayingNoCreatedResponseWasFound - asserts only 200 responses before; the test throws with a message containing that no created response was found
- 8.4: responses_listsEarlierResponsesInStepOrder - asserts two Given steps with distinct bodies; `ctx.Responses` has 2 items, bodies in step order
- 9.1: failingGivenRequest_failsBeforeWhen_showingStatusAndBody - asserts Given returns 404 `{"title":"Cart not found"}`; exception message contains `404` and the body; a counter in the When step stays 0
- 10.1: attemptedGivenRequest_thatFails_letsTheWhenStepRun - asserts `Attempt(PlaceOrder(unknownCartId))` returns 404; the When step runs (counter 1); with a passing expectation the test passes, with a failing expectation it fails
- 14.1: givenWithAStepArrayAndMoreSteps_runsBothInOrder - asserts `Given(cartWithSocks, ConfirmCart(cartId))`; the sample records call order: AddProduct then ConfirmCart
- 14.2: scenarioJoiningAnArrayAndSteps_runsBothInOrder - asserts `Scenario(cartWithSocks, ConfirmCart(cartId))` returns a new `TestStep[]` of length 2; `Given(confirmedCart)` runs both in order; the source array is unchanged
- 23.1: expectResponse_withWrongStatus_failsWithExpectedActualAndBody - asserts When returns 201 with a body; `ExpectResponse(OK)` message contains `200`, `201` and the body text
- 23.2: body_allowsExtraResponseFields - asserts response `{"id":"…","status":"Placed","total":20}`; `Body(new { status = "Placed" })` passes
- 23.3: body_failsWhenTopLevelArrayOrderDiffers - asserts response `{"items":["a","b"]}`; expected `["b","a"]` fails
- 23.4: body_failsWhenNestedArrayOrderDiffers - asserts response `{"cart":{"lines":[{"sku":"a"},{"sku":"b"}]}}`; expected lines b,a fails
- 23.5: etag_matchesAWeakETag - asserts header `ETag: W/"2"`; `ETag(WeakETag(2))` passes
- 23.6: location_matchesTheLocationHeader - asserts 201 with `Location: /carts/42`; `Location("/carts/42")` passes
- 23.7: headers_withDifferentValue_failsNamingHeaderExpectedAndActual - asserts `X-Version: 3`, expected `4`; message contains `X-Version`, `4`, `3`
- 23.8: satisfying_propagatesTheAssertionsException - asserts assertion throws `boom`; the test fails with message `boom`
- 23.9: satisfyingAsync_failsWithTheAssertionMessageAfterTheAsyncCheck - asserts async check reads the body `{"status":"Placed"}` and asserts `Cancelled`; failure message is that assertion's; a flag set after an `await Task.Yield()` inside the check is true when the failure surfaces
- 23.10: expectResponse_afterAnActionWhenStep_failsSayingNoResponse - asserts When is `Action(...)`; message says the When step got no response
- 23.11: location_predicate_matchesAServerGeneratedLocation - asserts `RegisterProduct` (README snippet) passes with `Location(l => l.StartsWith("/products/"))`, and `Location(l => l.StartsWith("/carts/"))` fails with a message containing the actual `Location`
- 23.12: redirect_isNotFollowed_soRedirectsToSeesThe302 - asserts a `302` with `Location: /carts/42` reaches the expectations as `302`, and `RedirectsTo("/carts/42")` passes
- 23.13: clientDefaults_changedInOneStep_doNotReachAParallelTest - asserts a step that adds `X-Tenant` to `client.DefaultRequestHeaders` does not make the header appear in another test's echoed request headers on the same specification
- 35.1: givenRequestFailure_namesTheStepRequestStatusAndBody - asserts the message contains "Given step 1", `GET` and the URI, `404` and the body
- 35.2: cancellingThen_duringTheWhenRequest_namesTheRequest - asserts `OperationCanceledException` whose message names the When request's method and URI; when the server also reports an aborted request, that error is the inner exception and the message names it
- extra: body_onANonJsonResponse_failsShowingExpectedAndActual - covers `Body(new { status = "Placed" })` on a `text/plain` body `Placed`: the failure names the expected value and shows the actual body, not a raw `JsonException`
- extra: noAnalyzerSuppression_testsPassTheToken - covers the sample test projects build with warnings as errors and without suppressing xUnit1051, passing `TestContext.Current.CancellationToken` to `Then(...)` and to `HttpClient` calls (qa 43)
- 24.1: expectError_matchesStatusAndProblemDetailsPartially - asserts 409 `{"title":"Cart already closed","status":409}`; `ExpectError(Conflict, new { title = "Cart already closed" })` passes
- 24.2: expectError_withWrongStatus_failsWithBothStatuses - asserts 409 vs `ExpectError(NotFound)`; message contains `409` and `404`
- 25.1: htmlContaining_checksTheHtmlBody - asserts `text/html` body containing `Order placed`; `HtmlContaining("Order placed")` passes; `HtmlNotContaining("Order placed")` fails
- 25.2: htmlContaining_onAJsonResponse_failsSayingContentTypeIsNotHtml - asserts JSON response; message says the content type is not HTML
- 25.3: redirectsTo_ignoresPathParametersAndQuery - asserts 303 `Location: /orders/7;jsessionid=1?x=1`; `RedirectsTo("/orders/7")` passes
- 34.1: businessLogicSampleTests_passUnchanged - asserts the existing C# and F# business logic sample test projects compile and pass with no edit to their files (run as part of `./build.sh test`)
- 34.2: deciderThen_stillComparesNestedCollectionsWithoutOrder - asserts state `Products = ["socks","shoes"]` vs expected `["shoes","socks"]` passes through `DeciderSpecification.Then(expectedState)`; the test lives in the business logic samples next to `BankAccountTests`
- extra: orderedMode_comparesNestedCollectionsInOrder_andDefaultModeDoesNot - covers in `Ogooreck.Tests` next to `ComparisonTests`: same pair of objects with nested lists in different order passes in default mode and fails in ordered mode, with the path of the difference in the message
- extra: orderedMode_failsOnDifferentCollectionLength - covers ordered mode, nested lists of 2 vs 3 items
- extra: everyRequest_carriesATraceparentWithAUniqueTraceIdPerStep - covers sample endpoint echoes the `traceparent` header; three steps give three valid W3C values with three different trace ids (protects 4.2 and 11.2 later; the handler is built here)
- extra: requestStep_withoutAnHttpResponseBodyOrWithAnEmptyBody_failsBodyExpectationClearly - covers `Body(...)` on a 204 fails with a message naming the empty body
- extra: then_withACancelledToken_endsWithOperationCanceledException - covers token cancelled before `Then`; exception type is `OperationCanceledException`
- extra: disposing_stopsTheHost - covers after `Dispose()`/`DisposeAsync()` a request through a client kept from the host fails
- extra: hostCallThatCustomisesTheFactory_isUsedByTheApp - covers `For<Program>().Host(f => f.WithWebHostBuilder(...))` registers a service the endpoint returns
- extra: createdId_readsOnlyTheFirst201Response - covers two 201 responses (`/carts/1`, `/carts/2`); `CreatedId()` is `1`
- extra: failingGivenRequest_atEachPosition_failsWithTheFailedStepsStatus - covers Given steps [200, 500, 200]: message shows `500`, the third step never runs
- extra: expectedErrorBodyMismatch_failsNamingTheDifferentField - covers `ExpectError(Conflict, new { title = "other" })` on the 409 of 24.1 fails with the path `title`
- extra: jsonSettingsTests_migrated - covers see the migration list below: Body uses the host's MVC and minimal-API JSON options in `JsonSettingsTests` (theory over `mvc`), the actual MVC output formatter options, and two hosts with different options in parallel do not share settings
```

---

## Step 2. Messaging in the Wolverine sample: recording, catch-up, Action and Publish

Runs as one step with one review and one fix round: part 2a, part 2b, in this order.

### Part 2a. Record messages and wait for catch-up: Recorded<T>, Recording, CatchUp, ExpectPublished, ExpectNothingPublished (Wolverine sample)

```text
Use the `implement-step` skill.

What to build
A user plugs a messaging tool into a test through `Recorded<T>`, `Recording(recorded, read[, catchUp])` and `CatchUp(...)`, and checks what the When step published. Messages count as the When step's only when they carry the When step's trace id. After every step Ogooreck runs the catch-up functions in rounds until a round adds no items, bounded by `Deadline(...)`. `ExpectPublished` compares exactly on a recorder with catch-up; `ExpectNothingPublished` checks the list is empty and fails at once on a recorder without catch-up. A subclass (`ApiSpecification<Program>` with the `configure` constructor) or a fixture (`For<Program>().Recording(...)`) holds the shared setup, also as an xUnit v3 assembly fixture.

Scope: `Recorded<T>` (unbounded `Channel<T>`, `TryWrite`), `Observed` (both constructors), `Recording` (both overloads, repeatable), `CatchUp` (repeatable), `Deadline`, the catch-up loop, trace attribution of observed messages, `ExpectPublished` on a recorder with catch-up, `ExpectNothingPublished`, numbered failure listings. `ExpectPublished` on a recorder without catch-up (subsequence matching) is added in step 4a, because only the Kafka sample runs it. `ExpectAppended` and `ExpectNothingAppended` are added in step 4b. Configuration calls added here take effect before the first step; the throw on a call after the first step is added with group 6 in step 3a.

New sample: a Wolverine in-memory app (local queues, in-app middleware as the hook) and its test project (`Ogooreck.Sample.Wolverine`, `Ogooreck.Sample.Wolverine.Tests`, following `Ogooreck.Sample.Api` / `.Api.Tests`), added to `Ogooreck.slnx` and to `Ogooreck.Build/Program.cs` as a test target of `test`. The app handles the cart/order flow of the criteria, publishes `OrderPlaced`, `CartClosed`, `ProductAdded`, `StockChecked`, reacts to messages with further messages, and exposes an asynchronous read model. The test project's `AppApi` registers a publish observer in the test host only, with `Recording(PublishedMessages, read, catchUp)`, where `catchUp` uses Wolverine's native tracked sessions (`TrackActivity`), plugged with the wrapping `CatchUp`/`Recording` overload (spec r8).

Context
- `src/dotnet/Ogooreck/API/` (the types from step 1): `Given`/`When`/`Then` run steps in order; add the per-step trace id set, the catch-up call after each step and the expectation base.
- `src/dotnet/Ogooreck.slnx`, `src/dotnet/Ogooreck.Build/Program.cs` (targets `test-sample-api`, `test-library`, ... and the `test` target): add the new test project.
- `.github/workflows/dotnet-ci.yml`: runs `./build.sh test` on `ubuntu-latest`; Wolverine in-memory needs no Docker.

Similar code
- Java `ApiE2ESpecification.java`: the `whenCaughtUp` loop (`published.size() != seen`), `expectPublished`, `expectNothingPublished`, `Transcript.numbered`, the `catchUpTimeout` field.
- `src/dotnet/Ogooreck.Sample.Api.Tests/ApiTests.cs`: class-fixture usage.
- `src/dotnet/Ogooreck.Sample.Api.Tests/JsonSettingsTests.cs` (parallel hosts) for tests that run in parallel with separate ids.

Blocked by
Step 1

Existing tests this step breaks, and how it updates them
- No existing test breaks. Existing sample tests use the specification without recorders, so catch-up must not run for them (22.4 protects this).
- `Ogooreck.Build/Program.cs`: the `test` target gains the Wolverine test target; `./build.sh test` is the check.

Acceptance criteria
- 2. A user configures the specification once per test project, so every test class shares it.
  - 2.1 GIVEN a class `AppApi : ApiSpecification<Program>` passing `Recording(...)` to the base constructor AND two test classes with `IClassFixture<AppApi>` WHEN both run THEN both use the recording, and neither repeats the configuration.
  - 2.2 GIVEN a fixture holding `ApiSpecification.For<Program>().Recording(...)` WHEN a test calls `Spec.Given(...).When(...).Then(...)` THEN it behaves the same as 2.1.
  - 2.3 GIVEN `AppApi` registered as an xUnit v3 assembly fixture WHEN 3 test classes run THEN one host starts in total.
- 3. A user customizes the in-process host, so test-only hooks are registered.
  - 3.1 GIVEN `.Host(f => f.WithWebHostBuilder(...registers a publish observer...))` WHEN a When step publishes `OrderPlaced` THEN the observer adds it to `AppApi.PublishedMessages`.
- 15. A user checks what the When step published, so they test the app's outgoing contract.
  - 15.1 GIVEN a recorder with a catch-up function AND the When step publishes `OrderPlaced(cartId)` WHEN `ExpectPublishedIn(AppApi.PublishedMessages, new OrderPlaced(cartId))` runs THEN it passes.
  - 15.2 GIVEN the same AND the When step also publishes `CartClosed(cartId)` after `OrderPlaced` WHEN `ExpectPublishedIn(AppApi.PublishedMessages, new OrderPlaced(cartId))` runs THEN it fails, listing the expected `OrderPlaced` and the published `OrderPlaced` and `CartClosed`, numbered.
  - 15.3 GIVEN the same WHEN `ExpectPublishedIn(AppApi.PublishedMessages, new CartClosed(cartId), new OrderPlaced(cartId))` runs THEN it fails, because the order differs.
  - 15.4 GIVEN the When step publishes `OrderPlaced(cartId)` with total `10` WHEN `ExpectPublishedIn(AppApi.PublishedMessages, new OrderPlaced(cartId) { Total = 20 })` runs THEN it fails, because messages are compared on all fields.
  - 15.5 GIVEN the When step publishes `OrderPlaced(cartId) { Products = ["socks", "shoes"] }` WHEN `ExpectPublishedIn(AppApi.PublishedMessages, new OrderPlaced(cartId) { Products = ["shoes", "socks"] })` runs THEN it fails, because nested collections are compared in order.
- 16. Messages from other steps and tests don't count, so tests can run in parallel on a shared broker.
  - 16.1 GIVEN a recorder with a catch-up function AND a Given step that publishes `ProductAdded(cartId)` WHEN the When step publishes only `OrderPlaced(cartId)` THEN `ExpectPublishedIn(AppApi.PublishedMessages, new OrderPlaced(cartId))` passes. It would fail if `ProductAdded` counted, because the check is exact.
  - 16.2 Removed (spec r9, qa 53).
  - 16.3 GIVEN a recorded item whose `read` result has no `traceparent` AND the When step's own `OrderPlaced(cartId)` WHEN `ExpectPublishedIn(AppApi.PublishedMessages, new OrderPlaced(cartId))` runs on a recorder with catch-up THEN it passes, and the item without a `traceparent` doesn't count.
  - 16.4 GIVEN a Given step whose message arrives after the When step started WHEN the When step's expectations run THEN that message doesn't count, because it carries the Given step's trace id.
- 17. A user checks that nothing was published, so they test that a rejected command has no effect.
  - 17.1 GIVEN a recorder with a catch-up function AND a When step that publishes nothing WHEN `ExpectNothingPublishedIn(AppApi.PublishedMessages)` runs THEN it passes.
  - 17.2 GIVEN the same AND a When step that publishes `OrderPlaced(cartId)` WHEN `ExpectNothingPublishedIn(AppApi.PublishedMessages)` runs THEN it fails, listing `OrderPlaced`.
  - 17.3 GIVEN a recorder without a catch-up function WHEN `ExpectNothingPublishedIn(recorded)` runs THEN it fails at once, saying that it needs a catch-up function on that recorder.
- 21. Recording never slows the tool, so tests measure the app, not Ogooreck.
  - 21.1 GIVEN an in-app observer that adds 10,000 items to a recorder that no expectation reads WHEN the When step runs THEN no `Add` call blocks or throws.
- 22. Ogooreck waits for the app to settle after each step, so the next step sees the effects of the previous one.
  - 22.1 GIVEN `.CatchUp(ct => store.WaitForNonStaleProjectionDataAsync(...))` AND a Given step that adds a product WHEN the When step reads the cart THEN the projection already contains the product, without `Eventually`.
  - 22.2 GIVEN a recorder whose items cause handlers to publish more items WHEN catch-up runs THEN it repeats until a round adds no items, and only then runs the next step.
  - 22.3 GIVEN a specification configured with a 2-second deadline AND an app that keeps publishing forever WHEN a step runs THEN the test fails after about 2 seconds, saying that the app kept producing messages for longer than the deadline.
  - 22.4 GIVEN no catch-up function WHEN steps run THEN each step starts right after the previous one returned, with no delay.

Tests
- 2.1: twoTestClasses_shareTheRecordingConfiguredInAppApi - asserts `AppApi : ApiSpecification<Program>` passes `Recording(...)` once to the base constructor; two classes with `IClassFixture<AppApi>` each pass `ExpectPublishedIn(AppApi.PublishedMessages, new OrderPlaced(cartId))`; the configuration lambda ran twice in total (once per fixture instance), the lambda text appears only in `AppApi`
- 2.2: fixtureHoldingASpecification_behavesLikeASubclass - asserts a fixture with `Spec = ApiSpecification.For<Program>().Recording(...)`; `Spec.Given(...).When(...).Then(ExpectPublished(...))` passes with the same result as 2.1
- 2.3: assemblyFixture_startsOneHostForThreeTestClasses - asserts `[assembly: AssemblyFixture(typeof(AppApi))]`, three test classes each take `AppApi` in the constructor; the start counter in the sample is 1 after all pass
- 3.1: publishObserverRegisteredThroughHost_addsPublishedMessagesToTheRecorder - asserts `.Host(f => f.WithWebHostBuilder(...registers a publish observer...))`; When publishes `OrderPlaced`; `ExpectPublishedIn(AppApi.PublishedMessages, new OrderPlaced(cartId))` passes
- 15.1: expectPublished_passesWhenTheWhenStepPublishedExactlyThatMessage - asserts recorder with catch-up; When publishes `OrderPlaced(cartId)`
- 15.2: expectPublished_failsListingExpectedAndPublishedMessagesNumbered_whenAnExtraMessageWasPublished - asserts When publishes `OrderPlaced` then `CartClosed`; failure message lists `1. OrderPlaced` under expected and `1. OrderPlaced`, `2. CartClosed` under published (assert the numbering and both type names)
- 15.3: expectPublished_failsWhenTheOrderDiffers - asserts expected `CartClosed`, `OrderPlaced`; published `OrderPlaced`, `CartClosed`
- 15.4: expectPublished_failsWhenAFieldDiffers - asserts published total 10, expected total 20; failure names `Total`
- 15.5: expectPublished_failsWhenNestedCollectionOrderDiffers - asserts published `Products = ["socks","shoes"]`, expected `["shoes","socks"]`
- 16.1: expectPublished_ignoresMessagesOfGivenSteps - asserts Given publishes `ProductAdded(cartId)`; When publishes only `OrderPlaced(cartId)`; passes. Companion check in the same test: the recorder holds both messages (read the item count through the test's own second recorder), proving the pass comes from attribution
- 16.2: removed (spec r9, qa 53)
- 16.3: itemWithoutTraceparent_doesNotCount - asserts `read` returns `Traceparent = null` for an extra item added directly to the recorder; the When step's own `OrderPlaced` still passes `ExpectPublished`
- 16.4: lateMessageOfAGivenStep_doesNotCount - asserts the sample consumer of a Given message waits on a gate; the gate opens after the When step started; the late message is recorded (checked with a second recorder) but `ExpectPublished` of the When step's own message passes
- 17.1: expectNothingPublished_passesWhenTheWhenStepPublishedNothing - asserts recorder with catch-up
- 17.2: expectNothingPublished_failsListingThePublishedMessage - asserts When publishes `OrderPlaced(cartId)`; failure lists `OrderPlaced`
- 17.3: expectNothingPublished_withoutCatchUp_failsAtOnceSayingItNeedsACatchUpFunction - asserts recorder without catch-up; fails in under 1 second with the deadline left at 30 s; message names the missing catch-up function on that recorder
- 21.1: addingTenThousandItems_neverBlocksOrThrows - asserts an in-app observer calls `Add` 10,000 times from the When step's thread; every call returns; no exception; the call is measured as a whole under 5 seconds with no expectation reading the recorder
- 22.1: catchUpFunction_makesTheNextStepSeeTheProjection_withoutEventually - asserts `CatchUp(ct => projectionWaiter(ct))` over the sample's asynchronous read model; Given adds a product (read model updated after 200 ms in the sample); When `GET` returns the product on the first request (request count 1)
- 22.2: catchUp_repeatsUntilARoundAddsNoItems_beforeTheNextStep - asserts handler publishes a follow-up message for each of 3 chained messages; the catch-up function call count is 4 (3 rounds that add items and 1 that adds none); the next step starts after it
- 22.3: appThatKeepsPublishing_failsAtTheDeadlineSayingSo - asserts `Deadline(2 s)`; a sample consumer republishes forever; failure after at least 2 s and under 10 s with the message `The application kept producing messages for longer than 00:00:02`
- 22.4: noCatchUpFunction_runsNextStepWithoutDelay - asserts no `Recording` and no `CatchUp`; two steps; the gap between the end of the first and the start of the second is under 100 ms (stopwatch in the steps)
- extra: recordedAdd_afterTheSpecificationIsDisposed_doesNotThrow - covers `Add` on a recorder whose specification was disposed returns without exception
- extra: recording_isRepeatable_andEachRecorderIsCheckedSeparately - covers two recorders registered; messages added to each; `ExpectPublished` on each sees only its own
- extra: catchUp_withoutRecorders_runsTheStandaloneCatchUpFunctionOnceWhenNothingChanges - covers only `CatchUp(...)` registered; function called once per step, no second round
- extra: catchUpFunctionThatThrows_failsTheTestWithItsException - covers exception type and message preserved
- extra: expectPublished_onTheWhenStepsMessagesOnly_whenNoMessageWasPublished_failsListingEmptyPublished - covers published list is shown as empty
- extra: expectPublished_inAnActionOrPublishLessWhenStep_usesTheWhenStepTraceId - covers When is `Request`; the trace id taken from the request's `traceparent` equals the attributed messages' trace id
- extra: itemWithMalformedTraceparentIsIgnored - covers an `Observed` whose `Traceparent` is not a valid W3C value: it counts for no step, the same as no `traceparent` (qa 40)
```

### Part 2b. Run actions and publish messages as other modules: Action context and Publish steps

```text
Use the `implement-step` skill.

What to build
A user moves time or triggers a job in an `Action(ctx => ...)` step and reads the step's `traceparent` from `ctx.Traceparent`. A user puts messages on the app's bus from the test with `Publish(...)`, through the function plugged in with `Publishing((request, ct) => ...)`, which receives the messages and the step's `traceparent`. Messages published from an action's own `Activity` that uses `ctx.Traceparent` as parent are attributed to the When step.

Scope: `StepContext.Traceparent`, the `Action` step (its plain form already exists since step 1), `Publish(params object[])`, `PublishRequest`, `Publishing(...)`. The Wolverine sample gains a fake clock, an expiring cart (`CartExpired` after 1 day), and a handler for `PaymentConfirmed` that publishes `OrderPaid`.

Context
- `src/dotnet/Ogooreck/API/`: `StepContext` and `Action` from step 1 (no `Traceparent` yet); the step runner from step 2a that creates the trace id per step.
- `src/dotnet/Ogooreck.Sample.Wolverine/` and `.Tests/` from step 2a.

Similar code
- Java `ApiE2ESpecification.java`: `action`, `traced(traceparent -> ...)`, `publish`/`send`.
- Step 2a tests in `Ogooreck.Sample.Wolverine.Tests` for the `AppApi` setup and catch-up.

Blocked by
Step 2a

Existing tests this step breaks, and how it updates them
- No existing test breaks.

Acceptance criteria
- 11. A user runs any code as a step, so they can move time or trigger jobs.
  - 11.1 GIVEN `Action(ctx => { clock.Advance(TimeSpan.FromDays(1)); return Task.CompletedTask; })` as the When step AND the app expires the cart after 1 day WHEN `ExpectPublishedIn(AppApi.PublishedMessages, new CartExpired(cartId))` runs THEN it passes.
  - 11.2 GIVEN an action step WHEN it reads `ctx.Traceparent` THEN it gets a valid W3C `traceparent` whose trace id is unique to that step.
  - 11.3 GIVEN an action that starts an `Activity` with `ctx.Traceparent` as parent and publishes `ExpireCart` from it WHEN the app then publishes `CartExpired` THEN `ExpectPublishedIn(AppApi.PublishedMessages, new CartExpired(cartId))` attributes it to the When step.
- 12. A user publishes a message as another module would, so they can test message-driven entry points.
  - 12.1 GIVEN `.Publishing((request, ct) => bus.Publish(..., carrying request.Traceparent))` AND the When step `Publish(new PaymentConfirmed(orderId))` AND the app reacts by publishing `OrderPaid(orderId)` WHEN `ExpectPublishedIn(AppApi.PublishedMessages, new OrderPaid(orderId))` runs THEN it passes.
  - 12.2 GIVEN no `.Publishing(...)` WHEN a `Publish(...)` step runs THEN the test fails, saying that publishing needs a publish function.

Tests
- 11.1: actionAdvancingAFakeClock_expiresTheCartAndPublishesCartExpired - asserts `Action(ctx => { clock.Advance(1 day); ... })` as When; `ExpectPublishedIn(AppApi.PublishedMessages, new CartExpired(cartId))` passes
- 11.2: actionContext_exposesAValidTraceparentUniquePerStep - asserts two action steps record `ctx.Traceparent`; both parse as W3C `traceparent` (version 00, 32-hex trace id, 16-hex span id, flags) and the trace ids differ
- 11.3: messageFromAnActionActivityParentedOnTheStepTraceparent_isAttributedToTheWhenStep - asserts the action starts an `Activity` with `ctx.Traceparent` as parent and publishes `ExpireCart`; the app then publishes `CartExpired` with that trace; `ExpectPublished(..., new CartExpired(cartId))` passes (exact match on a recorder with catch-up, so an unattributed extra would fail)
- 12.1: publishStep_putsAMessageOnTheBusCarryingTheTraceparent_andTheAppReacts - asserts `.Publishing((request, ct) => bus.Publish(..., headers with request.Traceparent))`; When `Publish(new PaymentConfirmed(orderId))`; the app publishes `OrderPaid(orderId)`; `ExpectPublished(..., new OrderPaid(orderId))` passes
- 12.2: publishWithoutAPublishFunction_failsSayingPublishingNeedsAPublishFunction - asserts no `.Publishing(...)`; message says publishing needs a publish function
- extra: publishStep_inGiven_runsBeforeTheWhenStepWithItsOwnTraceId - covers `Publish` as a Given step; the `PublishRequest.Traceparent` trace id differs from the When step's
- extra: publishStep_passesAllMessagesInOrder - covers `Publish(a, b, c)`; `PublishRequest.Messages` equals [a, b, c]
- extra: publishFunctionThatThrows_failsTheTestWithItsException - covers exception preserved
- extra: actionThatThrows_failsTheTestWithItsException - covers exception type and message preserved, later steps do not run
```

---

## Step 3. In-process traces and metrics, then Eventually

Runs as one step with one review and one fix round: part 3a, part 3b, in this order.

### Part 3a. In-process telemetry: Telemetry, ExpectTrace, ExpectCorrelatedTrace, ExpectMetric and fixed configuration

```text
Use the `implement-step` skill.

What to build
A user checks what the app did inside: the span tree of the test (`ExpectTrace(Span(...))`), the correlation of its traces (`ExpectCorrelatedTrace()`), and a metric's increase (`ExpectMetric(name, tags, increase)`). In-process, Ogooreck listens to all `Activity` sources and `Meter` instruments through `TelemetrySources.InProcess()`, a new source for each specification. Spans count when their trace id is in the test's set (every Given step and the When step). Configuration calls replace their defaults and throw `InvalidOperationException` after the first step. `Json(options)` replaces the default JSON options of `Body`. A custom `ITelemetrySource` plugs in.

Scope: `ITelemetrySource` (`Attach`, `Collect`), `RecordedSpan`, `RecordedMeasurement`, `TelemetrySources.InProcess()` and `.Off`, `Telemetry(...)`, `Json(...)`, the configuration lock for every configuration call from steps 1, 2a (`Host`, `Recording`, `CatchUp`, `Deadline`, `Telemetry`, `Json`), `ExpectTrace` with `Span(name, ...)` overloads (matching follows Java `SpanTree.mismatchOf`), `ExpectCorrelatedTrace`, `ExpectMetric` with baselines, delta and cumulative handling, histogram counting, measurements without a trace id counting by tags, observable instruments through `RecordObservableInstruments()`, attribute values converted as Java's `String.valueOf`. The OTLP half of 26.6 comes in step 5.

Sample: `Ogooreck.Sample.Api` gets an `ActivitySource` with the spans of the criteria (`POST /carts/{id}/orders` > `publish OrderPlaced`, attributes `cart.id`, `cart.closed`, `cart.total`, message ids and correlation attributes), counters `orders.placed`, `carts.expired` (background worker), an observable counter `carts.open`, and a handler span that finishes 500 ms after the response. The spec's table puts in-process traces and metrics in the API sample.

Context
- `src/dotnet/Ogooreck/API/` (steps 1 to 3): `ApiSpecification<TProgram>` stores configuration; the step runner keeps the test's trace id set; the baseline is taken before the When step.
- Java reference: `ApiE2ESpecification.java` (`expectTrace`, `expectCorrelatedTrace`, `expectMetric`, `traceIds`, the meter baseline with `getOrDefault(id, 0.0)`) and `SpanTree.java` (`mismatchOf`, `isBelow`).
- `src/dotnet/Ogooreck.Sample.Api/Program.cs`.

Similar code
- `Ogooreck.Sample.Api.Tests/JsonSettingsTests.cs` (the Json-override halves deleted in step 1 are rebuilt here).
- Step 2a tests for waiting expectations and deadline assertions.

Blocked by
Steps 2a, 2b

Existing tests this step breaks, and how it updates them
- `Ogooreck.Sample.Api.Tests/JsonSettingsTests.cs`: the two halves deleted in step 1 return as the 6.4 test (`Json(options)` replaces the per-assertion option argument).
- No other existing test breaks. Adding the configuration lock must not break step 1 and step 2a tests: they configure only before the first step (a test that calls a configuration call late is a bug and is fixed in the test).

Acceptance criteria
- 6. Configuration fully replaces a default and stays fixed after the first step, so tests that share a specification don't affect each other.
  - 6.1 GIVEN a specification configured with a 2-second deadline AND an expectation that never holds WHEN Then runs THEN it fails after about 2 seconds, not 30.
  - 6.2 GIVEN `.Telemetry(TelemetrySources.Off)` on an in-process host WHEN `ExpectTrace(...)` runs THEN it fails with a message saying telemetry is off.
  - 6.3 GIVEN a specification that has already run a step WHEN a test calls `.Deadline(...)` or any other configuration call on it THEN the call throws, saying that configuration must happen before the first step.
  - 6.4 GIVEN `.Json(options)` with `PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower` WHEN the response is `{"order_status":"Placed"}` THEN `Body(new { OrderStatus = "Placed" })` matches. Without `.Json(...)`, on a host whose app uses the web defaults, the same check fails, because the expected value serializes as `orderStatus`.
  - 6.5 GIVEN a custom `ITelemetrySource` that adds one `RecordedSpan` named `custom` with the When step's trace id WHEN `ExpectTrace(Span("custom"))` runs THEN it passes.
- 26. A user checks the trace of the test, so they test what the app did inside.
  - 26.1 GIVEN an in-process app whose When request creates spans `POST /carts/{id}/orders` > `publish OrderPlaced` WHEN `ExpectTrace(Span("POST /carts/{id}/orders", Span("publish OrderPlaced")))` runs THEN it passes.
  - 26.2 GIVEN the same WHEN `ExpectTrace(Span("POST /carts/{id}/orders", Span("publish OrderCancelled")))` runs THEN it fails at the deadline, describing the test's finished spans.
  - 26.3 GIVEN a span with attribute `cart.id = 42` WHEN `ExpectTrace(Span("POST /carts/{id}/orders", new Dictionary<string, string> { ["cart.id"] = "42" }))` runs THEN it passes, and with `"43"` it fails.
  - 26.4 GIVEN a parallel test whose trace contains `POST /carts/{id}/orders` > `publish OrderPlaced` AND this test's When request returns `409` without publishing WHEN this test runs `ExpectTrace(Span("POST /carts/{id}/orders", Span("publish OrderPlaced")))` on a specification configured with a 2-second deadline THEN it fails, because the other test's spans don't count.
  - 26.5 GIVEN a handler span that finishes 500 ms after the response WHEN `ExpectTrace(...)` expects it THEN it passes once the span finishes, with no fixed wait.
  - 26.6 GIVEN a span with the bool attribute `cart.closed = true` and the double attribute `cart.total = 1.5`, in a test running under culture `pl-PL` WHEN `ExpectTrace(Span("POST /carts/{id}/orders", new Dictionary<string, string> { ["cart.closed"] = "true", ["cart.total"] = "1.5" }))` runs in-process, and in the Aspire sample over OTLP, THEN it passes in both.
- 27. A user checks correlation across the test's traces, so they test that correlation ids flow through handlers.
  - 27.1 GIVEN all spans of the test's traces carry `messaging.message.correlation_id = c1` WHEN `ExpectCorrelatedTrace()` runs after catch-up THEN it passes.
  - 27.2 GIVEN one span carries `c2` while the others in its trace carry `c1` WHEN `ExpectCorrelatedTrace()` runs THEN it fails, naming the span and both ids.
  - 27.3 GIVEN a span with `messaging.message.causation_id = m1` that is not below the span with `messaging.message.id = m1` WHEN `ExpectCorrelatedTrace()` runs THEN it fails, naming both spans.
  - 27.4 GIVEN no span carries `messaging.message.correlation_id` WHEN `ExpectCorrelatedTrace()` runs THEN it fails, saying so.
  - 27.5 GIVEN a Given step's span with `messaging.message.id = m1` AND a span in the When step's trace with `messaging.message.causation_id = m1` WHEN `ExpectCorrelatedTrace()` runs THEN it fails, saying that the span is not below the span that handled `m1`, as in Java, where causation is checked within a trace.
- 28. A user checks a metric's increase, so they test what the app reports.
  - 28.1 GIVEN an in-process app that increments counter `orders.placed` with tag `channel=web` once per order WHEN the When step places one order AND `ExpectMetric("orders.placed", { ["channel"] = "web" }, 1)` runs THEN it passes.
  - 28.2 GIVEN a parallel test placing an order at the same time WHEN the same expectation runs in-process THEN it still passes, because only measurements inside this test's traces count.
  - 28.3 GIVEN the When step places two orders WHEN `ExpectMetric(..., 1)` runs THEN it fails at once with increase `2`.
  - 28.4 GIVEN an observable counter `carts.open` that reports `5` at the baseline and `7` when checked WHEN `ExpectMetric("carts.open", {}, 2)` runs THEN it passes, and `ExpectMetric("carts.open", {}, 7)` fails, because observable values are cumulative.
  - 28.5 GIVEN a counter series tagged `cart.id = cartId` that has no measurement before the When step AND the When step records `1` on it WHEN `ExpectMetric("orders.placed", { ["cart.id"] = cartId.ToString() }, 1)` runs THEN it passes, because a missing baseline counts as 0.
  - 28.6 GIVEN the in-process histogram `http.server.request.duration` WHEN the When step sends one request to `/carts/{id}/orders` that takes 34 ms THEN `ExpectMetric("http.server.request.duration", { ["http.route"] = "/carts/{id}/orders" }, 1)` passes, and `ExpectMetric(..., 0.034)` fails, because histogram measurements are counted, not summed, like Java's `timer.count()`.
  - 28.7 GIVEN a background worker with no current `Activity` that increments `carts.expired` with tag `cart.id = cartId` once during the When step WHEN `ExpectMetric("carts.expired", { ["cart.id"] = cartId.ToString() }, 1)` runs THEN it passes, because measurements without a trace id count by tags.

Tests
- 6.1: expectationThatNeverHolds_failsAtTheConfiguredDeadlineNotTheDefault - asserts `.Deadline(2 s)`; `ExpectTrace(Span("never"))`; failure after at least 2 s and under 10 s
- 6.2: expectTrace_withTelemetryOff_failsSayingTelemetryIsOff - asserts `.Telemetry(TelemetrySources.Off)` on the in-process host; message says telemetry is off; failure is immediate (under 1 s)
- 6.3: configurationCallAfterTheFirstStep_throwsSayingConfigurationMustHappenBeforeTheFirstStep - asserts `InvalidOperationException` for `Deadline`, `Host`, `Recording`, `CatchUp`, `Publishing`, `Telemetry` and `Json` (theory over the calls)
- 6.4: json_withSnakeCase_matchesSnakeCaseResponse_andWithoutItFails - asserts `.Json(options)` with `SnakeCaseLower` and response `{"order_status":"Placed"}`: `Body(new { OrderStatus = "Placed" })` passes; same check on a host with web defaults and no `.Json` fails because the expected value serializes as `orderStatus` (rebuilds the two override tests removed in step 1)
- 6.5: customTelemetrySource_addingASpanWithTheWhenTraceId_satisfiesExpectTrace - asserts source's `Attach` stores the recorders; during the When step the test adds `RecordedSpan(whenTraceId, ..., "custom", {})`; `ExpectTrace(Span("custom"))` passes
- 26.1: expectTrace_matchesTheSpanTreeOfTheWhenRequest - asserts `POST /carts/{id}/orders` > `publish OrderPlaced`
- 26.2: expectTrace_withAWrongChild_failsAtTheDeadlineDescribingTheFinishedSpans - asserts `publish OrderCancelled` expected; `Deadline(2 s)`; message lists the test's finished spans by name
- 26.3: expectTrace_matchesSpanAttributes - asserts `cart.id = 42` passes, `"43"` fails
- 26.4: expectTrace_ignoresTheSpansOfAParallelTest - asserts two tests in parallel; the other's trace has `publish OrderPlaced`; this test's request returns 409 with no publish span; with `Deadline(2 s)` it fails
- 26.5: expectTrace_waitsForASpanThatFinishesAfterTheResponse - asserts handler span ends 500 ms after the response; passes; elapsed under 5 s (no fixed wait is observable: the same test with the span ending immediately finishes under 1 s)
- 26.6: attributeValues_areStringsLikeJavaStringValueOf_underPlPlCulture - asserts bool `cart.closed = true`, double `cart.total = 1.5`, current culture `pl-PL` (set and restored in the test); expected `"true"` and `"1.5"` pass. In-process half only; the Aspire half is tested in step 5 as an extra test of that step
- 27.1: expectCorrelatedTrace_passesWhenAllSpansShareTheCorrelationId - asserts all spans `messaging.message.correlation_id = c1`
- 27.2: expectCorrelatedTrace_failsNamingTheSpanAndBothIds - asserts one span `c2`, others `c1`; message contains span name, `c1`, `c2`
- 27.3: expectCorrelatedTrace_failsWhenCausationSpanIsNotBelowTheHandlingSpan - asserts span with `causation_id = m1` not below the span with `message.id = m1`; message names both spans
- 27.4: expectCorrelatedTrace_failsWhenNoSpanHasACorrelationId - asserts message says no span carries `messaging.message.correlation_id`
- 27.5: expectCorrelatedTrace_failsWhenTheHandlingSpanIsInAGivenStepsTrace - asserts Given span `message.id = m1`, When-trace span `causation_id = m1`; message says the span is not below the span that handled `m1`
- 28.1: expectMetric_countsAnOrderOfTheWhenStep - asserts counter `orders.placed`, tag `channel=web`, one order; `ExpectMetric("orders.placed", {channel=web}, 1)` passes
- 28.2: expectMetric_ignoresAParallelTestsMeasurement - asserts a second test places an order while the first's When step runs (barrier); expectation `1` passes in both
- 28.3: expectMetric_failsAtOnceWhenTheIncreaseExceedsTheExpectation - asserts two orders, expected 1; failure under 2 s with the default 30 s deadline; message contains increase `2`
- 28.4: expectMetric_onAnObservableCounter_usesCumulativeValues - asserts `carts.open` reports 5 at baseline and 7 at check; expected 2 passes; expected 7 fails
- 28.5: expectMetric_countsAMissingBaselineAsZero - asserts series tagged `cart.id` has no measurement before the When step; When records 1; expected 1 passes
- 28.6: expectMetric_countsHistogramMeasurements_notTheirSum - asserts `http.server.request.duration`, route `/carts/{id}/orders`, one 34 ms request: expected `1` passes, expected `0.034` fails
- 28.7: expectMetric_countsAMeasurementWithoutATraceIdByTags - asserts background worker, no current `Activity`, increments `carts.expired` tagged `cart.id`; expected 1 passes
- extra: inProcessSource_isNewForEachSpecification_andStopsRecordingOnDispose - covers two specifications each see only their own listener; after `Dispose()` the `ActivityListener` and `MeterListener` are disposed (a new `Activity` is not sampled by that source)
- extra: configurationCallsReturnTheSameInstance_andFSharpStyleChainsCompile - covers `spec => spec.Deadline(...).Json(...)` in the `configure` constructor compiles and applies both
- extra: telemetryCollect_isCalledOnceAtTheBaseline_andBeforeEachExpectMetricCheck - covers a custom source counts `Collect` calls: 1 before the When step plus 1 per `ExpectMetric`
- extra: expectMetric_withNoMatchingTags_failsAtTheDeadlineWithIncreaseZero - covers `Deadline(2 s)`
- extra: expectMetric_sumsTheIncreaseAcrossSeriesWhoseTagsIncludeTheGivenTags - covers two series `channel=web,region=a` and `channel=web,region=b`, tags `channel=web`, increase 2
- extra: expectTrace_matchesSiblingsInOrderAndRejectsMissingChildren - covers follow `SpanTree` rules; for each rule in `mismatchOf` one case
- extra: expectCorrelatedTrace_runsOnceAfterCatchUp - covers a recorder with catch-up whose last message arrives in the final round; the check sees its span
```

### Part 3b. Re-send the When request until a read model catches up: Eventually

```text
Use the `implement-step` skill.

What to build
A user wraps the When request in `Eventually(...)` when the app has no idle signal. The request is re-sent back to back while any `ExpectResponse` or `ExpectError` expectation fails, until all pass or the deadline is reached. Each attempt has a fresh trace id. Every attempt's trace id joins the test's set for spans and measurements; for messages only the final attempt's trace id counts. After the response expectations pass, catch-up runs, and the other expectations run once against the final attempt. `Eventually` as a Given step fails the test.

Sample: the Wolverine sample gains an order read model that shows `Placed` only from the 3rd request, and an order view endpoint that publishes `OrderViewed(orderId)` on every request. The metric check of 13.6 uses the same app's `GET /orders/{id}` route (ASP.NET Core's `http.server.request.duration`).

Context
- `src/dotnet/Ogooreck/API/` (steps 1 to 4): the step runner and `Then`. `Eventually` wraps a `Request` step as the When step.
- `Ogooreck.Sample.Wolverine` from steps 2a, 2b.
- Java: no counterpart (`Eventually` has none).

Similar code
- Step 2a tests for waiting expectations (deadline assertions) and step 3a tests for `ExpectMetric`.

Blocked by
Steps 2a, 3a

Existing tests this step breaks, and how it updates them
- No existing test breaks.

Acceptance criteria
- 13. A user re-sends a When request until a read model catches up, so apps without an idle signal stay testable.
  - 13.1 GIVEN `When(Eventually(GetOrder(orderId)))` AND the read model shows `"Placed"` only from the 3rd request WHEN `Then(ExpectResponse(OK, Body(new { status = "Placed" })))` runs THEN the request is sent 3 times, and the test passes.
  - 13.2 GIVEN a specification configured with a 2-second deadline AND a read model that never shows `"Placed"` WHEN the same test runs THEN it fails after about 2 seconds, with the last response's status and body.
  - 13.3 GIVEN `When(GetOrder(orderId))` without `Eventually` AND the read model shows `"Placed"` only from the 3rd request WHEN the same expectation runs THEN the request is sent once, and the test fails.
  - 13.4 GIVEN `Eventually(...)` used as a Given step WHEN the test runs THEN it fails, saying that `Eventually` is allowed only as the When step.
  - 13.5 GIVEN `When(Eventually(GetOrder(orderId)))` where each request publishes `OrderViewed(orderId)` AND the read model shows `"Placed"` from the 3rd request WHEN `Then(ExpectResponse(OK, Body(new { status = "Placed" })), ExpectPublishedIn(AppApi.PublishedMessages, new OrderViewed(orderId)))` runs on a recorder with catch-up THEN it passes. Only the final attempt's messages count, and `ExpectPublished` runs once, after the response expectations pass.
  - 13.6 GIVEN `When(Eventually(GetOrder(orderId)))` that needs 3 attempts, each creating span `GET /orders/{id}` WHEN `ExpectMetric("http.server.request.duration", { ["http.route"] = "/orders/{id}" }, 3)` runs in-process THEN it passes, because every attempt's trace id belongs to the test.

Tests
- 13.1: eventually_resendsUntilTheReadModelShowsPlaced - asserts the sample counts requests; `Then(ExpectResponse(OK, Body(new { status = "Placed" })))`; request count 3; the test passes
- 13.2: eventually_atTheDeadline_failsWithTheLastResponseStatusAndBody - asserts `Deadline(2 s)`; the read model never shows `Placed`; failure after at least 2 s and under 10 s; message contains the last status and body
- 13.3: noEventually_sendsOnceAndFails - asserts `When(GetOrder(orderId))`; request count 1; the test fails
- 13.4: eventuallyAsAGivenStep_failsSayingItIsAllowedOnlyAsTheWhenStep - asserts message says `Eventually` is allowed only as the When step
- 13.5: eventually_countsOnlyTheFinalAttemptsMessages_andChecksThemOnce - asserts 3 attempts each publishing `OrderViewed(orderId)`; recorder with catch-up; `ExpectPublished(..., new OrderViewed(orderId))` passes (exact, so earlier attempts' messages must not count); the expectation delegate ran once (counter)
- 13.6: eventually_countsEveryAttemptsMeasurements - asserts 3 attempts, route `GET /orders/{id}`; `ExpectMetric("http.server.request.duration", {["http.route"] = "/orders/{id}"}, 3)` passes in-process
- extra: eventually_withAnExpectErrorExpectation_resendsUntilTheErrorStatusAppears - covers the app answers 200 twice, then 409
- extra: eventually_withNoResponseExpectations_sendsOnce - covers only `ExpectPublished`; request count 1
- extra: eventually_requestsEachCarryTheirOwnTraceparent - covers three attempts, three different trace ids in the sample's echo
- extra: eventually_cancelledThroughTheToken_endsWithOperationCanceledException - covers `Then([...], ct)`, token cancelled after 1 s while the read model never shows `Placed`
- extra: eventually_spansOfEveryAttemptCount_forExpectTrace - covers `ExpectTrace` on a span created by the first attempt only passes
```

---

## Step 6, part 0. Enforce a documented public API and full coverage (moved to run first in step 6)

```text
Use the `implement-step` skill.

What to build
The build enforces strictland's two `check` rules (qa 55): every public and protected member of the `Ogooreck` library has an XML doc comment, and the library has 100% line and branch coverage from all test projects. All code from steps 1-5 gets documented and covered.

Context
- `src/dotnet/Ogooreck/Ogooreck.csproj` already sets `GenerateDocumentationFile`, and `src/dotnet/Directory.Build.props` sets `TreatWarningsAsErrors`. Six files suppress `CS1591` with `#pragma warning disable CS1591`; remove every suppression.
- `src/dotnet/Ogooreck.Build/Program.cs` runs the test targets; add coverage collection (coverlet, exact pinned version at least two weeks old) across all test projects, merged, with a 100% line and branch threshold on the `Ogooreck` assembly only, failing `./build.sh test` and so `dotnet-ci.yml`.
- AGENTS.md and CONTRIBUTING.md: add a "Tests, coverage, and documentation" section adapted from strictland's (`/home/oskar/repos/strictland/AGENTS.md` and `CONTRIBUTING.md`, "Tests, coverage, and documentation").

Similar code
- Strictland: `/home/oskar/repos/strictland/src/jvm/build.gradle.kts` (`javadoc` with `-Xwerror`, `jacocoTestCoverageVerification` at 1.0 for LINE and BRANCH) and its documented public API.

Blocked by
Steps 4 and 5.

Acceptance criteria
- From spec "Testing Decisions": documented public API; 100% line and branch coverage.

Tests
- extra: buildFailsOnAnUndocumentedPublicMember - covers adding a public member without a doc comment fails `dotnet build` (checked once by hand, then reverted)
- extra: buildFailsBelowFullCoverage - covers removing a test that covers a branch makes `./build.sh test` fail on the coverage threshold (checked once by hand, then reverted)
- extra: unreachableBranchesRemoved - covers every branch the coverage report shows as unreachable is removed, not covered with an artificial test
```

---

## Step 4. Kafka, Marten and causation

Runs as one step with one review and one fix round: part 4a, part 4b, part 4c, in this order.

### Part 4a. Check messages without a catch-up function: Kafka sample, subsequence matching and cancellation

```text
Use the `implement-step` skill.

What to build
On a recorder without catch-up (a Kafka test-side consumer), `ExpectPublished` passes as soon as the expected messages appear as an in-order subsequence of the When step's messages, and fails at the deadline listing the When step's messages it saw. Messages of other parallel tests on the same topic do not count. `Then(expectations, ct)` ends with `OperationCanceledException` when `ct` is cancelled, also while waiting on a message that never arrives.

New sample: Kafka through Testcontainers (`Ogooreck.Sample.Kafka`, `.Tests`), added to `Ogooreck.slnx` and `Ogooreck.Build/Program.cs`. The test-side consumer has its own consumer group, adds every `ConsumeResult` to a `Recorded<ConsumeResult<string, string>>`, and `read` takes the `traceparent` from the Kafka message headers. The app publishes to the topic with its trace context in the headers. Tests need Docker.

Context
- `src/dotnet/Ogooreck/API/` (steps 1, 2a): `ExpectPublished` has the catch-up path; add the subsequence path for a recorder without catch-up.
- `src/dotnet/Ogooreck.Build/Program.cs`, `Ogooreck.slnx`, `.github/workflows/dotnet-ci.yml` (Docker is present on `ubuntu-latest`).

Similar code
- Step 2a `Ogooreck.Sample.Wolverine.Tests` for the layout of `AppApi` and the failing-scenario assertions.
- Java `ApiE2ESpecification.java` for `Transcript.numbered`.

Blocked by
Step 2a

Existing tests this step breaks, and how it updates them
- No existing test breaks.

Acceptance criteria
- 18. A user checks messages without a catch-up function, so broker-side recording works without an idle signal.
  - 18.1 GIVEN a Kafka recorder without catch-up AND the When step publishes `OrderPlaced(cartId)` then `CartClosed(cartId)` WHEN `ExpectPublishedIn(kafka, new OrderPlaced(cartId))` runs THEN it passes as soon as `OrderPlaced` arrives.
  - 18.2 GIVEN the same WHEN `ExpectPublishedIn(kafka, new CartClosed(cartId), new OrderPlaced(cartId))` runs THEN it fails at the deadline, because the expected messages didn't arrive in that order.
  - 18.3 GIVEN a specification configured with a 2-second deadline AND an app that never publishes `OrderPaid` WHEN `ExpectPublishedIn(kafka, new OrderPaid(cartId))` runs THEN it fails after about 2 seconds, listing the messages it did see for the When step.
  - 18.4 GIVEN two tests running in parallel on one Kafka topic, where test A's When step publishes `StockChecked("socks")` and test B's When step publishes nothing WHEN test B runs `ExpectPublishedIn(kafka, new StockChecked("socks"))` on a specification configured with a 2-second deadline THEN it fails, because test A's message doesn't count.
- 32. A user cancels a running test, so a cancelled run doesn't hang until the deadline.
  - 32.1 GIVEN `Then([ExpectPublishedIn(kafka, new OrderPaid(cartId))], ct)` waiting on a message that never arrives WHEN `ct` is cancelled after 1 second THEN `Then` ends with `OperationCanceledException` after about 1 second.

Tests
- 18.1: expectPublished_withoutCatchUp_passesAsSoonAsTheExpectedMessageArrives - asserts When publishes `OrderPlaced` then `CartClosed`; expected `OrderPlaced`; passes (elapsed time lower than the time the app takes to publish `CartClosed` when the sample delays it by 3 s)
- 18.2: expectPublished_withoutCatchUp_failsAtTheDeadline_whenTheOrderDiffers - asserts expected `CartClosed`, `OrderPlaced`; `Deadline(2 s)` on that instance; fails after at least 2 s
- 18.3: expectPublished_withoutCatchUp_failsAtTheDeadlineListingTheSeenMessages - asserts `Deadline(2 s)`; the app never publishes `OrderPaid`; failure after about 2 s (at least 2 s, under 10 s) with the messages seen for the When step listed
- 18.4: expectPublished_withoutCatchUp_ignoresAParallelTestsMessageOnTheSameTopic - asserts test A's When publishes `StockChecked("socks")` while test B's When publishes nothing; test B with `Deadline(2 s)` fails
- 32.1: then_cancelledAfterOneSecond_endsWithOperationCanceledExceptionAfterAboutOneSecond - asserts `Then([ExpectPublishedIn(kafka, new OrderPaid(cartId))], ct)` with a token cancelled after 1 s; exception type `OperationCanceledException`; elapsed at least 1 s and under 5 s
- extra: expectPublished_withoutCatchUp_matchesASubsequenceWithGapsBetweenMessages - covers published A, B, C; expected A, C passes
- extra: expectPublished_withoutCatchUp_noMessagesPublished_failsAtTheDeadlineListingNone - covers `Deadline(2 s)`
- extra: expectPublished_withoutCatchUp_ignoresMessagesWithAnotherTraceId - covers a message with a foreign `traceparent` header does not satisfy the expectation
```

### Part 4b. Check appended events: Marten sample, ExpectAppended and ExpectNothingAppended

```text
Use the `implement-step` skill.

What to build
A user records the events a Marten session appends through a session listener and checks what the When step appended with `ExpectAppended(...)` and `ExpectNothingAppended(...)`. They behave like the published checks, with failure messages that say events. Projection catch-up uses `WaitForNonStaleProjectionDataAsync`.

New sample: Marten with PostgreSQL through Testcontainers (`Ogooreck.Sample.Marten`, `.Tests`), added to `Ogooreck.slnx` and `Ogooreck.Build/Program.cs`. Events carry the test's trace context in Marten's headers or metadata (the sample chooses how it writes the `traceparent` and `read` reads it back).

Context
- `src/dotnet/Ogooreck/API/` (step 2a): the shared message-expectation code; `ExpectAppended` reuses it with its own wording.
- `.github/workflows/dotnet-ci.yml`: Docker is present on `ubuntu-latest`.

Similar code
- Step 2a and 6 sample test projects.

Blocked by
Step 2a

Existing tests this step breaks, and how it updates them
- No existing test breaks.

Acceptance criteria
- 19. A user checks appended events, so they test what the app stored.
  - 19.1 GIVEN a Marten recorder with a catch-up function AND the When step appends `OrderPlaced(cartId)` WHEN `ExpectAppendedIn(AppApi.AppendedEvents, new OrderPlaced(cartId))` runs THEN it passes.
  - 19.2 GIVEN the When step appends nothing WHEN `ExpectNothingAppendedIn(AppApi.AppendedEvents)` runs THEN it passes.

Tests
- 19.1: expectAppended_passesWhenTheWhenStepAppendedTheEvent - asserts Marten recorder with `catchUp = WaitForNonStaleProjectionDataAsync`; When appends `OrderPlaced(cartId)`
- 19.2: expectNothingAppended_passesWhenTheWhenStepAppendedNothing - asserts When is a request that appends nothing
- extra: expectAppended_withADifferentEvent_failsListingAppendedEventsNumbered - covers message uses the wording `appended` and lists numbered events
- extra: expectNothingAppended_whenAnEventWasAppended_failsListingIt - covers the error path named in the test name
- extra: expectNothingAppended_withoutCatchUp_failsAtOnceSayingItNeedsACatchUpFunction - covers the error path named in the test name
- extra: expectAppended_ignoresEventsAppendedByAGivenStep - covers exact comparison on a recorder with catch-up
- extra: projectionCatchUp_makesAReadOfAnAsyncProjectionSeeTheGivenStepsEvent - covers no `Eventually`, request count 1
```

### Part 4c. Check causation across messages: ExpectCausation with Sent, Published and Appended

```text
Use the `implement-step` skill.

What to build
A user checks correlation and causation of messages with `ExpectCausation(Published(...), Sent(...), Appended(...))` trees: every message carries its root's correlation id, each child's causation id is its parent's message id. With catch-up on every recorder in the tree it checks once after catch-up; otherwise it waits until the tree is found or the deadline is reached. It fails if `read` returns no ids. `Observed` already has the id fields (step 2a).

Tests for 20.1 to 20.4 run in the Wolverine sample (the app publishes `OrderPlaced` with id `m1`, correlation `c1`, causing `ShipOrder`); 20.5 runs in the Kafka sample, where `ShipOrder` arrives 500 ms after `OrderPlaced`.

Context
- `src/dotnet/Ogooreck/API/` (steps 2a, 4a, 4b): message expectations and recorder mode.
- Java `ApiE2ESpecification.java` `expectCausation` (trees, correlation id of the root, `causation == parent id`).

Similar code
- Step 2a and step 4a expectations.

Blocked by
Steps 2a, 4a, 4b

Existing tests this step breaks, and how it updates them
- No existing test breaks.

Acceptance criteria
- 20. A user checks what caused what, so they test correlation and causation across modules.
  - 20.1 GIVEN the When step publishes `OrderPlaced` (id `m1`, correlation `c1`), which causes `ShipOrder` (causation `m1`, correlation `c1`) WHEN `ExpectCausation(PublishedIn(AppApi.PublishedMessages, new OrderPlaced(cartId), SentIn(AppApi.PublishedMessages, new ShipOrder(cartId))))` runs THEN it passes.
  - 20.2 GIVEN `ShipOrder` has causation `m9` WHEN the same expectation runs THEN it fails, naming the expected causation `m1` and the actual `m9`.
  - 20.3 GIVEN `ShipOrder` has correlation `c2` WHEN the same expectation runs THEN it fails, naming the expected correlation `c1`.
  - 20.4 GIVEN a recorder whose `read` doesn't return message ids WHEN `ExpectCausation(...)` runs THEN it fails, saying that it needs message, correlation and causation ids from `read`.
  - 20.5 GIVEN a Kafka recorder without catch-up AND `ShipOrder` arriving 500 ms after `OrderPlaced` WHEN the expectation from 20.1 runs on that recorder THEN it passes once `ShipOrder` arrives.

Tests
- 20.1: expectCausation_passesForAMessageCausedByAnotherWithTheSameCorrelation - asserts `OrderPlaced` (id m1, correlation c1) causes `ShipOrder` (causation m1, correlation c1)
- 20.2: expectCausation_failsNamingExpectedAndActualCausation - asserts `ShipOrder` causation `m9`; message names `m1` and `m9`
- 20.3: expectCausation_failsNamingExpectedCorrelation - asserts `ShipOrder` correlation `c2`; message names `c1`
- 20.4: expectCausation_whenReadReturnsNoIds_failsSayingItNeedsIds - asserts message says it needs message, correlation and causation ids from `read`
- 20.5: expectCausation_withoutCatchUp_passesOnceTheCausedMessageArrives - asserts Kafka recorder without catch-up, `ShipOrder` delayed 500 ms; passes; elapsed at least 500 ms
- extra: expectCausation_withoutCatchUp_failsAtTheDeadlineWhenTheCausedMessageNeverArrives - covers `Deadline(2 s)`
- extra: expectCausation_acrossTwoRecorders_waitsWhenOneHasNoCatchUp - covers published recorder with catch-up plus Kafka recorder without
- extra: expectCausation_withAppendedNodeOnAMartenRecorder_passes - covers in the Marten sample; event metadata carries the ids
- extra: expectCausation_withAThreeLevelTree_checksEachParentChildPair - covers grandchild with the wrong causation fails naming it
```

---

## Step 5. Out-of-process apps: non-generic ApiSpecification, OtlpReceiver and Aspire


```text
Use the `implement-step` skill.

What to build
A user tests an app hosted outside the test process (Aspire or any base address) with the non-generic `ApiSpecification`: the address function runs once on the first step, Ogooreck builds its own client with a handler that sets the step's `traceparent`, optionally over a user-supplied `HttpMessageHandler`. A subclass passes the address to the base constructor or calls `.Host(async () => ...)`; without any address the first step fails saying a host is required. `OtlpReceiver.Start()` hosts an OTLP/HTTP protobuf receiver (Kestrel on a random `localhost` port, `POST /v1/traces` and `/v1/metrics`) with a hand-written decoder on `Google.Protobuf`'s `CodedInputStream` for the fields listed in the spec; it feeds `ExpectTrace` and `ExpectMetric` (sum, gauge and histogram counts, delta and cumulative by temporality, attribute values as `String.valueOf`). `WaitForMetricsExport = true` makes `Collect` wait for the next metrics export, bounded by the deadline. Disposal stops the host, the telemetry source and the receiver.

Scope: non-generic `ApiSpecification` (the three subclass constructors, both `For` overloads, both `Host` overloads, `IDisposable`/`IAsyncDisposable`, "host is required" failure), `OtlpReceiver`, `OtlpReceiverOptions`, the decoder, the dependency on `Google.Protobuf` in `Ogooreck.csproj`, disposal of host, listeners and receiver. New sample: Aspire (`Ogooreck.Sample.Aspire.AppHost`, an API project and `.Tests` using the Aspire testing host with Kafka), added to `Ogooreck.slnx` and `Ogooreck.Build/Program.cs`. The test creates `OtlpReceiver` first and passes `Endpoint` to the app; the API resource gets `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf` and short `OTEL_BSP_SCHEDULE_DELAY` and `OTEL_METRIC_EXPORT_INTERVAL`. The sample also uses a broker-side Kafka recorder, an `ExpectTrace`, a tag-filtered `ExpectMetric` and `WaitForMetricsExport`. Tests need Docker.

Context
- `src/dotnet/Ogooreck/API/` (steps 1 to 5): the in-process specification type; the shared internal implementation is used by both public types (the spec says the two types do not derive from each other).
- `src/dotnet/Ogooreck/Ogooreck.csproj`: add the `Google.Protobuf` package reference and, for Kestrel, the framework reference the receiver needs.
- `src/dotnet/Ogooreck.Build/Program.cs`, `Ogooreck.slnx`.

Similar code
- Step 4a Kafka sample for the broker-side recorder.
- Step 3a for `ITelemetrySource` and the in-process source, whose tests become the expectations of the OTLP source.
- Java: no OTLP counterpart (it uses the OpenTelemetry in-memory exporter).

Blocked by
Steps 3a, 4a

Existing tests this step breaks, and how it updates them
- No existing test breaks. `Ogooreck.csproj` gains `Google.Protobuf`, so the package restore of every project changes; `./build.sh test` is the check.

Acceptance criteria
- 4. A user tests an app hosted out of process, so Aspire apps can be tested.
  - 4.1 GIVEN `ApiSpecification.For(async () => app.GetEndpoint("api"))` WHEN the first step runs THEN the address function runs once, and requests go to that address.
  - 4.2 GIVEN that setup WHEN any request step runs THEN the request carries a `traceparent` header whose trace id is unique to that step.
  - 4.3 GIVEN `ApiSpecification.For(address, handlerFactory)` WHEN a request step runs THEN the request goes through the handler from `handlerFactory`, and still carries the step's `traceparent`.
  - 4.4 GIVEN a class `AspireApi : ApiSpecification` with a static `OtlpReceiver Otlp = OtlpReceiver.Start()` that passes to the base constructor an address function, which starts the app with `Otlp.Endpoint`, and `Telemetry(Otlp)` WHEN a test uses it as a fixture THEN it behaves the same as 4.1, and the app exports to `Otlp`.
  - 4.5 GIVEN a class `AspireApi : ApiSpecification` whose configuration calls `.Host(async () => app.GetEndpoint("api"))` WHEN the first step runs THEN it behaves the same as 4.1.
  - 4.6 GIVEN a class `AspireApi : ApiSpecification` whose configuration never calls `.Host(...)` and that passes no address WHEN the first step runs THEN it fails, saying that a host is required.
- 5. A user disposes the specification, so nothing keeps running after the tests.
  - 5.1 GIVEN a specification that started a host, listeners and an OTLP receiver WHEN it is disposed, synchronously or asynchronously, THEN all three are stopped.
- 29. A user checks the traces and metrics of an Aspire app, so out-of-process apps get the same checks.
  - 29.1 GIVEN an Aspire app whose API resource exports OTLP/HTTP protobuf to `OtlpReceiver.Endpoint` AND `.Telemetry(OtlpReceiver.Start())` WHEN the When request creates span `POST /carts/{id}/orders` THEN `ExpectTrace(Span("POST /carts/{id}/orders"))` passes once the batch arrives.
  - 29.2 GIVEN the same AND counter `orders.placed` exported as a cumulative sum WHEN one order is placed and `ExpectMetric("orders.placed", { ["cart.id"] = cartId.ToString() }, 1)` runs THEN it passes once the next export arrives.
  - 29.3 GIVEN no export arrives before the deadline WHEN `ExpectTrace(...)` runs THEN it fails at the deadline, saying that no matching spans were received.
  - 29.4 GIVEN the same Aspire app WHEN the When step sends one request THEN `ExpectMetric("http.server.request.duration", { ["http.route"] = "/carts/{id}/orders" }, 1)` passes once the export arrives, because OTLP histogram points contribute their count.
  - 29.5 GIVEN `OtlpReceiver.Start(new OtlpReceiverOptions { WaitForMetricsExport = true })` AND a Given step that adds a product to `cartId` AND a When step that adds another, both on counter `cart.products.added` tagged `cart.id = cartId` WHEN `ExpectMetric("cart.products.added", { ["cart.id"] = cartId.ToString() }, 1)` runs THEN it passes, because the baseline waited for the export that carries the Given step's increment.

Tests
- 4.1: addressFunction_runsOnceOnTheFirstStep_andRequestsGoToThatAddress - asserts counter in the address function equals 1 after two steps; the app echoes its port
- 4.2: everyRequestToAnOutOfProcessHost_carriesAUniqueTraceparentPerStep - asserts the API echoes `traceparent`; two steps give two different trace ids
- 4.3: handlerFactory_isUsedForRequests_andTheTraceparentIsStillSet - asserts a counting `DelegatingHandler` returned by the factory sees the request; the echoed header is a valid `traceparent`
- 4.4: aspireApiSubclassWithAStaticOtlpReceiver_behavesLikeFourPointOne_andTheAppExportsToIt - asserts `AspireApi : ApiSpecification` with `static OtlpReceiver Otlp = OtlpReceiver.Start()`, address function starting the app with `Otlp.Endpoint`, `Telemetry(Otlp)`; the receiver receives a batch from the app (observed through `ExpectTrace` passing)
- 4.5: subclassCallingHost_behavesLikeFourPointOne - asserts `.Host(async () => app.GetEndpoint("api"))` in the configuration
- 4.6: subclassWithoutAHost_failsOnTheFirstStepSayingAHostIsRequired - asserts message says a host is required
- 5.1: dispose_stopsTheHostTheListenersAndTheOtlpReceiver - asserts synchronous and asynchronous dispose in two cases: afterwards a request to the host address fails to connect, an `ActivityListener` count taken from the in-process source stops growing for a new `Activity`, and a POST to the receiver `Endpoint` is refused (one test for `Dispose()`, one for `DisposeAsync()`)
- 29.1: aspireApp_exportsSpansOverOtlp_andExpectTracePasses - asserts API resource exports OTLP/HTTP protobuf to `OtlpReceiver.Endpoint`; `.Telemetry(OtlpReceiver.Start())`; `ExpectTrace(Span("POST /carts/{id}/orders"))` passes once the batch arrives
- 29.2: aspireApp_exportsACumulativeCounter_andExpectMetricPassesAfterTheNextExport - asserts `orders.placed` cumulative sum, one order, tag `cart.id`; passes
- 29.3: noExportBeforeTheDeadline_failsSayingNoMatchingSpansWereReceived - asserts `Deadline(2 s)`; the receiver gets no data; message says no matching spans were received
- 29.4: otlpHistogramPoints_contributeTheirCount - asserts `http.server.request.duration` with route `/carts/{id}/orders`, one request, expected 1
- 29.5: waitForMetricsExport_makesTheBaselineIncludeTheGivenStepsIncrement - asserts `OtlpReceiver.Start(new OtlpReceiverOptions { WaitForMetricsExport = true })`; Given adds a product, When adds another on `cart.products.added` tagged `cart.id`; expected 1 passes. Same scenario without the option is not asserted (documented limitation)
- extra: otlpDecoder_readsSpanIdsNameAndStringIntDoubleBoolAttributes - covers post a protobuf `ExportTraceServiceRequest` built with `CodedOutputStream` to the receiver; `ExpectTrace` sees trace id, span id, parent span id, name and the four attribute types formatted as strings (`1.5` under `pl-PL`, `true`)
- extra: otlpDecoder_readsSumGaugeAndHistogramPointsWithTemporality - covers delta sum adds, cumulative sum subtracts the baseline, gauge is cumulative, histogram contributes its count
- extra: otlpDecoder_skipsFieldsItDoesNotRead - covers a request with unknown fields still decodes
- extra: otlpReceiver_rejectsNonProtobufContentType - covers JSON body gets an error status
- extra: otlpReceiver_collectDoesNothingByDefault - covers returns immediately with no export
- extra: otlpReceiver_collectWithWaitForMetricsExport_failsAtTheDeadlineWhenNoExportArrives - covers `Deadline(2 s)`
- extra: aspireOtlpAttributeValues_areStringsLikeJavaStringValueOf_underPlPlCulture - covers the Aspire half of 26.6: `cart.closed = true` and `cart.total = 1.5` pass `ExpectTrace` over OTLP with culture `pl-PL`
- extra: nonGenericSpecification_configurationAfterTheFirstStep_throws - covers same message as 6.3
- extra: nonGenericDefaults_useWebJsonAndTelemetryOff - covers `ExpectTrace` fails with the telemetry-off message; `Body` uses web defaults
- extra: hostCalledTwice_replacesTheFirstAddress - covers the second address is used
```

---

## Step 6. F#, other test frameworks, README and migration guide

Runs as one step with one review and one fix round: part 6a, part 6b, then step 7, then part 6c, which documents what step 7 adds.

### Part 6a. Write the same tests in F#

```text
Use the `implement-step` skill.

What to build
An F# user writes `Given`/`When`/`Then` scenarios with `open type ApiSpecification`, `Request(fun client -> ...)`, `Observed(message, traceparent)` in `read`, and `ExpectPublished(...)` inside `task { }`, with no type annotations on the lambdas. If the F# sample needs a change in the library (overloads that do not resolve), that change is part of this step.

New sample: an F# test project of a scenario from the Wolverine sample, formatted with Fantomas, lines within 80 characters, added to `Ogooreck.slnx` and `Ogooreck.Build/Program.cs`.

Context
- `src/dotnet/Ogooreck.Sample.BusinessLogic.FSharp.Tests/` (`.fsproj`, `Deciders`): the F# project layout and `Ogooreck.Build/Program.cs` target `test-fsharp` (`RunTests(..., "fsproj")`).
- `.config/dotnet-tools.json`: Fantomas.

Similar code
- `Ogooreck.Sample.BusinessLogic.FSharp.Tests` for xUnit in F#; the Wolverine test project from step 2a for the scenario.

Blocked by
Steps 2a, 2b

Existing tests this step breaks, and how it updates them
- No existing test breaks. `dotnet fantomas --check .` must pass for the new F# project.

Acceptance criteria
- 30. An F# user writes the same tests, so Ogooreck works in F# without wrappers.
  - 30.1 GIVEN an F# test with `open type ApiSpecification` WHEN it writes a Given/When/Then with `Request(fun client -> client.PostAsJsonAsync(...))`, `Observed(message, traceparent)` in its `read` function and `ExpectPublished(...)` inside `task { }` THEN it compiles without type annotations on the lambdas, and it passes.

Tests
- 30.1: fsharpScenario_compilesWithoutAnnotationsOnLambdas_andPasses - asserts F# test: `Given(Request(fun client -> client.PostAsJsonAsync(...)))`, `Observed(message, traceparent)` in `read`, `ExpectPublished(...)` in `task { }`; the project builds with warnings as errors and the test passes
- extra: fsharpFailingScenario_failsWithOgooreckMessage - covers wrong expected message; exception message lists numbered messages
- extra: fsharpConfigureLambda_chainsConfigurationCalls - covers `fun spec -> spec.Deadline(...).Recording(...)` compiles without `ignore`
```

### Part 6b. Prove test-framework independence: NUnit, MSTest and TUnit projects

```text
Use the `implement-step` skill.

What to build
A user on NUnit, MSTest or TUnit uses the same API. Four test projects (xUnit exists, plus three new tiny ones) each run one passing and one failing scenario against the existing API sample app; the failing one fails with Ogooreck's failure message as a plain exception. The `Ogooreck` package references none of these frameworks.

New projects: `Ogooreck.Sample.NUnit.Tests`, `Ogooreck.Sample.MSTest.Tests`, `Ogooreck.Sample.TUnit.Tests`, added to `Ogooreck.slnx` and `Ogooreck.Build/Program.cs`. The failing scenario must be expected to fail without making the build fail: each project asserts the thrown exception's message (for example with the framework's own `Assert.Throws`) so the project's run is green.

Context
- `src/dotnet/Ogooreck.Sample.Api.Tests/` (project file and `ApiTests.cs`): the template for a test project that references `Ogooreck.Sample.Api`.
- `src/dotnet/Ogooreck/Ogooreck.csproj`: must stay free of test-framework references.

Similar code
- `Ogooreck.Sample.Api.Tests/ApiTests.cs` from step 1.

Blocked by
Step 1

Existing tests this step breaks, and how it updates them
- No existing test breaks.

Acceptance criteria
- 31. A user on any test framework uses Ogooreck, so it doesn't force xUnit.
  - 31.1 GIVEN one test project each for xUnit, NUnit, MSTest and TUnit WHEN each runs one passing and one failing scenario THEN the passing one passes, and the failing one fails with Ogooreck's failure message as a plain exception. The `Ogooreck` package references none of these frameworks.

Tests
- 31.1: eachFrameworkProject_passesAGoodScenarioAndFailsABadOneWithAPlainException - asserts in xUnit (existing project), NUnit, MSTest and TUnit: the good scenario passes; the bad scenario (`ExpectResponse(OK)` against a 201) throws an exception whose type is not a type of any of the four frameworks and whose message contains the expected status 200 and the actual status 201; plus a check on `Ogooreck.csproj` that lists no package reference containing xunit, nunit, mstest or tunit
- extra: ogooreckAssembly_referencesNoTestFrameworkAssembly - covers in `Ogooreck.Tests`: the referenced assembly names of `Ogooreck` contain none of the four frameworks
```

### Part 6c. Document 1.0: README rewrite and migration guide

```text
Use the `implement-step` skill.

What to build
The README's API section is rewritten around the new `ApiSpecification`, with snippets pulled from the samples. It explains why trace context must propagate and how each sample does it; that catch-up is the preferred way to wait and `Eventually` is for apps with no idle signal; that metrics without a trace id count other parallel tests (observable instruments, counters recorded outside any request's activity, anything over OTLP) and users filter by a tag unique to the test or run those tests serially; that over OTLP a Given step's increment can arrive after the baseline and users tag such metrics with something only the When step touches or opt in to `WaitForMetricsExport`. The migration guide, `MIGRATION.md` at the repository root and linked from the README (qa 42), holds the before/after table of the spec, with the "after" column as code that compiles and passes. The 1.0.0 release is made by pushing the `dotnet-1.0.0` tag (the release workflow reads the version from the tag); this plan does not push tags.

Context
- `mdsource/README.source.md` (source; `README.md` is generated, never edited by hand) and `mdsnippets.json` (80-column limit for snippets; F# lines within 80).
- `src/dotnet/Ogooreck.Build/Program.cs` target `docs` (`dotnet tool run mdsnippets ../..`); `./build.sh docs`.
- The 0.x snippets in the README source (`SEND`, `Until`, `RESPONSE_*`) are all replaced.

Similar code
- Existing README structure and `#region` snippet names from steps 1 to 12.

Blocked by
Steps 1 to 5, step 7 (the README documents the plugs and their catch-up conditions, including that the decorating Kafka plug asks the broker through the app consumer's connection, so test apps set a low `fetch.wait.max.ms`, as the Kafka sample tests do through `Kafka:Consumer`; qa 105)

Existing tests this step breaks, and how it updates them
- No existing test breaks. The 0.x code blocks in `mdsource/README.source.md` are replaced here.

Acceptance criteria
- 33. A user upgrading from 0.10 migrates their tests, so they can adopt 1.0.
  - 33.1 GIVEN a 0.10 test from the migration guide's "before" column WHEN the user rewrites it as the "after" column THEN it passes against the same app.

Tests
- 33.1: migrationGuideAfterColumn_compilesAndPassesAgainstTheSameApp - asserts each row of the spec's migration table has an "after" snippet that lives in a test in `Ogooreck.Sample.Api.Tests` (a `Migration` test class, one test per row) and passes against the sample API; the "before" column is plain text in the guide
- extra: readmeGeneration_hasNoMissingSnippets - covers `./build.sh docs` runs with no missing-snippet warning (the repo treats missing snippets as warnings; check the output is clean)
- extra: readmeContainsNoRemovedApiNames - covers grep of the generated README for `SEND(`, `Until(`, `RESPONSE_BODY`, `GivenApiSpecificationBuilder` finds nothing outside the migration guide's before column
```

## Step 7. Plugs: one `Use(plug)` call per tool, with packages for Kafka, RabbitMQ, Marten and Wolverine

Runs as one step with one review and one fix round: part 7a first, then parts 7b and 7c in parallel, each in its own copy. Built from spec r13 ("Plugging in a messaging tool or event store", "Plugs", criteria 36 to 41; qa 57 to 72).

### Part 7a. Core plugs, the decorator helper and the message reader; Wolverine and Marten plugs

```text
Use the `implement-step` skill.

What to build
A user adds a tool to a specification with one repeatable `Use(plug)` call, on both `ApiSpecification<TProgram>` and `ApiSpecification`. Core gets `IPlug`, `PlugRegistration` (`Services`, `Recording`, `CatchUp`, `Publishing`, `Starting`, `AppServices`, `Deadline`), the rule that a plug made from another one through an option replaces it in `Use(...)`, `services.Decorate<TService>(...)`, `CallRecorder.Wrap<TService>(...)` for `Task`, `Task<T>`, `ValueTask` and `ValueTask<T>`, `MessageHeaders` with Java's names (`messageType`, `messageId`, `correlationId`, `causationId`, `traceparent`), and `MessageReader.Read(header, body)` with `UnknownMessage` for an unknown type, which fails only the When step's messages. Two plug packages come with it: `Ogooreck.Wolverine` (`WolverinePlug`: a recorder of the envelopes the app publishes, with a `MessageTypes(...)` option, the tracked-session wrapping catch-up and `Publishing` through `IMessageBus`) and `Ogooreck.Marten` (`MartenPlug`: the listener recorder and the projection wait, with `WithoutCatchUp()`). The Wolverine and Marten samples, and the F# Wolverine sample, move to the plugs: their setup classes keep only app settings and `Use(...)` calls. The Wolverine sample adds a user plug for its fake clock (36.1). Checks that prove a message really was published (`EveryPublishedMessage`, for 16.1 and 16.4) stay as test code next to the setup class.

First, spike recording from Wolverine's tracked session against today's namespace-filtered middleware (spec, Wolverine plug) and record in `qa.md` which hook `WolverinePlug` uses and how `MessageTypes(...)` keeps Wolverine's own messages out.

New projects: `src/dotnet/Ogooreck.Wolverine/`, `src/dotnet/Ogooreck.Marten/`, packed like `Ogooreck` (same `Directory.Build.props` settings: documentation file, warnings as errors), added to `Ogooreck.slnx`, `Ogooreck.Build/Program.cs` (pack and coverage) and the release workflow, so all packages ship from the `dotnet-1.0.0` tag. Core references no broker client.

Context
- `src/dotnet/Ogooreck/API/` (configuration calls, `Recording`, `CatchUp`, `Publishing`, `StepCatchUp.cs` for wrapping order): `Use` adds to the same settings lists; `Services(...)` adds `ConfigureTestServices` after the `Host(...)` customization.
- `src/dotnet/Ogooreck.Sample.Wolverine.Tests/AppApi.cs`, `PublishedMessagesMiddleware.cs`: the code that moves into `WolverinePlug`.
- `src/dotnet/Ogooreck.Sample.Marten.Tests/MartenApi.cs`: the code that moves into `MartenPlug`.
- `src/dotnet/Ogooreck.Sample.Wolverine.FSharp.Tests/`: uses `AppApi.Read`; moves to the plug.
- `.github/workflows/dotnet-release.yml`, `src/dotnet/Ogooreck.Build/Program.cs`: pack and coverage targets.
- qa 59: the spike's `RecordingProxy` and `Decorate` (forward through `MethodInfo.Invoke`, unwrap `TargetInvocationException`, record on successful task completion; resolve the original as instance, factory or `ActivatorUtilities.CreateInstance`).

Similar code
- `OtlpReceiver` for a caller-owned resource passed into configuration (answer 56).
- The current `AppApi.Tracked` and `MartenApi.WaitForProjections`.

Blocked by
Steps 1 to 6b

Existing tests this step breaks, and how it updates them
- The Wolverine, Marten and F# Wolverine tests that use `AppApi.PublishedMessages`, `AppApi.Read` or `MartenApi.AppendedEvents` switch to the plug's `Published` and `Appended`, with the same scenarios and assertions.
- `AppApi.ShippingPlacedOrders()`, `MartenApi.WithCatchUp/WithoutCatchUp` stay as setup-class factories, now built from `Use(...)` calls.

Acceptance criteria
- 36. A user plugs a tool with one call, so setups compose instead of growing into per-project setup classes.
  - 36.1 GIVEN `.Use(new WolverinePlug().MessageTypes(type => type.Namespace == typeof(OrderPlaced).Namespace)).Use(clock)`, where `clock` is a user class implementing `IPlug` that registers a `FakeTimeProvider` as the app's `TimeProvider` WHEN the When step is an action that advances `clock` by 1 day AND the app expires the cart THEN `ExpectPublishedIn(wolverine.Published, new CartExpired(cartId))` passes, as in 11.1.
  - 36.2 GIVEN a user plug that calls `Services(...)`, `Recording(...)`, `CatchUp(...)` and `Publishing(...)` on its `PlugRegistration` WHEN it is passed to `.Use(...)` THEN the app resolves the service the plug registered (a `FakeTimeProvider` as `TimeProvider`), `ExpectPublished` on the plug's recorder passes for the When step's `OrderPlaced(cartId)`, the plug's catch-up function runs once after each step, its publish function receives the `Publish(new PaymentConfirmed(orderId))` step, AND a second specification of the same app without the plug resolves the app's own `TimeProvider`.
  - 36.3 GIVEN two plugs that both call `Publishing(...)` WHEN the second is passed to `.Use(...)` THEN the call throws, naming both plugs and saying that a specification has one publish function.
  - 36.4 GIVEN a plug that calls `Services(...)` WHEN it is passed to `.Use(...)` on an out-of-process `ApiSpecification` THEN the call throws, naming the plug and saying that decorating the app's services needs `ApiSpecification<TProgram>`.
  - 36.5 GIVEN a plug's catch-up function that reads `registration.AppServices` WHEN it runs after the first step THEN it gets the test host's `IServiceProvider`, AND reading it before the host started throws, saying that the host has not started yet.
- 37. A user decorates one of the app's services, so recording needs no change to production code.
  - 37.1 GIVEN the app registers a service as an instance, as a factory, or as a type WHEN a plug calls `services.Decorate<TService>(inner => wrapper)` THEN the app resolves `wrapper`, which wraps the original registration, with the original lifetime.
  - 37.2 GIVEN the app doesn't register `TService` WHEN `Decorate<TService>(...)` runs THEN the host fails to start, naming the type and saying that the app must register it.
  - 37.3 GIVEN `CallRecorder.Wrap(inner, method, args => recorded.Add(...))` WHEN the wrapped async method succeeds THEN its arguments are recorded once, AND WHEN it fails THEN nothing is recorded and the caller gets the original exception, not a `TargetInvocationException`.
- 38. (reader part) 38.3 and 38.4 on `MessageReader` directly through a user plug in the Wolverine sample; 38.1 and 38.2 are proven on brokers in parts 7b and 7c.
- 41. A user plugs Marten or Wolverine, so the sample setup classes need no recording code.
  - 41.1 GIVEN `.Use(new MartenPlug())` WHEN the criteria 19.1, 19.2 and 22.1 run THEN they pass with `marten.Appended` as the recorder.
  - 41.2 GIVEN `.Use(new WolverinePlug())` WHEN the Wolverine sample's criteria 15, 17, 20 and 12.1 run THEN they pass with `wolverine.Published` as the recorder and `Publish` steps going through Wolverine's `IMessageBus`.

Tests
- 36.1: wolverinePlugAndAUserClockPlug_compose_cartExpiresAfterTheClockAdvances - asserts both plugs on one `AppApi`; When advances the fake clock 1 day; `ExpectPublishedIn(wolverine.Published, new CartExpired(cartId))` passes
- 36.2: userPlug_registrationCalls_behaveLikeDirectConfigurationCalls - asserts a plug's `Services` registration is visible to the app, its recorder feeds `ExpectPublished`, its catch-up runs after each step, its publish function receives `Publish` steps
- 36.3: secondPublishingPlug_throwsNamingBothPlugs
- 36.4: plugWithServicesOnAnOutOfProcessSpecification_throwsNamingThePlug - asserts in `Ogooreck.Tests` or the Aspire sample, without starting an app
- 36.5: appServices_afterTheHostStarted_isTheHostsProvider_andBeforeItThrows
- 37.1: decorate_wrapsAnInstanceAFactoryAndATypeRegistration_keepingTheLifetime - asserts three registrations of a sample service, each resolved as the wrapper over the original; scoped stays scoped
- 37.2: decorate_withoutARegistration_failsTheHostStartNamingTheType
- 37.3: callRecorder_recordsSuccessfulCallsOnly_andRethrowsTheOriginalException
- 38.3: messageReader_unknownMessageType_failsNamingTheValueAndTheListedTypes
- 38.4: messageReader_customRead_replacesTheReader
- 41.1: the existing Marten sample tests, now through `MartenPlug`
- 41.2: the existing Wolverine and F# Wolverine sample tests, now through `WolverinePlug`
- extra: useOfAPlugMadeFromAnother_replacesIt_recordingOnce - covers `Use(Messaging.Wolverine)` then `Use(Messaging.Wolverine.MessageTypes(...))` records each envelope once
- extra: plugDeadline_boundsTheToolsWait - covers a 2-second deadline ends Marten's projection wait after about 2 s with Ogooreck's catch-up message
- extra: callRecorder_valueTaskMethods_recordOnSuccessAndAwaitOnce
- extra: messageReader_unknownTypeOfAGivenStepsMessage_isIgnored_andOfTheWhenStepsMessage_fails
- extra: starting_runsOnceAfterTheHostStartsAndBeforeTheFirstStep - covers order and that a failing `Starting` fails the first step naming the plug
- extra: messageReader_matchesFullNameBeforeName - covers two listed types with the same `Name`
- extra: martenPlugWithoutCatchUp_recordsButDoesNotWait - covers `WithoutCatchUp()` with `ExpectNothingAppended` failing at once for lack of catch-up (17.3 wording)
```

### Part 7b. Kafka plug: decorated producer and consumer, consumer catch-up, test-side consumer

```text
Use the `implement-step` skill.

What to build
`Ogooreck.Kafka` with `KafkaPlug.Decorating<TKey, TValue>()` (shorthand `KafkaPlug.Decorating()` for `string, string`). It decorates the app's `IProducer<TKey, TValue>` and `IConsumer<TKey, TValue>` registrations that exist (the host fails to start, naming both types, when neither exists) with `Decorate` and `CallRecorder`, records each successful `ProduceAsync`, learns each consumer's topics from `Subscribe` and `Assign`, and tracks whether the consumer is inside `Consume`. On `Starting`, it waits until every decorated consumer has an assignment, its fetch position is known and the groups of the decorated consumers are stable (never other groups on the broker, qa 95), and reads the assigned partitions' end offsets (waiting for every partition never ends when another member holds some, qa 83); past the deadline the first step fails naming the topics. Catch-up reads the end offsets at the start of each round and waits until each decorated consumer is inside `Consume` with each position at or past the end offset; an unset position counts as caught up when the end offset hasn't moved since the assignment. Past the deadline, it fails naming the topic, partition, position and end offset. Before the first step and at the start of every round, it lists the members of the decorated consumers' groups; a member that isn't a decorated consumer fails the step with the message from the spec, naming the group, the member and the ways out. Groups of consumers the plug didn't wrap are never looked at (qa 95). Option `CatchingUpWith(...)` (the user's own wait, replacing the catch-up and the check) and the reading options return a new plug sharing the same recorder, so a test class overrides the shared plug in its own `Use(...)`; failure messages name only these test-side options. `KafkaPlug.Consuming(bootstrapServers, topics)` creates no topics (qa 95); it is a test-side consumer with its own new group, started on `Starting`, reading from the end, with no catch-up, disposed by the caller. Both read with `MessageReader` by default.

The Kafka sample app switches to the recommended header names, registers its producer and consumer in DI, and gains a `shipping` hosted service: Confluent's default consumer settings, a sequential loop that handles `OrderPlaced`, produces `ShipOrder` and marks the order shipped in memory (readable by a GET), with a test switch that fails the first try and retries inside the loop (39.3), one that blocks the handler (39.5), one that adds a second member to group `shipping` built with `new ConsumerBuilder(...)` (39.7), and a `billing` consumer in its own group built the same way (39.8). Per-cart behavior is scripted through the shared app (qa 93). `KafkaApi` keeps only app settings and `Use(...)`; `KafkaBroker`'s side consumer goes. The tests run one app per test project through an assembly fixture; 39.5's 2-second specification starts its own app with its own consumer group through `UseSetting(...)`. Criteria 18, 20.5 and 32 run on `KafkaPlug.Consuming`. The Aspire sample replaces `AspireApp`'s side consumer with `KafkaPlug.Consuming(...)` configured with the Aspire app's own header names (38.2).

New project: `src/dotnet/Ogooreck.Kafka/`, packed and covered like `Ogooreck.Wolverine` from part 7a.

Context
- qa 80: the spike's `KafkaPlug` (inside-`Consume` flag, `Assignment`, `Position`, end offsets through `QueryWatermarkOffsets`, the wait for assignment).
- `src/dotnet/Ogooreck.Sample.Kafka/` (`Program.cs`, `EventPublisher.cs`), `Ogooreck.Sample.Kafka.Tests/` (`KafkaApi.cs`, `KafkaBroker.cs`).
- `src/dotnet/Ogooreck.Sample.Aspire.Tests/` (`AspireApi.cs`, `AspireApp.cs`, `ConsumedOrder.cs`), `Ogooreck.Sample.Aspire.Api/Program.cs` (its producer and headers).
- Java: `KafkaMessageBus.java` in EventSourcing.JVM, its `whenCaughtUp`.

Similar code
- `WolverinePlug` and `MartenPlug` from part 7a.

Blocked by
Part 7a

Existing tests this step breaks, and how it updates them
- Kafka sample tests that use `KafkaApi.PublishedMessages` switch to the plug's `Published`, same scenarios. Criteria 18.1 to 18.4, 20.5 and 32.1 keep passing on `KafkaPlug.Consuming`.
- Aspire tests that use the side consumer's recorder switch to the plug's `Published`.

Acceptance criteria
- 38. 38.1 and 38.2 on Kafka: the Kafka sample with Java's header names and no `read`; the Aspire sample with its own names through `Headers(...)`.
- 39. A user plugs Kafka, so Kafka apps get exact checks in Confluent's default configuration, as in Java.
  - 39.1 GIVEN `KafkaPlug.Decorating()` on an in-process app WHEN the When step's request produces `OrderPlaced(cartId)` THEN `ExpectPublishedIn(kafka.Published, new OrderPlaced(cartId))` passes.
  - 39.2 GIVEN the same AND an app whose `shipping` consumer, resolved from DI with Confluent's default settings, handles `OrderPlaced` in a sequential loop and produces `ShipOrder` WHEN the When step places an order THEN `ExpectPublishedIn(kafka.Published, new OrderPlaced(cartId), new ShipOrder(cartId))` passes as an exact check, AND a next step that reads the order sees it shipped, without `Eventually`.
  - 39.3 GIVEN the same AND a handler whose first try fails and is retried inside the loop WHEN the When step places an order THEN the result is the same as 39.2.
  - 39.4 GIVEN the same AND older messages on the topic WHEN the first test of a new host runs THEN its first step starts only after the `shipping` consumer is assigned, and the test passes as in 39.2.
  - 39.5 GIVEN a specification configured with a 2-second deadline AND a `shipping` handler that blocks WHEN a step runs THEN catch-up fails after about 2 seconds, naming the topic, the partition, the position and the end offset.
  - 39.6 GIVEN an Aspire app AND `KafkaPlug.Consuming(bootstrapServers, topics: ["orders"])` WHEN the When step's request produces `OrderPlaced(cartId)` THEN `ExpectPublishedIn(kafka.Published, new OrderPlaced(cartId))` passes once it arrives.
  - GIVEN the app from 39.2 AND a second member in group `shipping` that the app builds with `new ConsumerBuilder(...)` and starts with the host WHEN the first step runs THEN it fails, naming group `shipping`, the member's client id, and the ways out (resolve the consumer from DI, or `CatchingUpWith(...)`).
  - GIVEN the app from 39.2 AND a `billing` consumer in its own group that the app builds with `new ConsumerBuilder(...)` WHEN the test from 39.2 runs THEN it passes: the plug never looks at groups of consumers it didn't wrap.

Tests
- 38.1: kafkaPlug_readsTheRecommendedHeadersWithoutAReadFunction - asserts `ExpectCausation` passes on ids from `messageId`, `correlationId`, `causationId`
- 38.2: aspireKafkaPlug_readsTheAppsOwnHeaderNames
- 39.1: kafkaPlugDecorating_recordsTheProducedMessage
- 39.2: kafkaPlug_exactCheckPasses_andTheNextStepSeesTheOrderShipped
- 39.3: kafkaPlug_retryInsideTheLoop_exactCheckStillPasses
- 39.4: kafkaPlug_firstStepWaitsForTheConsumerAssignment_withOlderMessagesOnTheTopic
- 39.5: kafkaPlugWithABlockedHandler_catchUpFailsAfterAboutTwoSecondsNamingTopicPartitionPositionAndEndOffset
- 39.6: aspireKafkaPlugConsuming_recordsTheProducedMessage
- 39.7: kafkaPlug_secondMemberInTheAppsGroup_failsNamingTheGroupAndTheWaysOut
- 39.8: kafkaPlug_consumerOutsideDIInItsOwnGroup_isNotLookedAt
- extra: kafkaPlug_catchingUpWith_replacesTheConsumerCatchUpAndTheCheck
- extra: kafkaPlugDecorating_producerOnlyApp_starts - covers no `IConsumer` registered
- extra: kafkaPlugOption_returnsANewPlugSharingTheRecorder - covers the shared plug unchanged after a test class overrides it
- extra: kafkaPlugDecorating_failedProduce_recordsNothing - covers a produce to a topic the broker rejects
- extra: kafkaPlugConsuming_disposedByTheCaller_stopsItsConsumer
```

### Part 7c. RabbitMQ plug and sample: decorated connection, probe and delivery tracking

```text
Use the `implement-step` skill.

What to build
`Ogooreck.RabbitMQ` with `RabbitMQPlug.Decorating()`, which decorates the app's `IConnection`, the channels it creates and the consumers registered on them; records each successful `BasicPublishAsync` as exchange, routing key, properties and body; learns the consumed queues from `BasicConsumeAsync`; and catches up with a probe per consumed queue (default exchange, `catchUpProbe` header) plus tracking in the decorated consumer: per channel, each delivery tag is done when the app's handler returns; running handlers are counted; each `BasicNack`/`BasicReject` with requeue through a decorated channel is pending until a redelivery arrives. A probe counts when every lower tag on its channel is done, no handler runs and no requeue is pending. The decorated consumer acknowledges a probe when the consumer uses manual ack and never passes it to the app's handler. Past the deadline, catch-up fails naming the queues, the running handlers and the pending requeues. At the start of every round, it passively declares each queue the decorated consumers read; more consumers than decorated fails the step with the message from the spec. Queues the decorated consumers don't read are never looked at (qa 95). Option `CatchingUpWith(...)` and the reading options return a new plug sharing the same recorder, as for Kafka. Recorded bodies are copied, because the client may reuse the buffer. Reading uses `MessageReader` by default.

New sample (qa 71): `Ogooreck.Sample.RabbitMQ` and `.Tests`, a small in-process app on `RabbitMQ.Client` 7 with a Testcontainers RabbitMQ: `IConnection` registered in DI; `POST /orders` publishes `OrderPlaced` to exchange `shop` with the recommended headers and trace context; a hosted consumer on the durable queue `shipping` (bound to `shop`) handles `OrderPlaced`, publishes `ShipOrder` and marks the order shipped; `GET /orders/{id}` returns the status. App settings set `ConsumerDispatchConcurrency = 4` (40.3) and add a consumer on its own connection on `shipping` (40.7) and on a `billing` queue bound to `shop` (40.8); nack with requeue on the first try (40.4) and blocking the handler (40.6) are scripted per order through the shared app (qa 93). RabbitMQ 4.4 refuses transient non-exclusive queues, so queues are durable (qa 80). The tests run one app per test project through an assembly fixture; 40.6's 2-second specification starts its own app with its own queue names through `UseSetting(...)`. Tests need Docker. Package versions: latest stable at least two weeks old, pinned exactly (qa 41).

New projects: `src/dotnet/Ogooreck.RabbitMQ/`, `Ogooreck.Sample.RabbitMQ`, `Ogooreck.Sample.RabbitMQ.Tests`, added to `Ogooreck.slnx`, `Ogooreck.Build/Program.cs` and the release workflow.

Context
- qa 80: the spike's `RabbitPlug` and `ChannelState` (three-level `DispatchProxy`, delivery-tag tracking, requeue counting).
- `src/dotnet/Ogooreck.Sample.Kafka/` and `.Tests` for the layout of a broker sample with Testcontainers.
- Java: `RabbitMQMessageBus.java` in EventSourcing.JVM (`catchUpProbe` header).

Similar code
- `KafkaPlug` from part 7b (they run in parallel; share only part 7a's core).

Blocked by
Part 7a

Existing tests this step breaks, and how it updates them
- No existing test breaks.

Acceptance criteria
- 38. 38.1 on RabbitMQ: the sample sends Java's header names and the plug needs no `read`.
- 40. A user plugs RabbitMQ, so RabbitMQ apps get exact checks, as in Java.
  - 40.1 GIVEN `RabbitMQPlug.Decorating()` on an in-process app WHEN the When step's request publishes `OrderPlaced(orderId)` THEN `ExpectPublishedIn(rabbitmq.Published, new OrderPlaced(orderId))` passes.
  - 40.2 GIVEN the same AND an app whose consumer on queue `shipping` handles `OrderPlaced` and publishes `ShipOrder` WHEN the When step places an order THEN `ExpectPublishedIn(rabbitmq.Published, new OrderPlaced(orderId), new ShipOrder(orderId))` passes as an exact check, AND a next step that reads the order sees it shipped, without `Eventually`.
  - 40.3 GIVEN the same with `ConsumerDispatchConcurrency = 4` WHEN the When step places 20 orders THEN all 20 are shipped when catch-up ends.
  - 40.4 GIVEN the same AND a handler that nacks each message with requeue on its first try WHEN the When step places an order THEN the result is the same as 40.2.
  - 40.5 GIVEN the same WHEN catch-up probes the `shipping` queue THEN the app's handler never receives the probe.
  - 40.6 GIVEN a specification configured with a 2-second deadline AND a `shipping` handler that blocks WHEN a step runs THEN catch-up fails after about 2 seconds, naming the queue and one running handler.
  - GIVEN the same app AND a second consumer on `shipping` that the app creates on its own connection WHEN a step runs THEN catch-up fails, naming queue `shipping`, 2 consumers, 1 decorated, and the ways out (resolve the consumer from DI, or `CatchingUpWith(...)`).
  - GIVEN a consumer on its own connection on queue `billing`, bound to exchange `shop`, that the decorated consumers don't read WHEN a step publishes to `shop` THEN catch-up passes: the plug never looks at queues its decorated consumers don't read.

Tests
- 38.1: rabbitMQPlug_readsTheRecommendedHeadersWithoutAReadFunction
- 40.1: rabbitMQPlugDecorating_recordsThePublishedMessage
- 40.2: rabbitMQPlug_exactCheckPasses_andTheNextStepSeesTheOrderShipped
- 40.3: rabbitMQPlug_concurrencyFour_allTwentyOrdersShippedWhenCatchUpEnds
- 40.4: rabbitMQPlug_nackRequeueOnFirstTry_exactCheckStillPasses
- 40.5: rabbitMQPlug_probeNeverReachesTheAppsHandler - asserts the handler's count of handled messages equals the number of orders placed
- 40.6: rabbitMQPlugWithABlockedHandler_catchUpFailsAfterAboutTwoSecondsNamingTheQueue
- 40.7: rabbitMQPlug_secondConsumerOutsideDI_failsNamingTheQueueAndCounts
- 40.8: rabbitMQPlug_consumerOnAnotherBoundQueue_isNotLookedAt
- extra: rabbitMQPlug_catchingUpWith_replacesTheProbeAndTheCheck
- extra: rabbitMQPlug_noConsumedQueues_catchUpReturnsAtOnce - covers an app that only publishes
- extra: rabbitMQPlugDecorating_failedPublish_recordsNothing
```

## Step 8. Variants of the test setup on a shared app: `With` and `WithNewApp`

A user changes only test setup (deadline, recordings, catch-ups, publishing, telemetry, JSON, plug options) for a test class without starting another app, and starts a separate app only explicitly (qa 96, spec r15 "Hosts and setup", criteria 2.4-2.6).

- Core: `ApiSpecification<TProgram>.With(configure)` returns a specification sharing the running app; `SpecificationSettings.Variant()` copies the test setup, keeps the app's plugs as the app's (their `Services` are skipped and their `Starting` runs once, through the parent), shares the telemetry source unless the variant sets its own, and lets the variant replace an inherited publish function. The host customization is given at construction (`For<Program>(host)`, base constructor `(host, configure)`), so a variant can't change it (qa 97); a plug that changes services and isn't the app's throws, naming the app's setup and `WithNewApp(...)`. Disposing a variant disposes only its own telemetry. `WithNewApp(configure)` and `WithNewApp(host, configure)` build a specification with the root's host customization (then `host`), the root's and the variant's plugs, and the test setup copied.
- Plugs: the Kafka plug keeps its wrapped consumers, and the RabbitMQ plug its `HostState`, in the app's services as keyed singletons (key: the plug's `Origin`); catch-up and the first-step wait read them through `registration.AppServices`.
- Samples: tests that built a specification only to change test setup use the shared app and `With(...)`, in the test class setup; per-cart and per-order scripting replaces app switches such as "without shipping"; tests whose app differs use `WithNewApp(...)` or keep their own explicit setup, each with its reason in progress.md.

Tests: `Ogooreck.Sample.Api.Tests/SharedAppTests.cs` (same app, start on first use through a variant, the variant's deadline only, inherited and added test setup, disposal sync and async, `Host` and service-changing plug errors, a plug of the app starting once, a starting-only plug in a variant, a variant of a variant, `WithNewApp` with the same setup, of a variant, with host changes, traces through the app's telemetry, own telemetry off, replacing and twice-setting publishing, configuration after the variant's first step).

## Decisions made while planning

- 0.x types and helpers the spec does not name stay only if the new code uses them (qa 39).
- An `Observed` with a malformed `traceparent` is ignored, like one with none (qa 40).
- Packages: no MassTransit (qa 51); others at the latest stable version at least two weeks old, pinned exactly (qa 41).
- The migration guide is `MIGRATION.md` at the repository root, linked from the README (qa 42).
