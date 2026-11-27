# Leftovers after step 8: MCP span attributes, MCP test-run speed, coverage per library

Revision: 1

Parent spec: `../spec.md` (r16). Q&A: `../qa.md`, entries 113 and 114, and the entries this work adds there.

## Problem Statement

Three things stand between the current working tree and a green `./build.sh test` that Oskar accepts:

- ogooreck-mcp drops every span attribute whose value is an array. `OtlpSpans.ValueOf` reads strings, booleans, integers and doubles and skips the rest, so a span with `own.labels = ["first", "second"]` loses `own.labels`, and ogooreck-mcp's own `test_run` span loses `ogooreck.mcp.arguments`. A test even guards the loss (`FindSpansTests.cs:52`). Oskar: "Definitely we cannot lose arrays".
- `Ogooreck.Mcp.Tests` takes 4 min 28 s of the 7 min 37 s build. The suite makes 36 sequential `ogooreck_test_run` calls, and each `dotnet test` of the fixture spent about 5.5 s of its 6.8 s building it (qa 114, measured before the CLI telemetry opt-out). Oskar: "not acceptable, why would it need to do it?", and "use Ogooreck MCP. if it doesn't give you enough information think on what could be added to help you investigate such issues", "even for test-mcp".
- The coverage gate now checks each library from its own test projects (qa 114 decision) and fails: `Ogooreck` lines 2126/2162, branches 985/999; `Ogooreck.Kafka` branches 87/88. The other libraries are at 100%. `Ogooreck.TUnit.Tests` counts for `Ogooreck` by that decision, but its coverage isn't collected: it runs on Microsoft.Testing.Platform, which `coverlet.msbuild` doesn't support.

## Solution

Keep array attributes as JSON arrays in what ogooreck-mcp returns, and match their items. Find with ogooreck-mcp, connected as an MCP server, why a test run rebuilds an unchanged project, and remove the cause. If its spans can't show where a run spends its time, add what's missing to ogooreck-mcp first. Collect TUnit's coverage and close every coverage gap with real scenarios through the public API, or by removing a branch no public call can reach.

## Acceptance criteria

### Span attributes

1. An agent sees every attribute a span carries, so an investigation doesn't miss data.
    1.1. GIVEN a test that sets `own.labels` to `new[] { "first", "second" }` on its span WHEN `ogooreck_find_spans` returns that span THEN its attributes hold `own.labels` as the JSON array `["first","second"]`, and the string, boolean and double attributes stay as they are.
    1.2. GIVEN the same span WHEN `ogooreck_find_spans` runs with the pattern `^second$` THEN it returns the span, because an array attribute matches when one of its items matches.

### Test-run speed

2. An agent gets test results through ogooreck-mcp without waiting for work that isn't needed, so the feedback loop stays short.
    2.1. GIVEN a test project that is built and unchanged WHEN `ogooreck_test_run` runs it THEN the run compiles nothing.
    2.2. GIVEN an `ogooreck_test_run` WHEN the agent reads the run's spans THEN they show how long each `dotnet` process the run started took. This holds only if Oskar approves it (Q2 below).

### Coverage

3. `Ogooreck.TUnit.Tests` counts for the coverage of `Ogooreck`, as the qa 114 decision lists it.
    3.1. GIVEN `./build.sh test` WHEN `test-tunit` runs THEN its coverage of `Ogooreck` is merged with the other projects' coverage of `Ogooreck` before the gate checks it.
4. The configuration API of `Ogooreck` is covered by `Ogooreck`'s own test projects.
    4.1. GIVEN `./build.sh test` WHEN the gate checks `Ogooreck` THEN `ApiSpecification.Generic.cs` (`Recording` with an around-step function, `CatchUp`, `AppServices` before and after the host starts) and `SpecificationPluginRegistration.cs` (`Recording` with a catch-up function or an around-step function, `Appending` with and without a catch-up function) have no uncovered line or branch.
