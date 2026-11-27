# Plan: Leftovers after step 8

Revision: 1. Built from spec r1.

Spec: `spec.md`. Q&A: `../qa.md`.

## Terms

- **Gate**: the `check-coverage` target in `src/dotnet/Ogooreck.Build/Program.cs`. It reads `results/coverage/<library>/coverage.<tfm>.cobertura.xml` and fails unless lines and branches are fully covered.
- **Gap**: a line the gate prints as `not fully covered: <file>:<line>`.
- **Fixture**: `src/dotnet/Ogooreck.Mcp.Fixture`, the test project that `Ogooreck.Mcp.Tests` runs through `ogooreck_test_run`.
- **Connected ogooreck-mcp**: the `ogooreck` server from `.mcp.json`, approved and connected in a Claude Code session started in the repository root, called through its `ogooreck_*` tools.

Plan conventions: every step keeps `./build.sh test` (from `src/dotnet`, .NET 11) green except for the gate's remaining gaps, which shrink with each coverage step. .NET 10 runs in CI only. Tests are written first, name the observable behaviour and use the real sample apps; no mocking library. Each step records its questions, Oskar's answers verbatim and its findings in `../qa.md` as `## Q<n> (leftovers plan r1)`. Steps run in the main session, without subagents.

---

## Step 1. Keep array attributes

```text
Use the `implement-step` skill.

What to build
An agent that finds spans through ogooreck-mcp sees array attributes as JSON arrays and finds a span by one of the array's items. ogooreck-mcp's own `test_run` span keeps `ogooreck.mcp.arguments`.

Context
- `src/dotnet/Ogooreck.Mcp/Spans/OtlpSpans.cs`: `ValueOf` reads AnyValue fields 1 to 4 (tags 10, 16, 24, 33) and skips the array (field 5, tag 42, an ArrayValue whose field 1 holds AnyValues). Attributes are a `Dictionary<string, string>`.
- `src/dotnet/Ogooreck.Mcp/Spans/CollectedSpan.cs`: `IReadOnlyDictionary<string, string> Attributes`, read by key in `SourceFiles.cs`, `SpanTree.cs` and `ScenarioDiagram.cs`.
- `src/dotnet/Ogooreck.Mcp/FindingSpans/FindSpansTool.cs`: matches the regex against attribute keys and values; `FoundSpan.Attributes` is what the tool returns.
- `src/dotnet/Ogooreck.Mcp.Fixture/ProductTests.cs:49-50`: the fixture span with `own.done`, `own.retried`, `own.ratio` and `own.labels`.
- Item strings follow spec "Implementation Decisions": the same text a scalar of that type gets today.

Similar code
- `FindSpans_OutsideOfAnOgooreckRun_HaveNoTestOrLocation` in `src/dotnet/Ogooreck.Mcp.Tests/FindingSpans/FindSpansTests.cs` for the fixture run and the attribute asserts.

Blocked by
None

Existing tests this step breaks, and how it updates them
- `FindSpansTests.cs:52` `Assert.DoesNotContain("own.labels", span.Attributes.Keys)` guards the loss; it becomes the assert of 1.1.

Acceptance criteria
- 1.1. GIVEN a test that sets `own.labels` to `new[] { "first", "second" }` on its span WHEN `ogooreck_find_spans` returns that span THEN its attributes hold `own.labels` as the JSON array `["first","second"]`, and the string, boolean and double attributes stay as they are.
- 1.2. GIVEN the same span WHEN `ogooreck_find_spans` runs with the pattern `^second$` THEN it returns the span, because an array attribute matches when one of its items matches.

Tests
- 1.1: FindSpans_OutsideOfAnOgooreckRun_HaveNoTestOrLocation (updated) - asserts `own.labels` is the array `["first","second"]` and `own.done` "true", `own.retried` "false", `own.ratio` "0.5" stay strings
- 1.2: FindSpans_MatchTheItemsOfAnArrayAttribute - asserts the pattern `^second$` returns the `own work` span, and `^first","second$` returns nothing (items are matched one by one, not as joined text)
- extra: FindSpans_ExportedToAFile_KeepArrayAttributes - covers the `filename` path of `ogooreck_find_spans` writing `own.labels` as an array
- extra: ExportSpans_WriteArrayAttributesAsArrays - covers `ogooreck_export_spans` (JSON of the collected spans) writing `own.labels` as an array (follow `ExportSpansTests`)
- extra: ImportSpans_KeepArrayAttributes - covers `ogooreck_import_spans` (length-prefixed OTLP files, decoded by `OtlpSpans`) keeping `own.labels` of an imported fixture run (follow `ImportSpansTests`)
```

