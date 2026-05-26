# Step 200 - Caller-Buffer GPT Partition Table Parser

## What Changed

- Added `OS.Storage.GptPartitionTableParser`, a span-based parser for a primary GPT header sector and caller-provided contiguous partition-entry sectors.
- Added status/result contracts for unsupported sector sizes, invalid block counts, short header buffers, signature/revision/header/LBA failures, partition-entry metadata failures, too-small entry buffers, too-small descriptor destinations, and invalid partition LBA ranges.
- Added `GptPartitionDescriptor` so parsed partition metadata is returned into a caller-provided span without allocating strings. The descriptor preserves type GUID, unique GUID, first/last LBA, attributes, entry offset, name byte offset, fixed name byte length, UTF-16 code-unit count, and null-termination metadata.
- Exposed CRC behavior explicitly through `GptPartitionTableCrcStatus.NotValidated` plus parser constants showing header CRC32 and partition-entry-array CRC32 are not validated yet.
- Added host tests with `storage.gpt.parser ...` markers.
- Updated `docs/cstorage.md` with the new parser contract, markers, and current CRC/backup-header limitations.

## Why It Changed

Future storage and virtio-blk code needs GPT metadata parsing over buffers it already owns. The existing `GptParser` goes through `IReadOnlyBlockDevice`, allocates read windows, and creates string names. This slice adds the lower-level no-allocation primitive first without rewriting the package loader or ESP selection path.

## Files Changed

- `src/OS.Kernel/Storage/GptPartitionTableParser.cs`
- `tests/OS.HostTests/GptPartitionTableParserTests.cs`
- `docs/cstorage.md`
- `done/step200.md`

## Tests Run

```text
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
```

Result:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 -o .\artifacts\tmp\worker-e-hosttests
```

Result:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

```text
dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll *> .\artifacts\logs\worker-e-gpt-parser-normalbin.log
```

GPT parser evidence before the later unrelated failure:

```text
TEST_PASS storage.gpt.parser parses caller buffers
TEST_PASS storage.gpt.parser exposes crc not validated
TEST_PASS storage.gpt.parser rejects bad signature
TEST_PASS storage.gpt.parser rejects unsupported revision
TEST_PASS storage.gpt.parser rejects invalid header size
TEST_PASS storage.gpt.parser rejects invalid current lba
TEST_PASS storage.gpt.parser rejects invalid usable bounds
TEST_PASS storage.gpt.parser rejects invalid entry count
TEST_PASS storage.gpt.parser rejects invalid entry size
TEST_PASS storage.gpt.parser rejects entry array out of range
TEST_PASS storage.gpt.parser rejects short entry buffer
TEST_PASS storage.gpt.parser reports destination too small
TEST_PASS storage.gpt.parser rejects invalid partition lba range
TEST_PASS storage.gpt.parser records unterminated name metadata
```

## Failures

- `dotnet build .\OS.slnx --nologo -m:1` could not complete because `tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe` was locked by an existing `OS.HostTests` process (`12768`). The kernel project and host-test project compiled successfully when the host-test output was redirected to an isolated worker directory.
- The full normal-bin host harness exited nonzero after all new `storage.gpt.parser` markers passed because `drivers.virtionet.rx.lifecycle reports completion failures` failed in `tests/OS.HostTests/VirtioNetReceiveLifecycleTests.cs`. That file is outside issue #219 and was not modified here.
- Running the host-test binary from the isolated output directory also caused unrelated image-builder tests to fail; those tests appear to assume the normal test binary layout and are not used as evidence for this slice.

## Deferred Work

- GPT header CRC32 validation is still not implemented.
- GPT partition-entry-array CRC32 validation is still not implemented.
- Backup GPT header parsing and fallback are still not implemented.
- The existing package loader and ESP selector still use `GptParser`; migration should be a separate integration issue after the caller-buffer parser has the CRC/backup behavior the loader needs.

## Evidence Boundary

This step proves only status-coded GPT primary-header and partition-entry parsing over caller-owned buffers in host tests. It is not evidence of live virtio-blk reads, disk-sector DMA ownership, FAT/package loading from a real device, post-ExitBootServices storage, native app launch, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
