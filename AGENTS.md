# Ogooreck

Sneaky testing library for readable, self-documenting Given / When / Then tests. See `README.md` for what it does. Project structure, setup, code style, and the tests, coverage, and documentation sections follow `CONTRIBUTING.md`. Keep them in sync with that file.

## Project structure

The repository groups source by language:

```
src/
└── dotnet/   the .NET package
```

Each package is a separate build with its own CI workflow in `.github/workflows` (`<language>-ci.yml`), which runs only when that package changes. There is no root build: build and test one package at a time, from inside its folder.

### .NET package structure

```
src/dotnet/
├── Ogooreck.slnx                                the solution
├── Directory.Build.props                        nullable, warnings as errors, implicit usings
├── .config/dotnet-tools.json                    local tools: MarkdownSnippets, Fantomas
├── build.sh, build.cmd, build.ps1               run the build program below
├── Ogooreck.Build/                              build targets (Bullseye): compile, verify-format, test, pack, docs
├── Ogooreck/                                    the library, published to NuGet as Ogooreck
├── Ogooreck.Wolverine/                          the Wolverine plugin, published to NuGet as Ogooreck.Wolverine
├── Ogooreck.Marten/                             the Marten plugin, published to NuGet as Ogooreck.Marten
├── Ogooreck.Kafka/                              the Kafka plugin, published to NuGet as Ogooreck.Kafka
├── Ogooreck.RabbitMQ/                           the RabbitMQ plugin, published to NuGet as Ogooreck.RabbitMQ
├── Ogooreck.Mcp/                                MCP server that runs .NET tests and explains them with their spans, packed as the ogooreck-mcp dotnet tool
├── Ogooreck.Mcp.Tests/                          MCP server tests, run against the fixture below over stdio
├── Ogooreck.Mcp.Fixture/                        test project the MCP tests run: passing, failing and skipped scenarios
├── Ogooreck.Tests/                              library tests
├── Ogooreck.Sample.Api/                         sample API used by the API tests
├── Ogooreck.Api.Tests/                          API testing samples
├── Ogooreck.Sample.Wolverine/                   sample Wolverine app (in-memory, local queues) used by the messaging tests
├── Ogooreck.Wolverine.Tests/                    messaging samples: recorded messages, catch-up, actions and publishing
├── Ogooreck.Wolverine.FSharp.Tests/             F# messaging sample: the same scenario written in F#
├── Ogooreck.Sample.Aspire.Api/                  sample API hosted by Aspire, exporting traces and metrics over OTLP and producing to Kafka
├── Ogooreck.Sample.Aspire.AppHost/              Aspire app host with the API and Kafka
├── Ogooreck.Aspire.Tests/                       out-of-process samples: OtlpReceiver, ExpectTrace and ExpectMetric over OTLP (need Docker)
├── Ogooreck.Sample.Kafka/                       sample app that publishes to Kafka, used by the broker-side recording tests
├── Ogooreck.Kafka.Tests/                        Kafka samples (Testcontainers, needs Docker): messages without catch-up, causation
├── Ogooreck.Sample.Marten/                      sample Marten app (PostgreSQL) used by the appended events tests
├── Ogooreck.Marten.Tests/                       Marten samples (Testcontainers, needs Docker): appended events, projection catch-up
├── Ogooreck.NUnit.Tests/                        NUnit sample: one passing and one failing scenario
├── Ogooreck.MSTest.Tests/                       MSTest sample: one passing and one failing scenario
├── Ogooreck.TUnit.Tests/                        TUnit sample: one passing and one failing scenario (runs with dotnet run)
├── Ogooreck.BusinessLogic.Tests/                C# business logic testing samples
└── Ogooreck.BusinessLogic.FSharp.Tests/         F# business logic testing samples
```

### Adding another language

Each language gets its own package folder, for example `src/jvm`, with:

- its own build, run from inside that folder,
- `.github/workflows/<language>-ci.yml` that runs only on changes in `src/<language>/**`, and `<language>-release.yml` that publishes on `<language>-X.Y.Z` tags,
- a dev container in `.devcontainer/<language>/` and a dev container workflow,
- a folder entry in `ogooreck.code-workspace`, and recommended extensions in `.vscode/extensions.json`,
- a section in `CONTRIBUTING.md` and in this file.

