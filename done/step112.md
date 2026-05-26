# Step 112 - ImageBuilder Package File And Readback Evidence

## What Changed

- Added exact issue #130 host-test markers:
  - `TEST_PASS imagebuilder.package-file`
  - `TEST_PASS imagebuilder.package-readback`
- Added an image-builder fixture that writes a parser-valid OS package to `\OS\APP.PKG` in a generated raw ESP image.
- Added readback validation through `OsPackageLoader.LoadFromPrimaryGptEsp` and `OsPackageParser.GetFileContent`.
- Updated the image-builder, storage, and completion-gate docs with the evidence boundary.

## Why It Changed

Issue #130 bridges host image creation and the storage package-loader boundary. It proves the generated raw image can carry a parser-valid `OS/APP.PKG` and that the existing GPT ESP, FAT32, and package parser stack can read it back from image bytes.

## Files Changed

- `tests/OS.HostTests/ImageBuilderTests.cs`
- `docs/image-builder.md`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step112.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

The parent-owned full host-test run was logged to:

```text
artifacts/logs/host-tests-current.log
```

## Logs And Evidence

- Build passed with 0 warnings and 0 errors.
- Full host tests emitted:
  - `TEST_PASS imagebuilder.package-file`
  - `TEST_PASS imagebuilder.package-readback`
  - `TEST_PASS imagebuilder creates raw ESP with manifest and APP.PKG`
  - `TEST_PASS storage.package loader reads package from gpt esp`
  - `TEST_PASS host.tests`

## Failures

- None in the parent-owned verification run.

## Deferred Work

- No live guest disk read.
- No virtio-blk.
- No boot-time package loading.
- No app launch.
- No hosted .NET 10, ASP.NET Minimal API, or Kestrel proof.

## Evidence Boundary

This step is host image-builder and parser evidence only. It proves generated-image package placement and in-memory parser-stack readback, not real post-ExitBootServices storage, native app execution, hosted .NET, or Kestrel.
