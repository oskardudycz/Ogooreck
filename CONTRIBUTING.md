# Contributing to Ogooreck

We take Pull Requests!

## Before you send Pull Request

1. Contact the maintainers via the [Discord channel](https://discord.gg/fTpqUTMmVa) or the [Github Issue](https://github.com/oskardudycz/Ogooreck/issues/new) to make sure that this issue or bug should be handled in the proposed way. Send details of your case and explain the details of the proposed solution.
2. Once you get approval from one of the maintainers, you can start to work on your code change.
3. After your changes are ready, make sure that you covered your case with automated tests and verify that you have limited the number of breaking changes to a bare minimum.
4. We also highly appreciate any relevant updates to the documentation.
5. Make sure that your code is compiling and all automated tests are passing.

## After you have sent Pull Request

1. Make sure that you applied or answered all the feedback from the maintainers.
2. We're trying to be as responsive as we can, but if we didn't respond to you, feel free to ping us on the [Discord channel](https://discord.gg/fTpqUTMmVa).
3. Pull request will be merged when you get approvals from at least one of the maintainers (and no rejection from others).

## Setup your work environment

Ogooreck is a monorepo with one package per language under [`src/`](./src/). Each package is a separate build with its own CI workflow, which runs only when that package changes.

### .NET

The **.NET** package is located in [`src/dotnet`](./src/dotnet).

The quickest start is the **dev container**. It ships with the .NET SDK, the recommended IDE extensions, and the Git hooks already configured:

1. Install [Docker](https://docs.docker.com/engine/install/) and [VS Code](https://code.visualstudio.com/) with the **Dev Containers** extension.
2. Open the repo and run **Dev Containers: Reopen in Container**, then pick **ogooreck-dotnet**.

To work on your own machine instead, install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Then build and test from the package:

```shell
cd src/dotnet
./build.sh test
```

On Windows, use `build.cmd` or `build.ps1` instead of `build.sh`.

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

These are also available as VS Code tasks (_Ctrl+Shift+P → Run Task_). **compile** is the default build (Ctrl+Shift+B).

### Editor setup

**VS Code** - open the workspace file rather than the folder: `code ogooreck.code-workspace`. Accept the recommended extensions when prompted (or run the **Install All Recommended Extensions** task). Saving a C# file formats it, sorts the `using` directives and applies the fixable `.editorconfig` style rules, the same rules `dotnet format` checks in the build. Saving an F# file formats it with the Fantomas version from the tool manifest, through Ionide.

**Rider** and **Visual Studio** - open [`src/dotnet/Ogooreck.slnx`](./src/dotnet/Ogooreck.slnx). Both read the `.editorconfig`.

## Project structure

The repository groups source by language:

```
src/
└── dotnet/   the .NET package
```

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
├── Ogooreck.Mcp.Fixture/                        test project the MCP tests run: passing, failing, skipped and hanging scenarios
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
- a section in this file and in `AGENTS.md`.

The agent change-check hooks already recognize a Gradle build (a folder with `gradlew`) as a package, so a JVM package gets formatted and compiled after agent edits without further setup.

## Working with the Git

1. Fork the repository.
2. Create a feature branch from the `main` branch.
3. We're not squashing the changes and using rebase strategy for our branches (see more in [Git documentation](https://git-scm.com/book/en/v2/Git-Branching-Rebasing)). Having that, we highly recommend using clear commit messages. Commits should also represent the unit of change.
4. Before sending PR, make sure that you rebased the latest `main` branch from the main Ogooreck repository.
5. When you're ready, create the [Pull Request on GitHub](https://github.com/oskardudycz/Ogooreck/compare).

## Code style

### .NET code style

The [`.editorconfig`](./.editorconfig) is the single source of truth for style. [`dotnet format`](https://learn.microsoft.com/dotnet/core/tools/dotnet-format) applies it to C#, and [Fantomas](https://fsprojects.github.io/fantomas/) to F#. F# lines stay within 80 characters, because the README shows the F# samples and MarkdownSnippets rejects longer lines. The editor integrations and the pre-commit hook use the same config.

The pre-commit hook in [`.githooks`](./.githooks) runs `dotnet format` on the staged C# files and Fantomas on the staged F# files before each commit, so style stays consistent without you thinking about it. The first build turns it on for you: [`Directory.Build.props`](./src/dotnet/Directory.Build.props) points Git at the shared hooks (`git config core.hooksPath .githooks`).

`./build.sh test`, which CI runs, starts with `dotnet format --verify-no-changes` and `dotnet fantomas --check .`. A change that skipped the hook fails there and shows the file and line to fix.

The build treats warnings as errors and enables nullable reference types for all projects ([`Directory.Build.props`](./src/dotnet/Directory.Build.props)), so annotate nullable references explicitly.

### Code organization

Group code by feature, not by technical role. A reader should find a feature, its tests and its helpers by following the same folder names.

- **Libraries.** Each feature gets a folder: `Ogooreck/API/Plugins`, `API/Steps`, `API/Responses`, `API/Hosts`, `API/Messages` and `API/Telemetry`, with the engine that runs a scenario in `API/Running`. The plugin packages do the same, for example `Ogooreck.Kafka/Decorating` and `Ogooreck.Kafka/Consuming`. `Ogooreck.Mcp` has one folder per tool (`Running`, `SlowestSteps`, `FindingSpans`, `ScenarioDiagrams`, `ExportingSpans`), each holding the tool and the records it returns, and keeps what the tools share in `Spans`. A library keeps one namespace across its folders (`Ogooreck.API`, `Ogooreck.Kafka`, `Ogooreck.Mcp`), so moving a file never changes the public API.
- **Samples.** A sample app is split into modules, then slices: `Carts/`, then `Carts/GettingCart/`. A module's `Configuration.cs` registers its services and maps its endpoints, and `Program.cs` only composes the modules. Code that knows nothing about the business, such as broker options or a publisher, lives in `Core/`. The messages an app puts on the bus live together in `Events/ShopEvents.cs`, grouped by module, never one file per event. Keep small types, such as a request, a command or a view, in the file of the slice that uses them. Namespaces follow the folders, and a class never shares its name with its module (the store of `Carts` is `CartStore`).
- **Tests.** A test project mirrors the folders of the code it tests, and its namespaces follow its folders: the tests of `Ogooreck/API/Plugins` live in `Ogooreck.Tests/API/Plugins`, and the MCP tests of `Ogooreck.Mcp/SlowestSteps` in `Ogooreck.Mcp.Tests/SlowestSteps`. Tests of a package's root type sit at the test project's root and are named after it, for example `WolverinePluginTests`.
- **Test helpers.** A helper used by one test class sits next to it, or inside it when it's small. A helper shared by several test classes goes to a `Fixtures/` folder in the closest folder that covers all its users, for example `API/Messages/Fixtures`. A helper shared across folders, such as the app under test or the requests of a sample, goes to the project's root `Fixtures/`. Name a helper file after what it holds: `ShopEvents.cs` for test events, `KafkaBroker.cs` for the broker fixture.

## Tests, coverage, and documentation

Alongside formatting, `./build.sh test` checks two more things: full test coverage and a documented public API. Both run in CI, so a pull request that drops either won't go green. The existing tests and comments show what's expected, so neither needs to slow you down.

### Tests and the README

Tests use [xUnit v3](https://xunit.net/), and the F# samples add [FsCheck](https://fscheck.github.io/FsCheck/) properties. Each test reads as a Given / When / Then scenario through Ogooreck's own API, the same way calling code uses it. The sample test projects are both tests and documentation: [`BankAccountTests`](./src/dotnet/Ogooreck.BusinessLogic.Tests/Deciders/BankAccountTests.cs) is a good place to start.

The `README.md` is generated. Its code samples come from the sample tests through [MarkdownSnippets](https://github.com/SimonCropp/MarkdownSnippets), so edit [`mdsource/README.source.md`](./mdsource/README.source.md) and the tests, never `README.md` directly. The build regenerates `README.md`, and so does `./build.sh docs`.

### 100% line and branch coverage

`./build.sh test` requires 100% line and branch coverage of the `Ogooreck`, `Ogooreck.Wolverine`, `Ogooreck.Marten`, `Ogooreck.Kafka` and `Ogooreck.RabbitMQ` libraries and of the `Ogooreck.Mcp` server, collected by [coverlet](https://github.com/coverlet-coverage/coverlet) from all test projects and merged, and the build program checks the result in [`Program.cs`](./src/dotnet/Ogooreck.Build/Program.cs). This does more than confirm the tests run the code they're meant to. It also surfaces code no test can reach. A branch you can't cover is often one that can't execute: a null check on a value that's never null, or a `default` case the type already rules out. When that's the case, the better fix is usually to remove the branch, not to write a test that only satisfies the check. So treat a coverage gap as a question about the code first, and the test second.

We reach the coverage from the outside in. Tests drive the public API the same way calling code does: `Spec.Given(...).When(...).Then(...)`. The internals are covered because real usage runs through them, not because a test reaches into an internal method. Tests written this way survive refactors and read as examples of the API. [`BankAccountTests`](./src/dotnet/Ogooreck.BusinessLogic.Tests/Deciders/BankAccountTests.cs) is a good place to start for business logic, and [`CausationTests`](./src/dotnet/Ogooreck.Api.Tests/API/Messages/CausationTests.cs) for API specifications.

### Documented public API

The public API is what other code is written against. Its documentation carries the intent a signature can't: what a type is for, when you'd reach for it, how the pieces fit. That's why we document all of it.

The build treats the missing XML documentation warning (`CS1591`) as an error, so every public and protected member of the `Ogooreck`, `Ogooreck.Wolverine`, `Ogooreck.Marten`, `Ogooreck.Kafka` and `Ogooreck.RabbitMQ` libraries needs a `<summary>` comment or the build fails. Don't suppress the warning. Each comment is written for the reader calling the member. It opens with what the thing is and why you'd reach for it, then shows a short `<example><code>` taken from a real test where that helps. It doesn't restate the signature. The members already documented, such as `ExpectTrace` or `ExpectCausation`, are the easiest guide to the tone.

Writing the comment also tells you something about the API. A member you can't describe in a plain sentence is often one whose design is worth a second look while it's still new.

## Releases

Each package releases on its own tag. To publish the .NET package to NuGet, push a `dotnet-X.Y.Z` tag, or run the **.NET Release** workflow by hand with the version.

## Licensing and legal rights

By contributing to Ogooreck:

1. You assert that contribution is your original work.
2. You assert that you have the right to assign the copyright for the work.

## Code of Conduct

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community.
