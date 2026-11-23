# Ogooreck

Sneaky testing library for readable, self-documenting Given / When / Then tests. See `README.md` for what it does. Project structure, setup, code style, and the tests and documentation sections follow `CONTRIBUTING.md`. Keep them in sync with that file.

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

## Tests and documentation

Tests use xUnit v3, and the F# samples add FsCheck properties. Each test reads as a Given / When / Then scenario through Ogooreck's own API, the same way calling code uses it. The sample test projects are both tests and documentation.

`README.md` is generated. Its code samples come from the sample tests through MarkdownSnippets, so edit `mdsource/README.source.md` and the tests, never `README.md` directly. The build regenerates `README.md`, and so does `./build.sh docs`.

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
