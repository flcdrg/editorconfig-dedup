# editorconfig-dedup

A .NET tool for removing redundant/duplicate entries in [`.editorconfig`](https://editorconfig.org/) files. It starts at the current (or specified) directory and will recursively process any child .`editorconfig` too.

## Usage

Scan the current directory and report duplicate entries without changing files:

```bash
dotnet editorconfig-dedup --what-if
```

Scan a specific directory and remove duplicate entries in place:

```bash
dotnet editorconfig-dedup --root /path/to/repo
```

Short form options are also available:

```bash
dotnet editorconfig-dedup -r /path/to/repo -w
```

