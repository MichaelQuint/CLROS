# Step 029 - App Package Placement in Raw ESP

## What Changed

- Added optional `--package <path>` support to `OS.ImageBuilder` for `plan-raw`, `create-raw --dry-run`, and `create-raw`.
- Added package validation with stable reasons:
  - `package_not_found`
  - `package_empty`
  - `package_too_large`
- Extended the raw FAT32 ESP writer to place package bytes at `\OS\APP.PKG`.
- Preserved existing `\EFI\BOOT\BOOTX64.EFI` placement and optional `\OS\MANIFEST.JSON` placement.
- Extended host tests to verify dry-run package planning, manifest+package co-placement, exact package byte readback, and package validation failures.
- Updated image-builder, storage, and app-model docs with the evidence boundary.

## Why It Changed

The native-tier app model needs a reproducible host-side way to package a workload payload before the guest has virtio-blk, FAT/package readers, or an app launcher. This step provides byte placement only, keeping runtime and loader claims deferred.

## Files Changed

- `tools/OS.ImageBuilder/Program.cs`
- `tests/OS.HostTests/ImageBuilderTests.cs`
- `docs/image-builder.md`
- `docs/cstorage.md`
- `docs/app-model.md`
- `done/step029.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` passed and emitted `TEST_PASS host.tests`.
- ImageBuilder host tests emitted:
  - `TEST_PASS imagebuilder emits dry-run package placement plan without writing image`
  - `TEST_PASS imagebuilder creates raw ESP with manifest and APP.PKG`
  - `TEST_PASS imagebuilder rejects missing package`
  - `TEST_PASS imagebuilder rejects empty package`
  - `TEST_PASS imagebuilder rejects oversized package`

## Failures

No failures remain for this slice.

## Deferred Work

- Define the actual package file format.
- Implement guest-side manifest/package reads from storage.
- Implement native-tier app launch.
- Package and run hosted .NET 10/Kestrel only after PAL, scheduler, storage, and networking prerequisites exist.

## Evidence Boundary

This step proves host-side byte placement in the raw FAT32 ESP. It does not prove package parsing, manifest loading, app launch, hosted .NET 10, ASP.NET Minimal API, Kestrel, virtio-blk, or guest filesystem behavior.
