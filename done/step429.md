# Step 429 - FAT32 public result default guards

## Summary

Implemented Gitea issue #414 by making the public FAT32 parser result structs
reject default/zero values as successful evidence.

## Why

`Fat32Status.Success` is zero. The public FAT32 result structs previously
reported `default(T).Succeeded == true` because success only checked the status
field. That could let callers trust zero/default BPB, directory, lookup, or
file-read metadata before a parser factory path had run.

## Files changed

- `src/OS.Kernel/Storage/Fat32BpbParseResult.cs`
- `src/OS.Kernel/Storage/Fat32DirectoryReadResult.cs`
- `src/OS.Kernel/Storage/Fat32FileLookupResult.cs`
- `src/OS.Kernel/Storage/Fat32FileReadResult.cs`
- `tests/OS.HostTests/StorageFat32ParserTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step429.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'storage fat32 result defaults are not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- FAT32 default-result marker evidence:

```text
TEST_START storage fat32 result defaults are not success
TEST_PASS storage fat32 result defaults are not success
TEST_PASS host.tests
```

- Full solution build passed with 0 warnings and 0 errors.
- Naming and source firewall guard emitted:

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

## Failures

None.

## Deferred work

Private FAT32 parser helper structs and broader GPT/package result structs still
need separate small default-result hardening slices. This step is host-tested
FAT32 contract evidence only; it does not implement live disk I/O, live
virtio-blk, boot-time package reads, hosted .NET, Kestrel, cloud readiness, or
production readiness.