---

## Step 2. Test runs that don't rebuild an unchanged project

```text
Use the `implement-step` skill.

What to build
An agent that runs an unchanged, built test project through `ogooreck_test_run` doesn't wait for a build. `Ogooreck.Mcp.Tests` gets shorter by the time those builds took.

Context
- Start with the connected ogooreck-mcp, not code: `ogooreck_test_run` on `src/dotnet/Ogooreck.Mcp.Tests` with `["--framework", "net11.0"]`, then `ogooreck_slowest_steps`, `ogooreck_find_spans` and `ogooreck_export_spans`. Then run the fixture twice in a row through `ogooreck_test_run` and compare.
- If the spans can't show where a run spends its time (the `dotnet-cli` spans that showed it are gone since `DOTNET_CLI_TELEMETRY_OPTOUT=1`), stop and ask Oskar for Q2 of the spec before going on; with his yes, criterion 2.2 is part of this step.
- `src/dotnet/Ogooreck.Mcp/Running/TestRunner.cs`: starts `dotnet msbuild -getProperty`, `dotnet --version` (Microsoft.Testing.Platform only) and `dotnet test`, with `MSBUILDDISABLENODEREUSE=1` and `DOTNET_CLI_TELEMETRY_OPTOUT=1`.
- `src/dotnet/Ogooreck.Mcp.Tests/Running/TestRunTests.cs`: `TestRun_OfADeterministicBuild_MapsTheSourcePathsToTheRepository` passes `-p:ContinuousIntegrationBuild=true`, which changes the fixture's build properties; the tests share the fixture through the `BuildsTheFixture` collection.
- The cause is not decided. Any fix removes it; never `--no-build` in tests or in `TestRunner`, since users run `ogooreck_test_run` on projects they just changed.

Similar code
- `TestRun_CollectsTheSpansOfTheTestsWithoutTheSpansOfTheDotnetCli` in `TestRunTests.cs` for a test that asserts on the run's spans.
- `../qa.md` 114 "Finding (why Ogooreck.Mcp.Tests takes 11 minutes)" for how the spans were read last time.

Blocked by
None. Needs Oskar to start Claude Code in the repository root and approve the `ogooreck` server.

Existing tests this step breaks, and how it updates them
- Not known before the trace. Record any change in `../qa.md` with its reason.

Acceptance criteria
- 2.1. GIVEN a test project that is built and unchanged WHEN `ogooreck_test_run` runs it THEN the run compiles nothing.
- 2.2. GIVEN an `ogooreck_test_run` WHEN the agent reads the run's spans THEN they show how long each `dotnet` process the run started took. This holds only if Oskar approves it (Q2 below).

Tests
- 2.1: TestRun_OfABuiltUnchangedProject_CompilesNothing - asserts the second of two runs of the fixture, with nothing changed between them, leaves the fixture's `bin` assembly with the same last-write time
- 2.2 (with Q2 approved): TestRun_HasASpanForEachDotnetProcessItStarted - asserts the run's spans hold one span per started `dotnet` process with its arguments, exit code and duration, for a VSTest run (`msbuild`, `test`) and a Microsoft.Testing.Platform run (`msbuild`, `--version`, `test`)
- extra: TestRun_AfterASourceChange_RunsTheChangedCode - covers that a changed project is still built: a test project copied into a temporary folder, changed between two runs, reports the changed result
- extra: the suite's time before and after, from the `test-mcp` line of `./build.sh test`, recorded in `../qa.md`
```

---

## Step 3. TUnit coverage counts for Ogooreck

