# Step 558 - Virtio-blk notify barrier execution default-result guard

## Summary

Added explicit initialized-state gating to
`VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult` so a default
barrier-gated notify execution result no longer satisfies `Succeeded`.

## Why

The barrier-gated notify register-window fake execution path proves that
descriptor and available-ring writes were ordered before a fake notify write.
Because the success enum value is zero, a default result could otherwise look
like completed barrier-gated notify execution before readiness and fake
byte-span execution had run.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step558.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to `storage.virtioblk.notifywindowbarrier`, `TEST_PASS host.tests`,
  and `TEST_FAIL`.
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```text
TEST_START storage.virtioblk.notifywindowbarrier writes planned notify access
TEST_PASS storage.virtioblk.notifywindowbarrier writes planned notify access
TEST_START storage.virtioblk.notifywindowbarrier rejects missing barrier
TEST_PASS storage.virtioblk.notifywindowbarrier rejects missing barrier
TEST_START storage.virtioblk.notifywindowbarrier rejects failed plan
TEST_PASS storage.virtioblk.notifywindowbarrier rejects failed plan
TEST_START storage.virtioblk.notifywindowbarrier propagates access failure
TEST_PASS storage.virtioblk.notifywindowbarrier propagates access failure
TEST_START storage.virtioblk.notifywindowbarrier default result is not success
TEST_PASS storage.virtioblk.notifywindowbarrier default result is not success
TEST_START storage.virtioblk.notifywindowbarrier preserves no-live-device boundary
TEST_PASS storage.virtioblk.notifywindowbarrier preserves no-live-device boundary
TEST_PASS host.tests
```

```text
dotnet build .\OS.slnx --nologo -m:1
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

```text
git diff --check
ExitCode: 0; existing LF-to-CRLF working-copy warnings only.
```

## Failures

- None.

## Deferred work

- This is host-tested barrier-gated notify register-window metadata safety only.
  It does not prove live virtio-blk device notification, BAR mapping, volatile
  MMIO, real memory barriers, DMA-safe allocation, hardware polling, block I/O,
  cloud readiness, Kestrel support, or production readiness.
