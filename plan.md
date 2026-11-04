# Ogooreck migration plan

## Scope and decisions

Migrate the existing library, samples, tests, build scripts and CI to `.slnx`
and .NET 10. Remove Newtonsoft.Json and FluentAssertions completely. Keep the
Given/When/Then API and framework independence. Implement the comparisons used
by this library in repository code; do not recreate FluentAssertions as a product.

User decisions recorded during analysis:

- Target .NET 10 only (drop .NET 6/7/8).
- Rechecked .NET 11 on 2026-11-03: Microsoft lists RC3 with go-live support,
  with general launch scheduled for November 10. User chose to keep .NET 10
  for this migration and add .NET 11 support after general release.
- Compare emitted events in order. Preserve default equivalence elsewhere,
  including the original specialized ordering/value-equality rules.
- Replace public `JsonSerializerSettings` parameters with `JsonSerializerOptions`.
  Follow standard System.Text.Json constructor rules; do not guess constructors.
- IHost/Aspire modernization belongs to subsequent work, planned below.
- Additional small modernization changes authorized after the initial analysis.
- User requires xUnit v3 for all repository tests; remove v2 and the separate
  Ogooreck.Xunit3.Tests project. Use FsCheck.Xunit.v3 for F# properties.
- Do not add global.json or another SDK pinning convention.
- Keep the dedicated AssertionException, deriving from InvalidOperationException.
- Match FluentAssertions 6.12 default equivalence; use a separate ordered path
  for emitted-event sequences. Other comparison rules follow the old defaults.

## Repository findings

- Six projects: library, build executable, sample API, API tests, C# business-logic
  tests and F# property tests. Existing targets span .NET 6/7/8.
- Production FluentAssertions usage comprises structural equivalence for events,
  states and response bodies, and simple status/header/null checks. Samples also
  use simple assertions that can use xUnit directly.
- Request bodies already use `System.Net.Http.Json`; responses use Newtonsoft.
  The Newtonsoft namespace exposes constructor selection helpers publicly.
  Removing these and changing settings types is a deliberate source/binary break.
- `ThenThrows` silently succeeds without an exception; a response assertion
  compares a value with itself; the JSON result wrapper ignores supplied settings;
  `GetETagValue` calls the created-ID reader. Address these with regression tests
  while replacing the relevant assertions/serialization code.
- Retry exhaustion is not enforced for checks returning false. Async `And`
  continuations can complete before their nested tasks. Host disposal swallows
  failures. Async callback correctness is included in the authorized small
  improvements; retry and host lifetime changes remain explicit next steps.
- CI's build helper runs only the API suite. Shell wrappers disagree about SDK
  requirements. Docs use generated README snippets with a separate source file.
- Installed SDK: 10.0.103; installed .NET 10 runtime: 10.0.3.

## Implementation sequence

1. Establish baseline test results and verify official package metadata/docs.
2. Use `dotnet sln src/Ogooreck.sln migrate`; preserve projects and solution items,
   then remove the old solution and update build references. Do not add
   global.json. Update all project TFMs, launchers and workflows. Ensure build/test includes every test project.
3. Upgrade every retained direct NuGet dependency to a verified stable compatible
   version. Check runner/FsCheck compatibility and compiler/FSharp.Core alignment.
   Do not silently swap test frameworks or add packages requiring .NET 11.
   Remove unused test-host references; pin documentation tooling locally.
4. Add a small internal assertion layer and repository-owned structural comparer:
   public readable properties/fields, scalar equality, records, nested graphs,
   dictionaries by key and unordered collections with duplicate accounting.
   Event sequences use the separate ordered helper. Preserve original numeric
   equivalence, enum values, custom Equals, declared member types, dictionary key
   comparers, byte-collection ordering, cycle failures and the depth-10 rule. Avoid
   JSON-based comparison, which loses CLR type/member information. Use a library
   exception without a test-framework dependency. Prove failure cases as well as
   success cases, including F# discriminated unions.
5. Switch response deserialization to System.Text.Json with web defaults and
   configurable options. Reject empty/null bodies, forward options through both
   overloads, and record enum/custom-constructor/converter changes in this plan. Delete
   the Newtonsoft helper namespace. Use xUnit assertions in samples/docs.
6. Add focused regression coverage for changed contracts and previously vacuous
   assertions. Run release solution build/tests, build wrapper, pack, dependency
   and vulnerability checks. Inspect package metadata for removed dependencies.
