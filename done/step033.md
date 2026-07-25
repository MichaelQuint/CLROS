# Step 033 - GPT Primary First-Partition Parser

## What Changed

- Added a status-coded GPT parser over `IReadOnlyBlockDevice`.
- Added GPT result/status/header/partition-entry value types.
- Parsed the primary GPT header at LBA 1 and the first partition entry from the partition-entry array.
- Validated supported block geometry, GPT signature, header size, LBA ranges, entry count, entry size, entry-array range, short reads, and first partition LBA range.
- Updated storage docs to describe the current parser boundary and unsupported GPT features.

## Why It Changed

The storage path needs a host-tested partition parser before virtio-blk and guest package reads start. Implementing this over the existing block-device contract keeps the first parser slice independent from hardware and makes corrupt metadata behavior status-coded.

## Files Changed

- `src/OS.Kernel/OS.Kernel.csproj`
- `src/OS.Kernel/Storage/GptStatus.cs`
- `src/OS.Kernel/Storage/GptHeader.cs`
- `src/OS.Kernel/Storage/GptPartitionEntry.cs`
- `src/OS.Kernel/Storage/GptParseResult.cs`
- `src/OS.Kernel/Storage/GptParser.cs`
- `tests/OS.HostTests/StorageGptParserTests.cs`
- `docs/cstorage.md`
- `done/step033.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
git diff --check
```

## Evidence

- Host test marker: `TEST_PASS storage gpt parses valid primary first partition`
- Host test marker: `TEST_PASS storage gpt rejects bad primary signature`
- Host test marker: `TEST_PASS storage gpt rejects invalid entry size`
- Host test marker: `TEST_PASS storage gpt reports primary header short read`
- Host test marker: `TEST_PASS storage gpt reports partition entry short read`
- Host test marker: `TEST_PASS storage gpt rejects unsupported geometry`
- Host test marker: `TEST_PASS storage gpt rejects partition entry array out of range`
- Full host test marker: `TEST_PASS host.tests`

## Failures

None for this parser slice.

## Deferred Work

- GPT header CRC32 validation.
- Partition-entry array CRC32 validation.
- Backup GPT header parsing/fallback.
- Multi-entry enumeration and ESP selection.
- MBR fallback.
- Virtio-blk integration and guest FAT/package reads.

## Evidence Boundary

This step proves a host-tested GPT primary-header and first-entry parser over a mocked block device. It does not prove real disk I/O, virtio-blk, filesystem reads, manifest loading, app loading, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