```text
Use the `implement-step` skill.

What to build
`./build.sh test` merges `Ogooreck.TUnit.Tests`'s coverage of `Ogooreck` with the other projects' before the gate checks it.

Context
- Needs Oskar's answer to spec Q1 and a spike of the chosen collector first, recorded in `../qa.md`.
- `src/dotnet/Ogooreck.Build/Program.cs`: `test-tunit` runs `dotnet run --project Ogooreck.TUnit.Tests` with no coverage; `RunTests` passes `MergeWith` with `coverage.<tfm>.json` for VSTest projects.
- `src/dotnet/Ogooreck.TUnit.Tests/Ogooreck.TUnit.Tests.csproj`; `src/dotnet/Directory.Packages.props` (or the project file) for the pinned package version, exact and at least two weeks old.
- AGENTS.md (line 124) and CONTRIBUTING.md (line 164) list the test projects per library, without TUnit; add it.

Similar code
- `RunTests` in `Program.cs` for the output names and `MergeWith`.

Blocked by
Spec Q1.

Existing tests this step breaks, and how it updates them
- None.

Acceptance criteria
- 3.1. GIVEN `./build.sh test` WHEN `test-tunit` runs THEN its coverage of `Ogooreck` is merged with the other projects' coverage of `Ogooreck` before the gate checks it.

Tests
- 3.1: checked by hand once, then reverted: with the NUnit, MSTest and TUnit targets the only ones run before the gate for `Ogooreck`, the gate's `Ogooreck` line counts more covered lines than with NUnit and MSTest alone
- extra: the `test-tunit` target still fails the build when a TUnit test fails (checked by hand once, then reverted)
```

---

## Step 4. Cover the configuration API of Ogooreck

```text
Use the `implement-step` skill.

What to build
A user who writes their own plug, or configures recording and catch-up on the specification, has each overload shown by a passing scenario in `Ogooreck.Api.Tests`, which covers them for the gate.

Context
- Gaps: `API/ApiSpecification.Generic.cs` 200-203 (`Recording(recorded, read, aroundStep)`), 273-275 (`CatchUp(aroundStep)`), 377-382 (`AppServices` before the host started, and after); `API/Plugins/SpecificationPluginRegistration.cs` 89-91 (`Recording(recorded, read, catchUp)`), 106-108 (`Recording(recorded, read, aroundStep)`), 119-120 (`Appending(recorded, read)`), 132-134 (`Appending(recorded, read, catchUp)`). The plug libraries' tests used to cover these; they no longer count for `Ogooreck`.
- The API sample's recorders: `src/dotnet/Ogooreck.Api.Tests/Fixtures/` (`RecordedMessages`, `ShopRequests`).

Similar code
- `src/dotnet/Ogooreck.Api.Tests/RecorderByRoleTests.cs` (a plug appending around each step) and `SharedAppTests.cs` for plugs in the API sample.
- The Wolverine and Marten plugs for real uses of each overload: `src/dotnet/Ogooreck.Wolverine/`, `src/dotnet/Ogooreck.Marten/`.

Blocked by
None

Existing tests this step breaks, and how it updates them
- None.

Acceptance criteria
- 4.1. GIVEN `./build.sh test` WHEN the gate checks `Ogooreck` THEN `ApiSpecification.Generic.cs` (`Recording` with an around-step function, `CatchUp`, `AppServices` before and after the host starts) and `SpecificationPluginRegistration.cs` (`Recording` with a catch-up function or an around-step function, `Appending` with and without a catch-up function) have no uncovered line or branch.

Tests
- 4.1: RecordingAroundEachStep_WaitsForTheStepsMessagesBeforeTheExpectations - asserts `Recording(recorded, read, aroundStep)` on the specification: a message the app publishes after the response is checked by `ExpectPublished` without a fixed wait
- 4.1: CatchUpAroundEachStep_EndsWhenTheAppIsIdle - asserts `CatchUp(aroundStep)` holds `Then` until the around function returns
- 4.1: PlugRecordingWithACatchUp_ChecksMessagesPublishedAfterTheResponse - asserts `registration.Recording(recorded, read, catchUp)` in a plug
- 4.1: PlugRecordingAroundEachStep_ChecksMessagesPublishedAfterTheResponse - asserts `registration.Recording(recorded, read, aroundStep)` in a plug
- 4.1: PlugAppending_ChecksAppendedEvents_WithAndWithoutACatchUp - asserts `registration.Appending(recorded, read)` and `registration.Appending(recorded, read, catchUp)` feed `ExpectAppended`
- 4.1: PlugReadingAppServicesBeforeTheFirstStep_FailsSayingWhereToReadThem - asserts the "The host has not started yet" message
- extra: PlugReadingAppServicesInItsCatchUp_GetsTheRunningAppsServices - covers `AppServices` after the host started, on a root specification and on a variant made with `With(...)`
```