7. Update existing documentation code examples only where required by changed
   code. Rebuild README using MarkdownSnippets and test snippets; do not add
   migration/build notes to public docs. Record versions, commands/results and
   any unresolved limitations in `todo.md`.

## Compatibility boundaries

This is a breaking library update: net10-only, serializer settings/namespace
removal, ordered events and a different assertion exception type. Do not publish
or choose a release number without the user's decision. Preserve existing package
identity during development. The comparer covers Ogooreck's needs, not arbitrary
FluentAssertions customization, global configuration or message compatibility.

Assertion failures use the user-selected `AssertionException : InvalidOperationException`.
There is no general public BCL assertion exception; no xUnit exception is used.

Comparison compatibility is verified against the original cached FluentAssertions
6.12.0 DLL with an external harness, without adding it back to any repository
project. The current matrix has 104 cases and zero pass/fail differences. It covers
numeric conversion/precision, enums, custom Equals, records/structs/tuples,
declared and runtime member types, cross-field/property matching, interfaces,
collection order/duplicates/nulls, dictionaries and key comparers, byte collections,
array dimensions, immutable arrays, references, cycles and the depth limit.
The repository tests store those characterized outcomes; passing our own tests
alone is not evidence of compatibility. Ordered events have a separate helper and
separate tests for counts, duplicates, positions and nested equivalence.

The previous implementation's depth-64, cycle-success and native JsonElement
content rules have been removed. Default equivalence again uses value equality
for JsonElement and F# unions, as FA 6.12 did. Consequently, a union's own Equals
can preserve order inside its payload; the comparer does not override that Equals.
Byte collections and multidimensional arrays retain the original ordered rules.
This work covers Ogooreck's object-graph comparisons; FluentAssertions' configurable
assertion pipeline and specialized XML/ADO.NET assertion APIs are not recreated.

Host-backed scenarios now inherit the tested API's JSON settings automatically,
with optional per-call overrides. Standalone responses and contexts use shared
`JsonSerializerOptions.Web` defaults. Both `GetResultFromJson<T>` overloads forward
explicit options; the Result overload also inherits response options from context.
String enums use the API's converter, a caller override or a serialization attribute;
custom converters and `[JsonConstructor]` follow
standard System.Text.Json rules. Empty/invalid JSON throws `JsonException`, and
JSON null fails the response expectation. Buffered content remains readable for
multiple assertions/extractions. These migration details stay in this plan.

Migrate existing C# and F# test projects to xUnit v3. FsCheck has an official
`FsCheck.Xunit.v3` integration; the earlier claim that F# required v2 was incomplete.
Use `xunit.v3.mtp-off` 4.0.2 with the existing VSTest adapter; the default v3 package
enables MTP v2 and rejects VSTest under SDK 10. Do not introduce a separate v3
project, duplicate the suites, or change the solution-wide test platform.
Production code has no xUnit dependency. Existing async regression waits use
xUnit v3's cancellation token, and a type alias resolves the TestContext name.
Do not suppress new compiler/analyzer failures or vulnerabilities to get green
checks; resolve their cause or bring a concrete decision to the user.

## Quick wins completed in this migration

- Use the .NET 9 `JsonSerializerOptions.Web` shared read-only defaults. Custom
  options remain caller-owned and are forwarded unchanged.
- Replace obsolete FormatterServices initialization with RuntimeHelpers.
- Replace async continuations with `async`/`await`; await callbacks and propagate
  original failures. Honor cancellation while awaiting a pending scenario builder.
- Use .NET 10's transitive package auditing and package pruning defaults; verify
  the whole dependency graph instead of adding redundant in-box JSON packages.
- Pin documentation tools locally, propagate PowerShell failures, and run all
  test suites through the existing build helper.
- Avoid unrelated C# syntax rewrites, new infrastructure packages and features
  with no consumer in this library.

The shared web defaults are a .NET 9 addition; package pruning is a .NET 10
SDK improvement. Async correctness, RuntimeHelpers cleanup
and reproducible tooling are migration cleanups, not newly introduced .NET 10 APIs.

## Additional quick wins proposed

Q1 was approved and implemented in the JSON host inheritance follow-up below.
Q2-Q5 remain recommendations, not completed features or approved public API designs.
Effort is relative: small means a focused implementation with regression tests;
medium means several related APIs or lifetime behavior must be reviewed together.

