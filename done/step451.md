# Step 451 - Virtio-blk read-only device plan default guard

## Summary

Implemented Gitea issue #436 by making
`VirtioBlockReadOnlyDevicePlanResult` reject default/zero values as successful
read-only device planning evidence.

## Why

`VirtioBlockReadOnlyDevicePlanStatus.Success` is zero. The result previously
reported success for `default(VirtioBlockReadOnlyDevicePlanResult)` because
`Succeeded` only checked status. The read-only device planner sits above the
virtio-blk read lifecycle and exposes storage planning metadata, so callers
must not treat uninitialized default metadata as successful evidence.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step451.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'storage\.virtioblk\.readonly default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- Virtio-blk read-only device plan default-result marker evidence:

```text
TEST_START storage.virtioblk.readonly default result is not success
TEST_PASS storage.virtioblk.readonly default result is not success
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

- `git diff --check` exited 0. Git reported existing LF-to-CRLF working-copy
  warnings only.

## Failures

- None.

## Deferred work

- This is host-tested virtio-blk read-only device planning evidence only. It
  does not notify a live device, poll hardware, handle interrupts, perform DMA
  or MMIO, copy payload bytes, read disks, host .NET, run Kestrel, or prove
  cloud/production readiness.