---

## Step 5. Cover the steps of Ogooreck

```text
Use the `implement-step` skill.

What to build
A user who uses `Eventually`, `Publish` or cancels a test in `Ogooreck.Api.Tests` sees each outcome shown by a scenario, which covers the steps for the gate.

Context
- Gaps: `API/ApiSpecification.cs` 91 (`Eventually` of a step that isn't a request); `API/Steps/TestStep.cs` 63 (`Publish` without a publish function); `API/Steps/WhenApiSpecificationBuilder.cs` 64-66 (`Eventually` in Given), 102 (response expectations with `Eventually`), 187-196 (re-sending until the response expectations pass, and the deadline), 221 (cancelled while catching up after the step), 232-244 (`ResponseFailureOf`).
- The API sample's handler can be held: `OrderHandling.Hold(cartId)` (see `../progress.md`, step 8 review fixes), so a read model updates after the response without a fixed wait.
- `Eventually` tests exist only in the Wolverine sample today.

Similar code
- `src/dotnet/Ogooreck.Wolverine.Tests/` `EventuallyTests` for the `Eventually` scenarios and their messages.
- The cancellation tests in `Ogooreck.Api.Tests` that cancel inside a step (`../progress.md`, step 8 review fixes).

Blocked by
None

Existing tests this step breaks, and how it updates them
- None.

Acceptance criteria
- 5.1. GIVEN `./build.sh test` WHEN the gate checks `Ogooreck` THEN `ApiSpecification.cs`, `TestStep.cs` and `WhenApiSpecificationBuilder.cs` (`Eventually` of a step that isn't a request, `Eventually` in Given, re-sending until the response expectations pass, the deadline of `Eventually`, `Publish` without a publish function, cancellation while catching up) have no uncovered line or branch.

Tests
- 5.1: Eventually_ResendsTheRequestUntilTheReadModelShowsTheChange - asserts a `GET` that first returns the old state passes once the held handler is released
- 5.1: Eventually_StillFailingAtTheDeadline_SaysHowManyAttemptsAndTheLastResponse - asserts the deadline message with a short deadline (`With(s => s.Deadline(...))`)
- 5.1: Eventually_OfAnAction_IsAnArgumentError - asserts the "Eventually(...) takes a Request(...) step" message
- 5.1: Eventually_InGiven_FailsSayingItBelongsInWhen - asserts the "allowed only as the When step" message
- 5.1: Publish_WithoutAPublishFunction_FailsSayingHowToConfigureIt - asserts the "needs a publish function" message
- 5.1: Then_CancelledWhileCatchingUp_SaysItWasWaitingForCatchUp - asserts the "cancelled while waiting for catch-up after the When step" message, cancelled from a catch-up that never ends
- extra: Eventually_WithOnlyMessageExpectations_SendsOnce - covers 102 when no expectation checks the response
```

---

## Step 6. Cover the rest of Ogooreck

