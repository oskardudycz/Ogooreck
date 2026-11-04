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

Response deserialization uses shared `JsonSerializerOptions.Web` defaults unless
the caller supplies options. Both `GetResultFromJson<T>` overloads forward those
options. String enums require a serialization attribute or a caller-provided
`JsonStringEnumConverter<TEnum>`; custom converters and `[JsonConstructor]` follow
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

These are recommendations, not completed features or approved public API designs.
Effort is relative: small means a focused implementation with regression tests;
medium means several related APIs or lifetime behavior must be reviewed together.

| Priority | Change and location | Benefit / acceptance check | Effort and decision |
| --- | --- | --- | --- |
| Q1 | Consistent JSON options in `BODY<T>` and `RESPONSE_BODY<T>` as well as extraction | A caller's naming policy and string-enum converters work in requests, expectations and extraction; existing defaults remain unchanged | Small; agree overloads or a scenario-level configuration object before implementation |
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
| N3 | Expose a host-independent HTTP entry point using client creation; complete Q1 | Agree public names and whether N2 ownership is explicit in options/factories | Existing WebApplicationFactory scenarios still work; identical scenarios work with externally configured HttpClient creation; xUnit v3 tests pass |
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
   already accept `Func<HttpClient>`. Introduce a host-independent entry point
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
