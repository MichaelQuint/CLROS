# Step 646 - ImageBuilder oversized raw image validation

## Summary

Added a raw image-builder validation guard that rejects image sizes whose ESP
sector count cannot fit the current FAT32/GPT layout before the layout reaches
the checked `uint` sector-count conversion.

## Why

`--image-size-mib` already rejected values below the supported minimum, but a
very large value could pass command-line parsing and fail later as an unhandled
checked conversion while planning the raw layout. The tool now reports the
stable `image_too_large` reason through the existing machine-readable
`TEST_FAIL imagebuilder.plan` path.

## Files changed

- `tools/OS.ImageBuilder/Program.cs`
  - Added an explicit raw ESP partition-sector-count limit check that reports
    `image_too_large`.
- `tests/OS.HostTests/ImageBuilderTests.cs`
  - Added `TEST_PASS imagebuilder rejects oversized raw image size`.
- `docs/image-builder.md`
  - Documented the maximum FAT32 sector-count geometry validation and
    `image_too_large` reason.
- `done/step646.md`
  - Records this host-tooling evidence.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- Full host-test run emitted:
  - `TEST_PASS imagebuilder rejects oversized raw image size`
  - `TEST_PASS imagebuilder emits dry-run raw layout plan without writing image`
  - `TEST_PASS imagebuilder rejects missing EFI artifact`
  - `TEST_PASS imagebuilder rejects missing manifest`
  - `TEST_PASS imagebuilder rejects oversized manifest`
  - `TEST_PASS imagebuilder rejects missing package`
  - `TEST_PASS imagebuilder rejects oversized package`
  - `TEST_PASS host.tests`

## Failures

None in the focused build or full host-test run.

## Deferred work

This is host-side image-builder validation evidence only. It does not prove
QEMU boot, libvirt boot, cloud import, live guest storage reads, hosted .NET,
Minimal API, Kestrel, or provider compatibility.