5. The steps of `Ogooreck` are covered by `Ogooreck`'s own test projects.
    5.1. GIVEN `./build.sh test` WHEN the gate checks `Ogooreck` THEN `ApiSpecification.cs`, `TestStep.cs` and `WhenApiSpecificationBuilder.cs` (`Eventually` of a step that isn't a request, `Eventually` in Given, re-sending until the response expectations pass, the deadline of `Eventually`, `Publish` without a publish function, cancellation while catching up) have no uncovered line or branch.
6. The rest of `Ogooreck` is covered by `Ogooreck`'s own test projects.
    6.1. GIVEN `./build.sh test` WHEN the gate checks `Ogooreck` THEN `ApiSpecification.Messages.cs`, `ServiceCollectionDecoration.cs`, `InProcessTelemetry.cs` and `StructuralComparison.cs` have no uncovered line or branch, and the gate passes for `Ogooreck`.
7. `Ogooreck.Kafka` is covered by its own test project.
    7.1. GIVEN `./build.sh test` WHEN the gate checks `Ogooreck.Kafka` THEN `KafkaGroups.cs:16` has no uncovered branch, and the gate passes for `Ogooreck.Kafka`.

## Implementation Decisions

- Array attributes: each item is turned into a string the way a scalar is today (strings as they are, booleans `true`/`false`, numbers in invariant culture), so an array of strings and an array of numbers both come back as an array of strings. Scalars stay strings, as `FindSpansTests` asserts today.
- Coverage (qa 114): every library has 100% line and branch coverage from the test projects that test it. `Ogooreck` from `Ogooreck.Tests`, `Ogooreck.Api.Tests`, `Ogooreck.BusinessLogic.Tests`, `Ogooreck.BusinessLogic.FSharp.Tests`, `Ogooreck.NUnit.Tests`, `Ogooreck.MSTest.Tests`, `Ogooreck.TUnit.Tests` and `Ogooreck.Aspire.Tests`; `Ogooreck.Wolverine` from `Ogooreck.Wolverine.Tests` and `Ogooreck.Wolverine.FSharp.Tests`; `Ogooreck.Marten`, `Ogooreck.Kafka`, `Ogooreck.RabbitMQ` and `Ogooreck.Mcp` each from their own test project. Gaps are closed with real scenarios through the public API, or by removing a branch no public call can reach. Never with a test that only exists to reach a line.
- Investigation: slowness and failures are traced through ogooreck-mcp, connected as an MCP server in the session (`.mcp.json`, server `ogooreck`), before any code is read or changed. Each cause is backed by span evidence.
- Locally only `./build.sh test` runs, on .NET 11. .NET 10 runs in CI only.

## Testing Decisions

- Tests follow `../spec.md` "Testing Decisions": public API only, real sample apps and infrastructure, no mocking library, test-first.
- The coverage criteria (3-7) are checked by the gate's output: `Ogooreck coverage: lines x/x, branches y/y` with no `not fully covered` line.

## Open questions

- Q1. TUnit coverage (criterion 3): `coverlet.msbuild` can't collect a Microsoft.Testing.Platform run. Which collector: coverlet's MTP extension (`coverlet.MTP`) or Microsoft's `Microsoft.Testing.Extensions.CodeCoverage`? Recommendation: `coverlet.MTP`, because it writes the same json and cobertura files, so the gate merges it with `MergeWith` as today. To verify with a spike before the step.
- Q2. Criterion 2.2: with `DOTNET_CLI_TELEMETRY_OPTOUT=1`, the `dotnet-cli` spans that showed the build time are gone, so ogooreck-mcp's spans no longer show where a run spends its time. Recommendation: ogooreck-mcp starts a span per `dotnet` process it runs (`dotnet msbuild`, `dotnet --version`, `dotnet test`), with the arguments and the exit code.
- Q3. `KafkaGroups.cs:16` filters groups in state `Empty` or `Dead`; no test sees a `Dead` group. Recommendation: cover it with a scenario where a consumer group is deleted through the admin client while the app runs, if the broker reports it as `Dead` in the listing; if it never does, remove `Dead` from the filter.
- Q4. The MCP server's behaviour is decided in `../qa.md` 106 to 114 but isn't in `../spec.md`. Go back to Q&A and write it into the spec, or keep it in qa.md?

## Out of Scope

Parked follow-ups, each Oskar's call later:

- One test project with subfolders instead of a dozen sample test projects; test apps named like `TestWebApi`, not `Sample` (qa 113).
- `.mcp/server.json` in the MCP package for MCP registries (qa 113).
- A receiver span per received metrics export (qa 114).
- The Aspire AppHost's `OTEL_METRIC_EXPORT_INTERVAL=500` without a measured reason (qa 114).
- `IDerivedPlugin`, unused usings, `MIGRATION.md` (step 6c of `../plan.md`).
- Proving `test-skill`'s failure checks by breaking the packed skill once by hand.
