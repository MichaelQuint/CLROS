# Step 205 - Image Builder GPT Parser Readback

## What Changed

- Added `TEST_PASS imagebuilder.gpt-parser-readback` to `tests/OS.HostTests/ImageBuilderTests.cs`.
- The test creates a deterministic 64 MiB raw image through `tools/OS.ImageBuilder`, slices LBA 1 and the primary GPT entry sectors declared by that header, and feeds those caller-owned buffers into `GptPartitionTableParser.ParsePrimary`.
- The assertion verifies the generated raw image exposes exactly one EFI System Partition descriptor at LBA 2048 through the kernel GPT parser contract, with the expected final usable LBA range and `EFI System` name metadata.
- Updated `docs/image-builder.md` to replace the stale note that this dedicated image-builder readback assertion was still missing.

## Why

The image builder already had byte-level GPT checks and the storage layer had independent caller-buffer GPT parser tests. This step ties those two boundaries together without changing the parser: generated raw image bytes are now consumed by the kernel GPT parser API before the storage path depends on real virtio-blk reads.

## Files Changed

- `tests/OS.HostTests/ImageBuilderTests.cs`
- `docs/image-builder.md`
- `done/step205.md`

## Tests Run

- `dotnet build .\OS.slnx --nologo -m:1`
- Temporary focused harness under `artifacts/issue224-focus` compiling only `tests/OS.HostTests/ImageBuilderTests.cs`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Logs And Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings/errors.
- The focused harness emitted `TEST_PASS imagebuilder.gpt-parser-readback`.
- The focused harness also emitted the existing image-builder package, manifest, native-app, and qcow2 markers.
- The integrated host harness currently exits before `ImageBuilderTests` because the parallel-owned `GptPartitionTableParserTests` module reports parser-test failures.

## Failures

- Full integrated host tests are blocked before this image-builder marker by out-of-scope GPT parser test failures:
  - `TEST_FAIL storage.gpt.parser parses caller buffers`
  - `TEST_FAIL storage.gpt.parser exposes crc not validated`
  - `TEST_FAIL storage.gpt.parser reports destination too small`
  - `TEST_FAIL storage.gpt.parser rejects invalid partition lba range`
  - `TEST_FAIL storage.gpt.parser records unterminated name metadata`

## Deferred Work

- Wire GPT CRC32 validation through the parser hardening issue without changing this image-builder readback contract.
- Run the full integrated host harness once the parallel GPT parser test failures are resolved.
- No QEMU, virtio-blk, post-ExitBootServices storage, or guest package-read claim is made by this step.

## Evidence Boundary

This is host-only image/readback evidence. It proves the generated GPT bytes can be parsed by the kernel caller-buffer GPT parser from a raw image file. It does not prove live block-device reads, firmware boot, or cloud import behavior.
