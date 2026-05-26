# Step 110 - FAT32 File Chain Walking

## What Changed

- Replaced the previous contiguous-only FAT32 file content assumption with FAT-entry walking for root and first-level file reads.
- Added loop, invalid-next-cluster, FAT read, FAT short-read, and short-chain status paths.
- Added host tests for non-contiguous chained file content, loop rejection, and short-chain rejection.

## Why It Changed

Issue #128 removes a storage parser limitation that would make packaged app/config reads fragile. Real FAT32 files may not be contiguous, so the parser must follow FAT chains before it can be trusted as a package reader primitive.

## Files Changed

- `src/OS.Kernel/Storage/Fat32Parser.cs`
- `src/OS.Kernel/Storage/Fat32Status.cs`
- `src/OS.Kernel/Storage/Fat32FileReadResult.cs`
- `tests/OS.HostTests/StorageFat32ParserTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build > .\artifacts\logs\host-tests-current.log
```

## Logs And Evidence

- Build passed with 0 warnings and 0 errors.
- Host tests emitted:
  - `TEST_PASS storage fat32 reads chained file content`
  - `TEST_PASS storage fat32 rejects chained file loop`
  - `TEST_PASS storage fat32 rejects short chained file`
  - `TEST_PASS host.tests`

## Failures

- An intermediate build failed because the chained FAT32 test helpers were incomplete. The final coordinated verification pass succeeded after adding the missing fixture helpers and FAT entries.

## Deferred Work

- Multi-cluster directory chains remain unsupported.
- Nested paths beyond one directory level remain unsupported.
- No virtio-blk-backed FAT32 reads.
- No writes, FSInfo updates, long-name creation, or package/app payload execution.

## Evidence Boundary

This step proves FAT-chain-aware reads over host in-memory test images only. It is not virtio-blk, live guest disk access, post-ExitBootServices storage, app launch, hosted .NET 10, or Kestrel evidence.