| Priority | Change and location | Benefit / acceptance check | Effort and decision |
| --- | --- | --- | --- |
| Q1 | Consistent inherited JSON options across requests, assertions, retry checks and extraction | Real Minimal API/MVC converter round trips, per-call overrides, chaining and host isolation | Implemented with the approved MVC-first precedence; standalone responses retain web defaults |
| Q2 | Cancellation-aware JSON reads in `HttpResponseMessageExtensions` | Cancellation interrupts pending content reads while repeated reads still work; retain existing overloads | Small; agree new overload signatures. Full scenario cancellation also needs callback propagation in N1 |
| Q3 | Opt-in strict JSON contract checks using .NET 9 nullable/required-constructor flags and .NET 10 duplicate-property rejection | Missing required values, explicit nulls and duplicate properties can fail contract tests | Small coverage/sample follow-up after Q1; keep web defaults. User decides whether a convenience preset is useful; `JsonSerializerOptions.Strict` also changes casing and must not silently replace web behavior |
| Q4 | Cache reflected public-member metadata in `StructuralComparison` | Repeated comparisons reuse discovery; all comparison semantics and paths stay identical; measure repeated large collections before/after | Small, internal implementation; do after correctness review, without adding a cache for mutable comparison results |
| Q5 | Centralize repeated versions with `Directory.Packages.props` | One version per shared dependency; identical resolved graph and package metadata | Small build convention change; user decides whether central package management is wanted for this repository |

Source-generated JSON metadata can already be supplied through the existing
`JsonSerializerOptions.TypeInfoResolver`. A dedicated `JsonTypeInfo<T>` overload
is a convenience/API decision, not a prerequisite for that existing capability.
It does not make reflection-based comparison or object initialization AOT-safe.

Defer .NET 10 PipeReader deserialization: our public input is HttpContent, and
scenarios intentionally read bodies repeatedly. Introducing streaming needs an
explicit buffering/ownership contract and a measured use case. New collection
types, C# extension blocks, file-based apps and blanket syntax rewrites have no
identified benefit for the existing library. TimeProvider is available since
.NET 8; adopting it is useful for retry tests, but is not a .NET 9/10 feature.

## Next steps: delivery order and user decisions

The migration is complete; the following work is planned separately. xUnit v3
compatibility was additionally requested and is tracked in todo.md.
Each stage should be reviewable on its own. Proposed decisions below are not
approvals; ask the user before implementing the affected contract.

| Stage | Deliverable | Decision to ask before implementation | Acceptance checks |
| --- | --- | --- | --- |
| N1 | Fix retry limits/cancellation and use injectable TimeProvider; complete Q2 | Recommend `maxNumberOfRetries` means additional attempts (zero = one send), false on the final check fails, and only explicitly retryable exceptions are retried. Agree exception policy and timeout diagnostics | Bounded false/throw paths, no delay after success, immediate cancellation, deterministic time tests, retained final diagnostics |
| N2 | Define client/request/response/host ownership; add async disposal | Agree who owns a supplied client and factory, how callers release returned responses, and whether recorded calls retain live responses or snapshots | Caller-owned resources survive; owned resources dispose once; cleanup failures remain observable; diagnostics still work after failed attempts |
| N3 | Expose a host-independent HTTP entry point using client creation | Agree public names and whether N2 ownership is explicit in options/factories | Existing WebApplicationFactory scenarios still work; identical scenarios work with externally configured HttpClient creation; xUnit v3 tests pass |
| N4 | Generic Host adapter for DI/service scenarios | Owned vs externally started hosts; per-scenario vs per-step DI scope; same package vs optional adapter | Async start/stop, scope disposal and cancellation; non-HTTP worker/service test; existing HTTP behavior retained |
| N5 | Aspire AppHost integration sample on the N3 boundary | Recommend a sample first; decide whether a reusable Ogooreck.Aspire package is warranted after the sample | Build/start real AppHost, bounded health readiness, named-resource HttpClient, sequential scenario, failure diagnostics and cleanup |
| N6 | Release follow-ups: stable .NET 11 targeting, packaging and optional MTP | .NET 11 after GA already authorized; release number/package split and solution-wide runner change remain user decisions | net10/net11 build/test matrix, package inspection, xUnit v3/FsCheck compatibility retained; MTP remains a separate runner decision |

The details below explain the implementation boundaries for those stages.

