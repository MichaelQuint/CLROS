# Step 438 - Package parser result default guard

## Summary

Implemented Gitea issue #423 by making package parser, file lookup, path-copy,
and content results reject default/zero values as successful parser evidence.

## Why

`OsPackageStatus.Success` is zero. Default parser result values previously
reported success because `Succeeded` only checked the status field. Package
filesystem, loader, and PAL-facing callers must not treat zero/default parser
metadata or content spans as initialized package evidence.

## Files changed

- `src/OS.Kernel/Storage/OsPackageParser.cs`
- `tests/OS.HostTests/StoragePackageParserTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step438.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'storage\.package parser result defaults are not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- Package parser default-result marker evidence:

```text
TEST_START storage.package parser result defaults are not success
TEST_PASS storage.package parser result defaults are not success
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

- None.

## Deferred work

Other storage and networking result structs still need separate default-result
hardening slices. This step is host-tested package parser contract evidence
only; it does not implement live disk I/O, package boot, hosted .NET, Kestrel,
cloud readiness, or production readiness.
