# Agent instructions for this repository

## Build and test commands

Run all commands from the repository root.

- Build the solution: `dotnet build`
- Run the full test suite: `dotnet test --solution editorconfig-dedup.slnx --no-progress`
- Run a single test method: `dotnet test --project tests/tests.csproj --filter-method tests.UnitTest1.Test1 --no-progress`
- Pack the CLI as a .NET tool: `dotnet pack dotnet-editorconfig-dedup/dotnet-editorconfig-dedup.csproj -c Release`

The repo pins **.NET SDK 10.0.107** in `global.json`. The test runner is **Microsoft.Testing.Platform**, so prefer `--solution`/`--project` and MTP-specific filters such as `--filter-method` instead of older VSTest `--filter` examples.

## High-level architecture

This repository is a small two-project .NET solution:

- `dotnet-editorconfig-dedup/` is the CLI project. It targets `net10.0`, references `System.CommandLine`, and is configured to pack as a .NET global tool named `dotnet-editorconfig-dedup`.
- `tests/` is an xUnit v3 test project that references the CLI project directly and runs through Microsoft.Testing.Platform.
- `samples/.editorconfig` is the only domain fixture in the repo today and should be treated as the main example input for the eventual deduplication behavior.

The implementation is still at the scaffold stage: `Program.cs` currently only prints `Hello, World!`. When extending the tool, treat the csproj metadata and the sample `.editorconfig` as the clearest signals of intended direction: a command-line tool that analyzes `.editorconfig` content, with behavior exercised from the separate test project.

## Key conventions

- Use the solution file `editorconfig-dedup.slnx`, not a traditional `.sln`, when you need solution-wide operations.
- Keep the CLI code in the `dotnet-editorconfig-dedup` project and test it from the separate `tests` project through project references rather than duplicating logic in tests.
- Respect the repository’s `.editorconfig` style choices when adding C# code: explicit types are preferred over `var` in most cases, max line length is `170`, and the configured modifier order is `public, private, protected, internal, file, const, static, extern, new, virtual, abstract, sealed, override, readonly, unsafe, required, volatile, async`.
- The CLI is intended to ship as a tool package, so changes to command names, entrypoint behavior, or packaging metadata in `dotnet-editorconfig-dedup.csproj` have repo-wide impact.