The agent change-check hooks already recognize a Gradle build (a folder with `gradlew`) as a package. [Strictland](https://github.com/event-driven-io/strictland) has the same layout with a JVM package in `src/jvm`, so use it as the reference for the Gradle build, the JVM dev container and the JVM workflows.

## Setup

### .NET

The **.NET** package is located in [`src/dotnet`](./src/dotnet). It needs the .NET 10 SDK. The dev container in `.devcontainer/dotnet` ships with it.

#### Everyday commands

All from [`src/dotnet`](./src/dotnet):

```shell
./build.sh test                      # verify formatting, compile and run all tests, as CI does
dotnet build Ogooreck.slnx           # compile only - the fast inner loop
dotnet test Ogooreck.slnx            # run the tests
dotnet format Ogooreck.slnx          # format C# by hand, if you ever need to
dotnet fantomas .                    # format F# by hand, if you ever need to
./build.sh pack                      # build the NuGet package into artifacts/
./build.sh docs                      # regenerate README.md from mdsource/
```

### Additional commands

Run from `src/dotnet`:

```shell
dotnet test Ogooreck.Tests/Ogooreck.Tests.csproj --filter 'FullyQualifiedName~HttpExpectationsTests' # run one test class
```

## Code style

### .NET code style

The `.editorconfig` is the single source of truth for style. `dotnet format` applies it to C#, and Fantomas to F#. F# lines stay within 80 characters, because MarkdownSnippets rejects longer lines in README samples. The stop hook runs both in each package you edited (see "Verification"), and the pre-commit hook in `.githooks` runs them on the staged files. The build turns the pre-commit hook on (`Directory.Build.props`, skipped when `CI=true`), and `./build.sh test` fails on unformatted code (`dotnet format --verify-no-changes`, `dotnet fantomas --check .`).

The build treats warnings as errors and enables nullable reference types for all projects (`src/dotnet/Directory.Build.props`), so annotate nullable references explicitly.

### Code organization

Group code by feature, not by technical role. A reader should find a feature, its tests and its helpers by following the same folder names.

- **Libraries.** Each feature gets a folder: `Ogooreck/API/Plugins`, `API/Steps`, `API/Responses`, `API/Hosts`, `API/Messages` and `API/Telemetry`, with the engine that runs a scenario in `API/Running`. The plugin packages do the same, for example `Ogooreck.Kafka/Decorating` and `Ogooreck.Kafka/Consuming`. `Ogooreck.Mcp` has one folder per tool (`Running`, `SlowestSteps`, `FindingSpans`, `ScenarioDiagrams`, `ExportingSpans`), each holding the tool and the records it returns, and keeps what the tools share in `Spans`. A library keeps one namespace across its folders (`Ogooreck.API`, `Ogooreck.Kafka`, `Ogooreck.Mcp`), so moving a file never changes the public API.
- **Samples.** A sample app is split into modules, then slices: `Carts/`, then `Carts/GettingCart/`. A module's `Configuration.cs` registers its services and maps its endpoints, and `Program.cs` only composes the modules. Code that knows nothing about the business, such as broker options or a publisher, lives in `Core/`. The messages an app puts on the bus live together in `Events/ShopEvents.cs`, grouped by module, never one file per event. Keep small types, such as a request, a command or a view, in the file of the slice that uses them. Namespaces follow the folders, and a class never shares its name with its module (the store of `Carts` is `CartStore`).
- **Tests.** A test project mirrors the folders of the code it tests, and its namespaces follow its folders: the tests of `Ogooreck/API/Plugins` live in `Ogooreck.Tests/API/Plugins`, and the MCP tests of `Ogooreck.Mcp/SlowestSteps` in `Ogooreck.Mcp.Tests/SlowestSteps`. Tests of a package's root type sit at the test project's root and are named after it, for example `WolverinePluginTests`.
- **Test helpers.** A helper used by one test class sits next to it, or inside it when it's small. A helper shared by several test classes goes to a `Fixtures/` folder in the closest folder that covers all its users, for example `API/Messages/Fixtures`. A helper shared across folders, such as the app under test or the requests of a sample, goes to the project's root `Fixtures/`. Name a helper file after what it holds: `ShopEvents.cs` for test events, `KafkaBroker.cs` for the broker fixture.

## Tests, coverage, and documentation

Alongside formatting, `./build.sh test` checks two more things: full test coverage and a documented public API. Both run in CI, so a change that drops either won't go green. The existing tests and comments show what's expected.

### Tests and the README

Tests use xUnit v3, and the F# samples add FsCheck properties. Each test reads as a Given / When / Then scenario through Ogooreck's own API, the same way calling code uses it. The sample test projects are both tests and documentation.

`README.md` is generated. Its code samples come from the sample tests through MarkdownSnippets, so edit `mdsource/README.source.md` and the tests, never `README.md` directly. The build regenerates `README.md`, and so does `./build.sh docs`.

### 100% line and branch coverage

`./build.sh test` requires 100% line and branch coverage of the `Ogooreck`, `Ogooreck.Wolverine`, `Ogooreck.Marten`, `Ogooreck.Kafka` and `Ogooreck.RabbitMQ` libraries and of the `Ogooreck.Mcp` server, collected by [coverlet](https://github.com/coverlet-coverage/coverlet) from all test projects and merged, and the build program checks the result in [`Program.cs`](./src/dotnet/Ogooreck.Build/Program.cs). This does more than confirm the tests run the code they're meant to. It also surfaces code no test can reach. A branch you can't cover is often one that can't execute: a null check on a value that's never null, or a `default` case the type already rules out. When that's the case, the better fix is usually to remove the branch, not to write a test that only satisfies the check. So treat a coverage gap as a question about the code first, and the test second.

We reach the coverage from the outside in. Tests drive the public API the same way calling code does: `Spec.Given(...).When(...).Then(...)`. The internals are covered because real usage runs through them, not because a test reaches into an internal method. Tests written this way survive refactors and read as examples of the API. [`BankAccountTests`](./src/dotnet/Ogooreck.BusinessLogic.Tests/Deciders/BankAccountTests.cs) is a good place to start for business logic, and [`CausationTests`](./src/dotnet/Ogooreck.Api.Tests/API/Messages/CausationTests.cs) for API specifications.

### Documented public API

The public API is what other code is written against. Its documentation carries the intent a signature can't: what a type is for, when you'd reach for it, how the pieces fit. That's why we document all of it.

The build treats the missing XML documentation warning (`CS1591`) as an error, so every public and protected member of the `Ogooreck`, `Ogooreck.Wolverine`, `Ogooreck.Marten`, `Ogooreck.Kafka` and `Ogooreck.RabbitMQ` libraries needs a `<summary>` comment or the build fails. Don't suppress the warning. Each comment is written for the reader calling the member. It opens with what the thing is and why you'd reach for it, then shows a short `<example><code>` taken from a real test where that helps. It doesn't restate the signature. The members already documented, such as `ExpectTrace` or `ExpectCausation`, are the easiest guide to the tone.

Writing the comment also tells you something about the API. A member you can't describe in a plain sentence is often one whose design is worth a second look while it's still new.

## Agent skills

Shared skills live in `.agents/skills/<name>/SKILL.md`. That file is the canonical body, so edit the skill there.

Codex and OpenCode read `.agents/skills/` directly. Claude Code does not, so `.claude/commands/<name>.md` holds a thin pointer that reads the skill file. OpenCode also gets a pointer in `.opencode/commands/<name>.md`, which exposes the skill as a `/name` slash command.

When adding a skill, write the body in `.agents/skills/` with `name` and `description` in the front matter, then add both pointer files.

See `.agents/README.md` for how skills and the change-check hooks work. Run the hook tests from the repository root with `node --test .agents/hooks/*.spec.ts .opencode/plugins/*.spec.ts`.

## Verification

The completion hook formats and compiles each package edited in the session: `dotnet format`, `dotnet fantomas .` and `dotnet build` in a .NET package (a folder with `*.slnx`, today `src/dotnet`), `./gradlew spotlessApply` and `./gradlew testClasses` in a Gradle build (a folder with `gradlew`).

For documentation or agent-configuration changes, validate only the changed files and configuration. The package build is not required.

While changing library code, identify and run the smallest relevant set of tests covering the affected behavior. Before reporting a development phase as complete, run the same command as the package's CI workflow: `./build.sh test` from `src/dotnet`. If a build is interrupted, ask before running it again.

Report which verification commands were run, whether they passed, and why any relevant checks were skipped.

### Investigating a failing or hanging test

Start from the spans, not from the code. Ogooreck exists to explain test runs with telemetry, so use it on its own tests:

1. Run the failing test project through the Ogooreck MCP server (`Ogooreck.Mcp`, the `ogooreck-mcp` tool): `ogooreck_test_run`, then `ogooreck_find_spans`, `ogooreck_slowest_steps` or `ogooreck_export_spans`. For a hang, pass `--blame-hang-timeout`, so the run ends and names the test that was running.
2. Read what the spans show: which step ran, which spans overlap it, which span failed, and which spans are missing. A test that has no Ogooreck span at all hung before its first step.
3. State the cause with that evidence, for example "the `catch-up` span after `GET /orders` overlaps 14 `publish OrderPlaced` spans of other tests". Then write the failing test and the fix.

Read the code and the logs only after that, to explain what the spans show. If a run has no spans that explain the failure, say so, and say what telemetry was missing.

## Working rules

- Use simple, direct, straightforward language (STE English). Keep explanations concise, example-based, and focused on the problem. Simplify without oversimplifying.
- If a requirement or intended behavior is unclear, ask instead of assuming.
- Do not monkey-patch globals, prototypes, or third-party modules. If a change appears to require monkey-patching, stop and ask first.
- Prefer simple, easily removable code over premature abstractions. Do not introduce a new abstraction unless explicitly requested.
- Do not use comments inside the code unless asked. If you want to do it, review code and make it self-explanatory instead. XML documentation on the public API is the exception.
- Use a test-first approach for behavior changes. Name tests after observable behavior from the usage perspective, not implementation details.
- Treat questions, design discussions, and requests for opinions as read-only. Wait for explicit approval before editing files or running mutating commands.
- Never reverse a decision already agreed to (a requirement, a behaviour, a test that guards it) unless explicitly approved by that specific reversal. Ask user and explain tradeoffs.
- Plan to use subagents, when available, for independent, non-sequential work that can usefully run in parallel, such as research, repository exploration, focused implementation, verification, and review. Keep user interaction, cross-cutting decisions, and final synthesis in the main context. Ask for that usage before sending them. Don't start subagents with inherit context. Give subagents sufficient context and require concrete, evidence-backed findings so important details are not lost.
- Do not commit, create branches or tags, rebase, reset, or otherwise modify Git history unless explicitly requested.
- Do not add manual line breaks inside Markdown paragraphs. Keep each paragraph on a single line and let the renderer or editor wrap it.
