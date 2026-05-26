# Step 099 - Native Manifest Loader From GPT ESP FAT32

## What Changed

- Added `NativeManifestLoader.LoadFromPrimaryGptEsp`.
- Combined GPT ESP location, `PartitionBlockDevice`, FAT32 `OS/MANIFEST.JSON` reads, and `NativeManifestParser`.
- Added status-coded substatus reporting for GPT, partition, FAT32, and manifest parser failures.
- Added host tests for successful load, missing ESP, and malformed native manifest.

## Why It Changed

Issue #117 connects the storage/parser contracts into the first host-testable manifest load path for the native-tier app model.

## Files Changed

- `src/OS.Kernel/Manifest/NativeManifestLoader.cs`
- `tests/OS.HostTests/NativeManifestLoaderTests.cs`
- `docs/manifest.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Host tests emitted:
  - `TEST_PASS manifest loader reads native manifest from gpt esp`
  - `TEST_PASS manifest loader rejects missing esp`
  - `TEST_PASS manifest loader rejects malformed native manifest`

## Failures

- No build or host-test failures after integration.

## Deferred Work

- No boot-time invocation.
- No virtio-blk.
- No package/app binary load.
- No app launcher handoff.

## Evidence Boundary

This step proves host-side composition of GPT, partition, FAT32, and manifest parser contracts only. It is not boot-time manifest loading or app launch evidence.
