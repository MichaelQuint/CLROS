# Step 216 - Image Builder Azure VHD and AWS AMI Dry-Run Contracts

## Summary

Added local host-tool contracts for two provider artifact tracks:

- Azure fixed VHD conversion planning through `OS.ImageBuilder convert-azure-vhd`.
- AWS Nitro/HVM AMI import metadata validation through `OS.ImageBuilder plan-aws-ami`.

Both paths are host-side validation and dry-run tooling. They do not upload artifacts, require provider credentials, create cloud images, or prove cloud boot.

## Why

The new provider requirements docs define cloud-specific artifact expectations before live Azure/AWS boot is possible. Azure needs a fixed VHD with 1 MiB-aligned virtual size. AWS needs x86_64/HVM/EBS/boot-mode/ENA metadata to line up with the actual image layout. Capturing those constraints in `OS.ImageBuilder` gives later agents a deterministic contract without jumping to provider APIs too early.

## Files changed

- `tools/OS.ImageBuilder/Program.cs`
- `tests/OS.HostTests/ImageBuilderTests.cs`
- `docs/image-builder.md`
- `docs/cloud-import.md`
- `done/step216.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence

- Build passed with `0 Warning(s)` and `0 Error(s)`.
- Host tests saved to `artifacts/logs/host-tests-image-cloud-235-238.log`.
- The host-test log ended with `TEST_PASS host.tests` and `HOST_TEST_EXIT 0`.
- New Azure markers:
  - `TEST_PASS imagebuilder.azure.vhd validates aligned fixed disk`
  - `TEST_PASS imagebuilder.azure.vhd rejects unaligned virtual size`
- New AWS markers:
  - `TEST_PASS imagebuilder.aws.ami validates nitro hvm metadata`
  - `TEST_PASS imagebuilder.aws.ami rejects non hvm virtualization`
  - `TEST_PASS imagebuilder.aws.ami rejects arm64 import path for first target`
  - `TEST_PASS imagebuilder.aws.ami validates boot mode matches layout`

## Failures

- None in this slice.

## Deferred work

- No Azure VHD was generated in this test pass; dry-run mode only proved command shape and validation.
- No Azure Gallery import, Azure VM boot, AWS S3 upload, AWS VM Import, AMI registration, or EC2 boot was attempted.
- The current AWS plan rejects legacy BIOS metadata for the OS-produced raw image because the current layout is GPT/ESP/`BOOTX64.EFI`.
- ENA, NVMe, Hyper-V, VMBus, NetVSC, StorVSC, hosted .NET 10, ASP.NET Minimal API, and Kestrel remain separate tracks.

