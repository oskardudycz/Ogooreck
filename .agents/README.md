# Agent setup

The `.agents` folder holds the skills and hooks that coding agents use in this repository. Claude Code, Codex and OpenCode share one source here, so you write a skill or a check once and each agent gets it through a thin pointer or config.

## Skills

The skill body lives in `.agents/skills/<name>/SKILL.md`. Codex and OpenCode read it from there. Claude Code does not, so `.claude/commands/<name>.md` holds a pointer that tells it to read the skill file. OpenCode also gets a pointer in `.opencode/commands/<name>.md`, which gives the `/name` slash command.

To add a skill, write `SKILL.md` with `name` and `description` in the front matter. Then copy an existing pointer in both `commands` folders and change the name and description.

## Change check

Each language package is a separate build under `src/`. A package is the nearest folder below the repository root that holds either a Gradle wrapper (`gradlew`) or a .NET solution (`*.slnx`). Today that is the .NET solution in `src/dotnet`. When the agent edits a file, the hook finds its package, for example `src/dotnet` for a file in `src/dotnet/Ogooreck/API`, and records it for the session. When the agent stops, the hook formats and then compiles each recorded package:

| Package | Format                    | Compile                 |
| ------- | ------------------------- | ----------------------- |
| .NET    | `dotnet format`, then `dotnet tool restore` and `dotnet fantomas .` | `dotnet build` |
| Gradle  | `./gradlew spotlessApply` | `./gradlew testClasses` |

Edits outside any package, such as the root `README.md` or `.agents`, do not trigger a check. The hooks do not use `git diff`, because you can have your own uncommitted changes that the agent did not make.

```mermaid
flowchart TD
    A[Agent edits a file] --> B["Find the nearest folder with gradlew or *.slnx"]
    B --> C["Add the folder to the marker file for the session id"]
    C --> D[Agent stops or session goes idle]
    D --> E{Folders in the marker?}
    E -- no --> F[Done]
    E -- yes --> G["Format and compile each folder"]
    G -- all pass --> H[Delete the marker] --> F
    G -- some fail --> I["Keep only the failed folders and report the failure"]
    I -.->|next stop checks them again| D
```

The marker is a file at `.agents/.cache/agent-check/<session_id>` in the repository root (ignored by Git), with one folder per line. On failure, Claude Code and Codex get exit code 2 with the check output, which blocks the stop and gives the output back to the agent. OpenCode shows an error toast and writes the output to the OpenCode log.

The hook reads the edited paths from the tool input:

| Agent       | On edit                                                                                                                        | On stop                                   | Where configured                       |
| ----------- | ------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------- | -------------------------------------- |
| Claude Code | `PostToolUse` on `Edit\|Write\|MultiEdit\|NotebookEdit` runs `record-edit.ts`, which reads `file_path` or `notebook_path`       | `Stop` runs `validate-change.ts`          | `.claude/settings.json`                |
| Codex       | `PostToolUse` on `apply_patch\|Edit\|Write` runs `record-edit.ts`, which reads the patch file lines from `command`, relative to `cwd` | `Stop` runs `validate-change.ts`          | `.codex/hooks.json`                    |
| OpenCode    | `tool.execute.after` on `edit`, `write`, `apply_patch` reads `filePath` or `patchText`                                         | `session.status` event with `idle` status | `.opencode/plugins/validate-change.ts` |

OpenCode loads the plugin from `.opencode/plugins/`. The plugin keeps the edited folders per session in memory, so it does not write marker files.

Edge cases:

- If `stop_hook_active` is set, the stop hook skips the check. This prevents a loop when the agent continues after a blocked stop.
- If the session id is missing or has characters other than letters, digits, `-` and `_`, the edit is not recorded and the stop hook does not run the check.
- Files inside build output folders (`build`, `.gradle`, `bin`, `obj`) and outside the repository are ignored.
- The hooks find the repository root with `git rev-parse --show-toplevel`, so the marker and the check use the root also when the agent works in a subdirectory.
- The first check after a reboot also restores NuGet packages or starts the Gradle daemon, so it takes longer. The stop hook timeout is 300 seconds.
- The OpenCode plugin runs the check asynchronously, so it does not block OpenCode. Only one check runs at a time.

## Tests

Run the tests for the hooks and the plugin from the repository root:

```shell
node --test .agents/hooks/*.spec.ts .opencode/plugins/*.spec.ts
```

The tests use the Node.js test runner, so they need no dependencies. Unit specs test the small functions without I/O. Integration specs run the hooks and the plugin against a temporary Git repository with fake packages. A fake `gradlew` script and a fake `dotnet` command record each task they run.
