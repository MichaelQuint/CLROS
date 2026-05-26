# Step 096 - GPT ESP Manifest Locator Boundary

## What Changed

- Added `GptParser.LocatePrimaryEfiSystemPartition`.
- Added `GptEfiSystemPartitionLocationResult` with ESP start/end LBA, block count, block size, byte offset, and byte length.
- Added host tests for ESP location, missing ESP, and invalid ESP LBA ranges.

## Why It Changed

Issue #111 provides the GPT-side location metadata needed before a manifest/package reader can combine ESP selection with FAT32 `OS/MANIFEST.JSON` reads.

## Files Changed

- `src/OS.Kernel/Storage/GptParser.cs`
- `src/OS.Kernel/Storage/GptEfiSystemPartitionLocationResult.cs`
- `tests/OS.HostTests/StorageGptParserTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Host tests emitted:
  - `TEST_PASS storage gpt locates esp partition`
  - `TEST_PASS storage gpt rejects missing esp partition`
  - `TEST_PASS storage gpt rejects invalid esp lba range`

## Failures

- No build or host-test failures after integration.

## Deferred Work

- No GPT CRC validation.
- No backup GPT fallback.
- No virtio-blk.
- No integrated GPT+FAT32 manifest read from a generated image.

## Evidence Boundary

This step proves host-side GPT ESP location metadata only. It is not real disk or manifest loading evidence.
