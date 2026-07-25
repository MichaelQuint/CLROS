# Step 428 - Storage block-device default-result guards

## Summary

Closed Gitea issue #413 by making default/zero storage block-device result
structs non-successful until constructed through their explicit factory paths.

## Why

Several kernel-facing result structs used `Status == Success` as the only
success condition, which made `default(T).Succeeded` true. Storage callers must
not trust zero/default metadata as successful block-device, block-read, or
partition-create evidence.

## Files changed

- `src/OS.Kernel/Storage/BlockReadResult.cs`
- `src/OS.Kernel/Storage/BlockDeviceInfoResult.cs`
- `src/OS.Kernel/Storage/PartitionBlockDevice.cs`
- `tests/OS.HostTests/StorageBlockDeviceTests.cs`
- `tests/OS.HostTests/StoragePartitionBlockDeviceTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step428.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'storage block device result defaults are not success|storage partition device default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- Full host-test run ended with `TEST_PASS host.tests`.
- Marker-only evidence:

```text
TEST_START storage block device result defaults are not success
TEST_PASS storage block device result defaults are not success
TEST_START storage partition device default result is not success
TEST_PASS storage partition device default result is not success
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

This is still host-tested storage contract evidence only. It does not implement
live PCI enumeration, live virtio-blk, boot-time package reads from a real
device, hosted .NET, Kestrel, cloud readiness, or production readiness.
