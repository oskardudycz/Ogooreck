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
├── Ogooreck.Tests/                              library tests
├── Ogooreck.Sample.Api/                         sample API used by the API tests
├── Ogooreck.Sample.Api.Tests/                   API testing samples
├── Ogooreck.Sample.BusinessLogic.Tests/         C# business logic testing samples
└── Ogooreck.Sample.BusinessLogic.FSharp.Tests/  F# business logic testing samples
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

## Tests and documentation

Tests use [xUnit v3](https://xunit.net/), and the F# samples add [FsCheck](https://fscheck.github.io/FsCheck/) properties. Each test reads as a Given / When / Then scenario through Ogooreck's own API, the same way calling code uses it. The sample test projects are both tests and documentation: [`BankAccountTests`](./src/dotnet/Ogooreck.Sample.BusinessLogic.Tests/Deciders/BankAccountTests.cs) is a good place to start.

The `README.md` is generated. Its code samples come from the sample tests through [MarkdownSnippets](https://github.com/SimonCropp/MarkdownSnippets), so edit [`mdsource/README.source.md`](./mdsource/README.source.md) and the tests, never `README.md` directly. The build regenerates `README.md`, and so does `./build.sh docs`.

## Releases

Each package releases on its own tag. To publish the .NET package to NuGet, push a `dotnet-X.Y.Z` tag, or run the **.NET Release** workflow by hand with the version.

## Licensing and legal rights

By contributing to Ogooreck:

1. You assert that contribution is your original work.
2. You assert that you have the right to assign the copyright for the work.

## Code of Conduct

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community.
