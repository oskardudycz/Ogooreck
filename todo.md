# Migration progress

- [x] Inspect all projects, public serializer/assertion APIs, build scripts and CI.
- [x] Verify solution, JSON, equivalence, xUnit and Aspire documentation.
- [x] Record user decisions: net10 only; ordered events; standard System.Text.Json.
- [x] Write implementation plan and subsequent host/Aspire modernization roadmap.
- [x] Capture baseline test results.
- [x] Verify compatible latest stable dependency versions.
- [x] Migrate solution, project targets, SDK selection, scripts and CI.
- [x] Implement repository-owned comparisons and focused regression tests.
- [x] Remove FluentAssertions from library and samples.
- [x] Update existing documentation examples to remove FluentAssertions.
- [x] Remove Newtonsoft and adopt configurable System.Text.Json deserialization.
- [x] Fix and test vacuous exception/body assertions, settings forwarding and ETag reader.
- [x] Modernize asynchronous composition and verify callback completion/failure.
- [x] Use shared JSON web defaults and supported object initialization.
- [x] Restore default JsonElement value equality and custom value-type behavior.
- [x] Build the full solution and run all C# and F# tests through the build helper.
- [x] Build release package and inspect dependency metadata.
- [x] Check outdated/vulnerable dependencies and record any exceptions explicitly.
- [x] Remove unsolicited README migration note, regenerate existing code examples
  with MarkdownSnippets, and inspect final diff.

## Evidence and notes

- Initial working tree was clean; no repository AGENTS.md instructions found.
- Baseline: `dotnet test src/Ogooreck.sln -c Release -f net8.0` passed 20 tests.
- NuGet feed lookup required approved network access after sandbox DNS failure.
- FSharp.UMX 1.1.0 is already current. FSharp.Core 11.0.100 targets .NET Standard
  and compiled/passed property tests with SDK 10.0.103.
- Replaced FsCheck.Xunit with the official FsCheck.Xunit.v3 3.6.0 integration.
  Earlier claims that FsCheck forced retaining v2 missed this package.
- First .NET 10 regression run exposed repeated JSON stream consumption. Kept
  HttpContent's buffered string reads before System.Text.Json deserialization to
  support multiple body assertions/extractions on the same response.
- Final `./build.sh test pack`: 145 tests passed (API 3, C# business logic 15,
  F# 5, library 122), with zero build warnings/errors. No duplicated v2/v3 suite.
- Produced `artifacts/Ogooreck.0.9.0.nupkg` and symbol package without publishing
  or changing the release version. Package targets net10.0 and has only
  Microsoft.AspNetCore.Mvc.Testing 10.0.14 as a direct package dependency.
- All seven current restored dependency graphs exclude Newtonsoft.Json and
  FluentAssertions. No redundant System.Text.Json package was added.
- `./build.sh docs` passes using the pinned local MarkdownSnippets tool. Affected
  C#/F# business-logic examples are extracted from executable tests; README is
  regenerated, not edited directly. README prose matches the original exactly.
- Existing pack advisory: package does not embed a README. Left package metadata
  unchanged because documentation expansion was not requested.
- Windows command/PowerShell wrappers were reviewed but not executed on Linux.
- Removed the solution build's explicit `--framework` argument: all projects
  now declare one TFM. The SDK removes that global property from single-target
  project references, causing duplicate builds and an MVC manifest write race
  when the solution supplied it globally. The normal solution build passes.
- Outdated direct-package and transitive-vulnerability audits refreshed on
  2026-11-04 with no reported packages across all seven current projects.
- User confirmed .NET 10 now; .NET 11 support after its general release.

## User corrections and comparison compatibility

- [x] Remove global.json; SDK selection follows the user's installed environment.
- [x] Remove the dedicated xUnit v3 project and the duplicated suite/compile symbol.
- [x] Migrate every existing C# and F# test project to xUnit v3.
- [x] Verify no xUnit v2 package in any current project dependency graph.
- [x] Move the real API compatibility scenario into the existing API test project.
- [x] Derive the dedicated AssertionException from InvalidOperationException.
- [x] Extract ordered event comparison into Expect.OrderedEquivalent.
- [x] Audit comparison semantics against the original FA 6.12.0 implementation.
- [x] Restore the characterized default rules; distinguish ordered event sequences.
- [x] Add 104 data-driven characterization cases; external audit reports zero
  pass/fail differences against the original DLL, with no restored FA dependency.
- [x] Complete final full-solution build/tests, package and audit checks.

The initial comparison implementation was not behavior-compatible: numeric and
cross-enum equality, custom Equals, declared types, cycles, recursion depth and
collection matching diverged. Removed the arbitrary depth-64 and cycle-success
rules, and the additional native JSON content comparison. Byte collections keep
the old ordered semantics. Event sequences alone use the explicitly ordered
assertion helper. F# union payloads follow their own Equals, as in the old engine.

All test projects use xunit.v3.mtp-off 4.0.2 with the existing VSTest adapter. No
xUnit runner/analyzer errors are suppressed. xUnit v3's cancellation token is used
directly, without a v2 conditional build. The library still has no xUnit dependency.
The README's existing IAsyncLifetime code signatures are updated to ValueTask for
v3; it is regenerated with MarkdownSnippets. No migration prose is added.

## Modernization follow-ups (planned, not implemented)

- [x] Expand plan.md with completed quick wins, Q1-Q5 proposals, N1-N6 stages,
  user decision points, acceptance checks and official documentation references.
- [ ] Q1: agree and implement consistent JSON configuration across helpers.
- [ ] Q2: agree and implement cancellation-aware JSON reads.
- [ ] Q3: decide whether strict JSON needs a convenience preset; retain web defaults.
- [ ] Q4: measure and cache comparison member metadata if worthwhile.
- [ ] Q5: decide whether to adopt central package management.
- [ ] N1: decide retry contracts; implement bounded retries and deterministic time.
- [ ] N2: decide resource ownership and recorded diagnostics; implement cleanup.
- [ ] N3: agree the host-independent HTTP API and adapt WebApplicationFactory.
- [ ] N4: agree host/scope ownership and deliver a Generic Host adapter.
- [ ] N5: deliver an Aspire sample; decide whether an adapter package is needed.
- [ ] N6: add .NET 11 after GA; decide release/package/runner changes separately.

## Resolved dependency versions

| Dependency | Version |
| --- | --- |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.14 |
| Bullseye | 6.3.0 |
| SimpleExec | 13.1.0 |
| Microsoft.NET.Test.Sdk | 18.11.0 |
| xunit.v3.mtp-off | 4.0.2 |
| xunit.runner.visualstudio | 4.0.1 |
| coverlet.collector | 10.1.0 |
| FsCheck / FsCheck.Xunit.v3 | 3.6.0 |
| FSharp.UMX | 1.1.0 (already current) |
| FSharp.Core | 11.0.100 (.NET Standard compatible) |
| MarkdownSnippets.MsBuild / MarkdownSnippets.Tool | 28.5.1 |
| actions/checkout | v7.0.1 |
| actions/setup-dotnet | v6.1.0 |
