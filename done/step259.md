# Step 259 - Kestrel Smoke Package Raw Image Staging

## What Changed

- Added `--kestrel-package <dir>` support to `tools/OS.ImageBuilder` for `plan-raw` and `create-raw`.
- Reused `KestrelSmokePackageContractValidator` from `OS.ManifestTool` so image staging follows the existing `kestrel.smoke.package.v0` contract instead of introducing a second validator.
- Added deterministic package-root-to-`OSPKGv0` staging into the existing ESP package target `\OS\APP.PKG`.
- Added host tests for valid staging, invalid contract rejection, `SingleMinimalApi` profile evidence, and `MultiMinimalApi` rejection.
- Updated image-builder, app-model, and Kestrel docs with command shape and evidence boundary.

## Why It Changed

Issue #259 needed the host image-building path to place the first Kestrel smoke package contract into the raw image layout. This gives future guest package readers a stable on-disk target without claiming guest package I/O, hosted .NET 10 startup, socket binding, or Kestrel execution.

## Files Changed

- `tools/OS.ImageBuilder/OS.ImageBuilder.csproj`
- `tools/OS.ImageBuilder/Program.cs`
- `tools/OS.ManifestTool/Program.cs`
- `tools/OS.ManifestTool/KestrelSmokePackageCommand.cs`
- `tests/OS.HostTests/ImageBuilderTests.cs`
- `docs/image-builder.md`
- `docs/kestrel.md`
- `docs/app-model.md`
- `done/step259.md`

## Tests Run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll
dotnet build .\OS.slnx --nologo -m:1
```

## Logs And Evidence

`dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` passed with:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

The full host harness exited `0` and emitted:

```text
TEST_PASS imagebuilder.kestrel_package stages files
TEST_PASS imagebuilder.kestrel_package rejects invalid contract
TEST_PASS imagebuilder.kestrel_package records single profile
TEST_PASS imagebuilder.kestrel_package rejects multi profile
TEST_PASS host.tests
```

`dotnet build .\OS.slnx --nologo -m:1` passed with:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

## Evidence Boundary

This is host packaging and raw-image placement evidence only. It does not prove guest package loading, virtio-blk reads, hosted .NET 10/CoreCLR startup, PAL file/config reads, PAL socket exports, TCP listen/accept, QEMU/libvirt guest HTTP, TLS, Minimal API execution, or Kestrel support.

## Deferred Work

- Use the published `apps/MinimalApiSmoke` package output as the source for this image-builder path in a CI/package script.
- Add guest-side package reads from `\OS\APP.PKG` after real storage is available.
- Connect the hosted manifest contract to the future hosted runtime/PAL launch path.
- Point the QEMU HTTP probe at a real guest Kestrel endpoint only after guest networking and hosted runtime prerequisites exist.
