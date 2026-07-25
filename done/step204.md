# Step 204 - GPT CRC32 validation

## What Changed

- Wired `OS.Std.Hash.Crc32` into `OS.Storage.GptPartitionTableParser`.
- Added primary GPT header CRC32 validation with status-coded `HeaderCrcMismatch` and `GptPartitionTableCrcStatus.HeaderMismatch`.
- Added partition-entry-array CRC32 validation with status-coded `PartitionEntryArrayCrcMismatch` and `GptPartitionTableCrcStatus.PartitionEntryArrayMismatch`.
- Added parse-result CRC diagnostics for expected/computed header CRC32 and expected/computed partition-entry-array CRC32.
- Preserved caller-owned buffers: the parser computes partition-entry-array CRC32 directly over the validated entry byte range and uses only a bounded stack copy of the validated GPT header-size slice so the checksum field can be zeroed without mutating caller memory.
- Updated GPT parser host tests with `storage.gpt.parser.crc ...` markers and refreshed GPT fixtures to produce structurally valid CRCs.
- Updated `docs/cstorage.md` so GPT CRC validation is no longer listed as deferred for the caller-buffer parser.

## Why It Changed

The caller-buffer GPT parser is intended to become the hardened primitive for future virtio-blk/package loading. It could previously parse structurally valid GPT data while explicitly reporting CRC validation as absent. This step closes that integrity gap while keeping the parser no-allocation, status-coded, and independent of live disk I/O.

## Files Changed

- `src/OS.Kernel/Storage/GptPartitionTableParser.cs`
- `tests/OS.HostTests/GptPartitionTableParserTests.cs`
- `docs/cstorage.md`
- `done/step204.md`

## Tests Run

```text
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

Build completed with 0 warnings and 0 errors.

The integrated host-test harness completed successfully and emitted:

```text
TEST_PASS storage.gpt.parser.crc validates good buffers
TEST_PASS storage.gpt.parser.crc rejects header mismatch
TEST_PASS storage.gpt.parser.crc rejects partition array mismatch
TEST_PASS host.tests
```

## Failures

- No failures were observed in the build or integrated host-test run for this slice.

## Deferred Work

- The legacy block-device `GptParser` still does not enforce GPT CRCs.
- Backup GPT header parsing and fallback remain unimplemented.
- The package loader still reaches GPT through the legacy `GptParser`; migrating it to the caller-buffer parser should be a separate integration issue.
- No live virtio-blk, post-ExitBootServices disk, native app launch, hosted .NET 10, ASP.NET Minimal API, or Kestrel evidence is claimed.

## Evidence Boundary

This step proves only GPT primary-header CRC32 and partition-entry-array CRC32 validation over caller-owned buffers in host tests. It is not evidence of live block-device I/O, DMA-safe virtqueue ownership, FAT/package loading from a real disk, native app execution, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