1. **Correct retry contracts first.** Async callback correctness is covered above.
   Bound false-returning retries, stop
   immediately on success, propagate cancellation and retain final diagnostics.
   Decide retries-versus-attempts semantics and failed-response ownership with the
   user. Test cancellation, exhaustion, async failure and response cleanup.
   `TestContext.Calls` currently retains live requests/responses; do not dispose
   retry responses blindly without first agreeing how recorded diagnostics work.
   A passing check currently still incurs a delay; remove it in this stage.
   Agree how response/retry callbacks receive cancellation without silently
   replacing the public `ResponseAssert` and `RetryCheck` delegates.
2. **Decouple HTTP scenarios from WebApplicationFactory.** Existing builders
   already accept `Func<HttpClient>`; JSON resolution now independently accepts
   `IServiceProvider` and is tested with Generic Host services. Introduce a public
   host-independent entry point
   around client creation while preserving `ApiSpecification<TProgram>` as an
   adapter. Agree API names and client ownership before implementation: builders
   currently dispose every created client, so accepting shared clients needs an
   explicit lifetime contract.
3. **Generic Host support.** Design a separate adapter for externally managed
   `IHost`/`IHostApplicationBuilder`, DI scopes and non-HTTP scenarios. An `IHost`
   alone does not imply an HTTP server. Separate starting/stopping an owned host
   from accessing a caller-owned running host; use async disposal and cancellation.
   Consider whether host integration should be a separate optional package so
   business-logic tests do not require MVC testing dependencies.
4. **Aspire integration through its supported testing APIs.** Start an AppHost via
   `DistributedApplicationTestingBuilder.CreateAsync`, build/start asynchronously,
   wait for resource readiness and obtain resource clients via `CreateHttpClient`.
   Feed those clients into the host-independent HTTP adapter. Do not treat Aspire
   as a WebApplicationFactory or scrape dashboard endpoints. Decide whether a
   sample is enough before introducing an optional Ogooreck.Aspire package.
   Use `ResourceNotifications.WaitForResourceHealthyAsync` with cancellation and
   an explicit timeout; retries for HTTP assertions are not service readiness.
5. **Diagnostics and deterministic tests.** Optional scenario/HTTP diagnostics,
   TimeProvider for retries, bounded readiness waits, predictable cleanup and
   explicit isolation for shared sample state. Decide logging/redaction policy
   before capturing request/response bodies.
6. **Additional follow-ups requiring user choices.** Solution-wide Microsoft
   Testing Platform migration including matching FsCheck integration; package split into
   core and ASP.NET adapters; source-generated JSON/AOT support (reflection-based
   comparison and object initialization require a separate compatibility audit).
7. **Add .NET 11 after general release.** User authorized supporting both .NET 10
   and .NET 11 once the stable release is available. Recheck SDK and ASP.NET package
   versions, multi-target the library/tests, and run both frameworks in CI.

## Open GitHub issues reviewed on 2026-11-04

Reviewed all nine open issues and their acceptance criteria (none currently has
comments). The user accepted the proposed delivery order. Issue #23 was closed
as completed after confirming that GitHub main contains the fix. Other closure
recommendations and public API proposals still require the user's decision.

