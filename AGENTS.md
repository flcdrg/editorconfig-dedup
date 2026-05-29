# Agent instructions for this repository

## Build and test commands

Run all commands from the repository root.

- Build the solution: `dotnet build`
- Run the full test suite: `dotnet test --solution editorconfig-dedup.slnx --no-progress`
- Run a single test method: `dotnet test --project tests/tests.csproj --filter-method tests.DeduplicatorTests.DeduplicateSingleFile_DuplicateKeysInSameSection_MarkFirstAsRedundant --no-progress`
- Pack the CLI as a .NET tool: `dotnet pack dotnet-editorconfig-dedup/dotnet-editorconfig-dedup.csproj -c Release`

The repo pins **.NET SDK 10.0.107** in `global.json`. The test runner is **Microsoft.Testing.Platform**, so prefer `--solution`/`--project` and MTP-specific filters such as `--filter-method` instead of older VSTest `--filter` examples.

## High-level architecture

This repository is a small two-project .NET solution:

- `dotnet-editorconfig-dedup/` is the CLI project. It targets `net10.0`, references `System.CommandLine`, and is configured to pack as a .NET global tool named `dotnet-editorconfig-dedup`.
- `tests/` is an xUnit v3 test project that references the CLI project directly and runs through Microsoft.Testing.Platform.
- `samples/.editorconfig` is the main domain fixture and is used as the primary example input for the deduplication behavior.

### CLI usage

`Program.cs` wires up a `System.CommandLine` root command with two options:

- `--root` / `-r` — path to search for `.editorconfig` files (defaults to `.`)
- `--what-if` / `-w` — report duplicates without modifying files

### Core classes in `dotnet-editorconfig-dedup/`

| Class | Responsibility |
|---|---|
| `EditorConfigFile` | Parses a single `.editorconfig` file into sections and properties; writes deduplicated output back to disk |
| `EditorConfigSection` | Holds a section header pattern (e.g. `[*.cs]`) and its list of `PropertyDefinition`s |
| `PropertyDefinition` | Represents a single `key = value` line; carries `IsRedundant` flag used to suppress it on write |
| `Deduplicator` | Orchestrates analysis: within-section duplicates, cross-section duplicates within the same file (broader pattern wins), and cross-file duplicates in a directory hierarchy |
| `PatternMatcher` | Determines whether one section pattern is broader than another and provides basic glob matching |
| `DeduplicationSummary` | Accumulates duplicate findings and generates a human-readable report |

### Deduplication logic

Three passes run in order:

1. **Within-section** — if the same key appears more than once in the same section, all but the last occurrence are marked redundant.
2. **Cross-section within a file** — if a key/value pair in a narrower-pattern section (e.g. `[*.cs]`) duplicates one already present in a broader section (e.g. `[*]`) of the same file, the narrower entry is marked redundant.
3. **Cross-file hierarchy** — if a child `.editorconfig` repeats a key/value that a parent file already defines under a matching or broader pattern, the child entry is marked redundant.

### Test classes in `tests/`

- `EditorConfigParserTests` — parsing correctness (sections, comments, duplicate key storage)
- `DeduplicatorTests` — unit tests for each deduplication pass
- `IntegrationTests` — end-to-end tests covering what-if mode, modify mode, and hierarchy deduplication

Most tests use **Verify** snapshot assertions; accepted snapshots live as `*.verified.txt` files alongside the test source.

## Key conventions

- Use the solution file `editorconfig-dedup.slnx`, not a traditional `.sln`, when you need solution-wide operations.
- Keep the CLI code in the `dotnet-editorconfig-dedup` project and test it from the separate `tests` project through project references rather than duplicating logic in tests.
- Respect the repository's `.editorconfig` style choices when adding C# code: explicit types are preferred over `var` in most cases, max line length is `170`, and the configured modifier order is `public, private, protected, internal, file, const, static, extern, new, virtual, abstract, sealed, override, readonly, unsafe, required, volatile, async`.
- The CLI is intended to ship as a tool package, so changes to command names, entrypoint behavior, or packaging metadata in `dotnet-editorconfig-dedup.csproj` have repo-wide impact.
- When adding a new Verify snapshot test, run the test once to generate the `.received.txt` file, review its content, then copy/rename it to the corresponding `.verified.txt` file.
