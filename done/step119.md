# Step 119 - Deterministic Host Package Builder

## What Changed

- Added `OS.ImageBuilder build-package --input <dir> --output <pkg>`.
- The command recursively reads host input files, normalizes package paths to forward-slash relative paths, sorts entries with ordinal ordering, and writes the existing `OSPKGv0` header/file-table/content format.
- Added validation for missing input directories, empty package directories, unsafe package paths, duplicate normalized paths, file-count limits, and package size limits.
- Added host tests for directory packaging, deterministic byte-for-byte output, and unsafe path rejection.
- Updated image-builder, app-model, storage, and completion-gate docs.

## Why It Changed

Issue #135 needed a deterministic host-side package creation path so future app binaries/config can be packaged into `OS/APP.PKG` without hand-authoring package bytes. This keeps the package format aligned with `OsPackageParser` and does not introduce an app launcher.

## Files Changed

- `tools/OS.ImageBuilder/Program.cs`
- `tests/OS.HostTests/ImageBuilderTests.cs`
- `docs/image-builder.md`
- `docs/app-model.md`
- `docs/cstorage.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Solution build passed with 0 warnings and 0 errors.
- Parent-owned full host tests emitted:
  - `TEST_PASS imagebuilder.package-builds-directory`
  - `TEST_PASS imagebuilder.package-builds-deterministically`
  - `TEST_PASS imagebuilder.package-build rejects unsafe input`
  - `TEST_PASS host.tests`

## Failures

- None in the parent-owned verification run.

## Deferred Work

- The package builder does not compile apps, validate executable formats, sign packages, compress content, or generate package manifests beyond file-table metadata.
- No live boot path reads this package from virtio-blk.
- No native app ABI, hosted .NET 10 runtime, ASP.NET Minimal API launch, or Kestrel behavior is implemented.

## Evidence Boundary

This step proves deterministic host package generation and parser compatibility only. It does not prove guest storage, app execution, hosted .NET 10, Minimal API, or Kestrel.