```text
Use the `implement-step` skill.

What to build
The remaining behaviours of `Ogooreck` that only other libraries' tests reached get scenarios in `Ogooreck.Api.Tests` or `Ogooreck.Tests`, or the unreachable branch is removed. After this step the gate passes for `Ogooreck`.

Context
- Gaps: `API/Messages/ApiSpecification.Messages.cs` 46 (`ExpectNothingPublishedIn(recorded)`), 192-196 (a message of the expected type with different content); `API/Messages/ServiceCollectionDecoration.cs` 37 (`Decorate` skips keyed registrations of the same type); `API/Telemetry/InProcessTelemetry.cs` 22 (`SampleUsingParentId`, an activity started with a string parent id); `Assertions/StructuralComparison.cs` 266 (a decimal or a float/double that is zero against one that isn't).
- For each gap, first check whether a public call reaches it. If not, remove the branch and say so in `../qa.md`.

Similar code
- `src/dotnet/Ogooreck.Api.Tests/RecorderByRoleTests.cs` for `ExpectNothingPublishedIn`; the `StructuralComparison` characterization tests in `Ogooreck.Tests` (qa 113, decimal against float).

Blocked by
None

Existing tests this step breaks, and how it updates them
- None.

Acceptance criteria
- 6.1. GIVEN `./build.sh test` WHEN the gate checks `Ogooreck` THEN `ApiSpecification.Messages.cs`, `ServiceCollectionDecoration.cs`, `InProcessTelemetry.cs` and `StructuralComparison.cs` have no uncovered line or branch, and the gate passes for `Ogooreck`.

Tests
- 6.1: ExpectNothingPublishedIn_ARecorderTheAppPublishedTo_FailsListingTheMessages - asserts the named form's failure message
- 6.1: ExpectPublished_OfTheRightTypeWithOtherContent_NamesTheDifferingField - asserts the field difference in the failure message
- 6.1: Decorate_DecoratesTheUnkeyedRegistration_AndLeavesKeyedOnesAlone - asserts a keyed registration of the same type stays undecorated
- 6.1: ExpectTrace_SeesASpanStartedWithAStringParentId - asserts the span is recorded and checked
- 6.1: Equivalent_ZeroDecimalAndNonZeroDouble_AreNotEquivalent - asserts `0m` against `0.5d` fails and `0m` against `0d` passes
```

---

## Step 7. Cover Ogooreck.Kafka

```text
Use the `implement-step` skill.

What to build
The Kafka plug's wait for consumer groups is shown working for the group state no test sees today, or the state is dropped from the filter. After this step the gate passes for `Ogooreck.Kafka`.

Context
- Needs Oskar's answer to spec Q3.
- `src/dotnet/Ogooreck.Kafka/Decorating/KafkaGroups.cs:16`: `group.State is not (ConsumerGroupState.Empty or ConsumerGroupState.Dead)`; no test lists a `Dead` group.
- `src/dotnet/Ogooreck.Kafka.Tests/Fixtures/KafkaBroker.cs` for the admin client and the broker.

Similar code
- The Kafka catch-up tests that list consumer groups in `src/dotnet/Ogooreck.Kafka.Tests/`.

Blocked by
Spec Q3.

Existing tests this step breaks, and how it updates them
- None.

Acceptance criteria
- 7.1. GIVEN `./build.sh test` WHEN the gate checks `Ogooreck.Kafka` THEN `KafkaGroups.cs:16` has no uncovered branch, and the gate passes for `Ogooreck.Kafka`.

Tests
- 7.1 (if covered): CatchUp_WithADeletedConsumerGroup_DoesNotWaitForIt - asserts catch-up ends while a deleted group is listed
- 7.1 (if removed): the existing Kafka catch-up tests stay green and the reason is in `../qa.md`
```

---

## Step 8. Close out

```text
What to do
- `./build.sh test` from `src/dotnet` on .NET 11 passes, gate included. Report the command, the result and the timings table.
- `../qa.md` holds this work's questions, Oskar's answers verbatim and the findings: the coverage merge that never merged (TFM-suffixed `coverage.<tfm>.json`), the Mcp and Aspire test projects that targeted net10.0 only, the array finding, the TUnit coverage, the test-mcp trace and its fix, Oskar's 7-minute question.
- Clean up and report what was removed: in the scratchpad `ci-build.log`, `full-build.log`, `net11-build.log`, `Program.cs.before`, `pack/`, `spans/`; `src/dotnet/Ogooreck.Mcp.Tests/TestResults/` if it is untracked output of these runs; leftover `Ogooreck.Mcp.Fixture` processes and containers started by the runs. Report the dnvm SDK in `~/.local/share/dnvm/dn` and leave it.
- Tick `todo.md`.

Blocked by
Steps 1 to 7.
```