| Issue | Current evidence | Recommendation / remaining work |
| --- | --- | --- |
| [#23: ThenThrows passes without an exception](https://github.com/oskardudycz/Ogooreck/issues/23) | Fixed in the migration: missing exceptions throw AssertionException; regression coverage also checks the matching exception, a different exception and callback failure | Closed as completed on 2026-11-04 |
| [#22: Different request/response JSON serializers](https://github.com/oskardudycz/Ogooreck/issues/22) | Requests and responses now both use System.Text.Json; both GetResultFromJson overloads accept converter options | Implemented locally: BODY, RESPONSE_BODY, RESPONSE_BODY_MATCHES and extraction inherit host settings and accept per-call options. Real converter round trips cover the use case. Keep open until this change is merged/published |
| [#19: System.Text.Json support](https://github.com/oskardudycz/Ogooreck/issues/19) | Serializer replacement is complete, including standard constructors and web defaults | Implemented locally: host settings are inherited automatically using the approved MVC-first precedence, with per-call overrides for different endpoints. Keep open until this change is merged/published |
| [#21: Form-data requests](https://github.com/oskardudycz/Ogooreck/issues/21) | A custom RequestTransform can already assign MultipartFormDataContent or FormUrlEncodedContent to request.Content; BODY is JSON-only | Keep open. A focused multipart upload/field scenario could prove the existing extension point without a new API. Decide whether a helper is wanted and whether it covers multipart, URL-encoded forms or both; agree ownership before accepting reusable content instances |
| [#20: Business-logic TestContext](https://github.com/oskardudycz/Ogooreck/issues/20) | TestContext remains confined to the API namespace; business-logic builders expose state/events and callbacks | Not implemented. The issue has no body: agree what the context records/exposes and which callbacks receive it before designing an API |
| [#18: Problem Details and error messages](https://github.com/oskardudycz/Ogooreck/issues/18) | Generic JSON extraction can deserialize a caller's DTO, but no dedicated Problem Details assertion or enriched HTTP failure diagnostics exists | Not implemented. Decide helpers/fields and failure-message behavior, including whether response detail/extensions can appear in diagnostics |
| [#4: NuGet logo](https://github.com/oskardudycz/Ogooreck/issues/4) | No tracked logo asset or active PackageIcon; the old PackageIconUrl is commented out | Not implemented. Small packaging follow-up once the user selects an image; embed it and inspect the nupkg metadata |
| [#3: API tests for README examples](https://github.com/oskardudycz/Ogooreck/issues/3) | Existing API tests cover basic GET/POST and the migration adds register/read chaining; other README API examples remain handwritten and untested | Partly addressed. Add executable examples in the existing API test project and regenerate their snippets. Cover Given/context, chaining, fixtures and eventual consistency; retry examples depend on N1's contract decision |
| [#2: XML documentation comments](https://github.com/oskardudycz/Ogooreck/issues/2) | XML output is enabled, but almost all existing public APIs still suppress CS1591 and have no comments | Not implemented. Document behavior after the relevant API decisions, remove suppressions from completed areas and inspect packaged XML |

Delivery order: #23 closed; implement and verify the approved Q1 / host-settings
policy for #22/#19;
verify multipart support for #21; extend executable API snippets for #3. The
logo is an independent small task after asset selection. Context and Problem
Details need user decisions before implementation; XML documentation should
describe those agreed contracts.

## JSON host inheritance: requirements and research

The user requires automatic inheritance from the tested API settings, with
optional per-call overrides, and requested an API proposal before implementation.
No scenario source-selection API is wanted. On 2026-11-04 the user approved the
existing-helper API and MVC-first precedence described below, and requested preparation
for IHost/Aspire as part of implementation.
Research checked on 2026-11-04:

| Tool / source revision | Actual behavior | Implication for Ogooreck |
| --- | --- | --- |
| [Alba host](https://github.com/JasperFx/alba/blob/bb576a745925d790c10542a1c216dc19dd3e96d9/src/Alba/AlbaHost.cs), [MVC strategy](https://github.com/JasperFx/alba/blob/bb576a745925d790c10542a1c216dc19dd3e96d9/src/Alba/Serialization/FormatterSerializer.cs), [Minimal API strategy](https://github.com/JasperFx/alba/blob/bb576a745925d790c10542a1c216dc19dd3e96d9/src/Alba/Serialization/SystemTextJsonSerializer.cs) | Builds two strategies. MVC reuses configured input/output formatters; Minimal API reads IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>.Value.SerializerOptions, falling back to web defaults. Default selects MVC when matching formatters exist, otherwise Minimal API | Inheritance is established from the running host; MVC and Minimal API settings remain distinct. Ogooreck's scope stays System.Text.Json, rather than reproducing Alba's formatter abstraction and Newtonsoft support |
| [Alba request selection](https://github.com/JasperFx/alba/blob/bb576a745925d790c10542a1c216dc19dd3e96d9/src/Alba/Scenario.cs), [response reads](https://github.com/JasperFx/alba/blob/bb576a745925d790c10542a1c216dc19dd3e96d9/src/Alba/ScenarioResult.cs), [docs](https://jasperfx.github.io/alba/scenarios/json.html) | Request helpers accept JsonStyle.Mvc or JsonStyle.MinimalApi for mixed hosts. ReadAsJson/ReadAsJsonAsync use the host default, without that override parameter | Alba's explicit request style is research context, not an approved Ogooreck API. Ogooreck must automatically inherit the API settings for requests, assertions and extraction |
| [FastEndpoints startup](https://github.com/FastEndpoints/FastEndpoints/blob/59d3cbfa32afc62ba773caed77ac89c35c1221a8/Src/Library/Main/MainExtensions.cs), [client helpers](https://github.com/FastEndpoints/FastEndpoints/blob/59d3cbfa32afc62ba773caed77ac89c35c1221a8/Src/Library/Testing/HttpClientExtensions.cs), [configuration](https://github.com/FastEndpoints/FastEndpoints/blob/59d3cbfa32afc62ba773caed77ac89c35c1221a8/Src/Library/Config/Config.cs) | Copies HTTP JsonOptions during serializer initialization into static configuration, then applies application configuration. Client helpers use those settings for requests and a copy for response reads, including its test-specific unmapped-member policy | Reusing application converters is useful; Ogooreck needs scenario/host isolation for different factories running concurrently |
| [.NET 10 HttpClient JSON helpers](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.json.httpclientjsonextensions.postasjsonasync?view=net-10.0), [response JSON source](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Net.Http.Json/src/System/Net/Http/Json/HttpContentJsonExtensions.cs) | Use explicit options or JsonSerializerOptions.Web; HttpClient/HttpContent alone do not resolve host DI settings | A bare HttpResponseMessage requires explicit options unless associated with an Ogooreck result/context. Do not claim automatic inheritance on a detached response |

Implementation review on 2026-11-04 compared the changed resolver and helper
integration with the three pinned host/serializer sources above. No copied
implementation was identified. Research informed the MVC-first precedence;
the implementation reads public ASP.NET Core options directly and extends
Ogooreck's existing TestContext, BODY, RESPONSE_BODY, RESPONSE_BODY_MATCHES and
Result APIs. It introduces no third-party serializer strategies, formatter
execution pipeline or scenario model. Naming follows the existing records,
camelCase private fields and uppercase scenario helpers. All comments added
in this change have been removed at the user's request.

Required behavior:

- Existing BODY, response assertions and result extraction automatically inherit
  the tested API's configured System.Text.Json settings. The caller must not
  select MVC or Minimal API settings in Given or configure a scenario source.
- Optional per-call JsonSerializerOptions overrides apply to that operation
  only. They must not modify the application's options or subsequent operations.
- Preserve inheritance across Given/When requests, response expectations, retry
  checks, result extraction and AndWhen chaining, with isolation between hosts.
- Do not introduce a JsonSettingsSource enum or Given selection overload.
- Use the approved precedence for mixed hosts: MVC when matching JSON
  input/output formatters exist, otherwise HTTP JSON settings. Per-call options
  cover operations whose endpoint uses different settings.
- Establish the limits for detached HttpResponseMessage extraction and
  endpoint-local serializer overrides from source/docs before proposing the API.
- API and mixed-host policy approved. Implement within existing projects, keeping
  endpoint-local and detached-response limitations explicit in this plan.

Approved implementation:

1. Lazily resolve JSON settings from the running WebApplicationFactory.Services
   when a JSON helper first runs, before serializing/deserializing its value. Keep
   this internal; existing Setup(factory), Given() and When() calls remain sufficient.
2. For MVC, read the configured System.Text.Json input/output formatter settings;
   for Minimal APIs, read IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>.
   Use the appropriate request/response options without modifying host settings.
3. Pass access to these host settings through the existing context to BODY,
   RESPONSE_BODY, RESPONSE_BODY_MATCHES, Result.GetResultFromJson and
   GetResponseBody. Preserve it across chained steps. In particular, fix
   GetResponseBody's current mapping through a bare HttpResponseMessage, which
   discards access to the host context.
4. Add optional per-operation options overloads, retaining existing calls:

   ```csharp
   await api.Given()
       .When(POST, URI("/items"), BODY(request))
       .Then(OK, RESPONSE_BODY(expected))
       .GetResponseBody<Item>();

   // Only explicit overrides require options:
   BODY(request, options);
   RESPONSE_BODY(expected, options);
   RESPONSE_BODY_MATCHES<Item>(predicate, options);
   // ...GetResponseBody<Item>(options)
   ```

Approved mixed-host policy: a mixed MVC/Minimal API host can have conflicting settings.
Use MVC options when matching JSON formatters exist, otherwise HTTP JSON settings.
This precedence avoids a new source-selection API,
but does not automatically follow each endpoint in a mixed host. The user
approved this policy on 2026-11-04. Do not describe it as endpoint-aware
inheritance. ASP.NET Core's MatcherFactory and Matcher are internal in .NET 10;
do not use reflection or invent URL matching to infer the endpoint. Checked:
[MatcherFactory](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Http/Routing/src/Matching/MatcherFactory.cs),
[Matcher](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Http/Routing/src/Matching/Matcher.cs).
Endpoint-local JsonResult / Results.Json options are separate overrides, not
host-wide settings. A detached HttpResponseMessage cannot resolve host DI by
itself; preserve the existing explicit-options overload for that case.

Implemented acceptance tests in the existing projects: real Minimal API and MVC
converter round trips, deliberately different settings in one mixed host,
per-call override isolation, request/response/assertion/retry/extraction/chaining
consistency, concurrent hosts with conflicting converters, default settings,
and repeated body reads. Add executable snippets only for changed APIs, and
regenerate README through MarkdownSnippets. Do not expand unrelated public
documentation. Verification: `./build.sh test pack` passes 157 tests with zero
build warnings/errors; `./build.sh docs` regenerates the existing snippets with
no README changes. The NuGet package retains its single MVC Testing dependency.

### IHost / Aspire preparation in this change

The user requested preparation, without requiring a full integration. Keep JSON
settings resolution host-independent: accept an IServiceProvider and return
separate request/response options. TestContext carries a lazy settings resolver;
WebApplicationFactory supplies its running Services. Builders retain their existing
Func<HttpClient> boundary, so serialization and scenario execution do not depend
on WebApplicationFactory. Add no Aspire dependency or new project in this change.

The resolver is verified with IHost.Services in a Generic Host regression test. A public
host-independent entry point, client/host ownership, async lifecycle and scoped
service scenarios remain the N2-N4 API decisions. Propose those contracts before
adding them; an IHost does not inherently provide an HTTP client or API endpoint.

Aspire's named-resource CreateHttpClient fits the existing client factory boundary,
but the tested resources run in separate processes. AppHost.Services is not the
API resource's service provider, so it cannot supply that resource's JSON settings.
For N5, start with a sample using explicit shared API options/contracts for remote
requests and responses; agree how those options are supplied at the future public
entry point. Do not claim automatic remote DI inheritance. Readiness, cancellation,
resource/client ownership and failure diagnostics remain part of that adapter work.
Sources: [IHost and Services](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host),
[Aspire resources and clients](https://aspire.dev/testing/accessing-resources/),
[Aspire process isolation](https://learn.microsoft.com/en-us/dotnet/aspire/testing/overview).

## References checked

- [Official solution migration command](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-sln)
- [System.Text.Json migration differences](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/migrate-from-newtonsoft)
- [Immutable types and constructor selection](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/immutability)
- [Existing equivalence semantics](https://fluentassertions.com/objectgraphs/)
- [xUnit v3 migration and package changes](https://xunit.net/docs/getting-started/v3/migration)
- [Aspire testing overview](https://aspire.dev/testing/overview/)
- [Aspire first integration test](https://aspire.dev/testing/write-your-first-test/)
- [Official NuGet version feed](https://api.nuget.org/v3/index.json)
- [.NET 9 JSON additions](https://devblogs.microsoft.com/dotnet/system-text-json-in-dotnet-9/)
- [.NET 10 package auditing and pruning](https://devblogs.microsoft.com/dotnet/nuget-package-pruning-in-dotnet-10/)
- [.NET 11 release status](https://dotnet.microsoft.com/en-us/download/dotnet/11.0)
- [.NET 11 launch schedule](https://dotnet.microsoft.com/en-us/conf/2026/agenda)
- [.NET 9 library additions and JSON contract options](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9/libraries)
- [.NET 10 strict JSON and PipeReader support](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries)
- [Source-generated JSON through options/type metadata](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)
- [TimeProvider availability and testing](https://learn.microsoft.com/en-us/dotnet/standard/datetime/timeprovider-overview)
- [Central Package Management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)
- [xUnit v3 packages and runner choices](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)
- [.NET 10 test platform selection](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test)

- [FsCheck xUnit v3 integration](https://www.nuget.org/packages/FsCheck.Xunit.v3/3.6.0)
- [Original FluentAssertions 6.12 comparison source](https://github.com/fluentassertions/fluentassertions/tree/6.12.0/Src/FluentAssertions/Equivalency)
