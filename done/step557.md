# Step 557 - Virtio-blk notify register-window execution default-result guard

## Summary

Added explicit initialized-state gating to
`VirtioBlockQueueNotificationRegisterWindowExecutionResult` so a default
execution result no longer satisfies `Succeeded`.

## Why

The notify register-window fake execution path records the planned notify write
metadata that future live notification work will depend on. Because the success
enum value is zero, a default result could otherwise look like completed notify
execution before access planning and fake byte-span execution had run.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step557.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to `storage.virtioblk.notifywindowexec`, `TEST_PASS host.tests`,
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
TEST_START storage.virtioblk.notifywindowexec writes planned notify access
TEST_PASS storage.virtioblk.notifywindowexec writes planned notify access
TEST_START storage.virtioblk.notifywindowexec rejects failed plan
TEST_PASS storage.virtioblk.notifywindowexec rejects failed plan
TEST_START storage.virtioblk.notifywindowexec propagates access failure
TEST_PASS storage.virtioblk.notifywindowexec propagates access failure
TEST_START storage.virtioblk.notifywindowexec default result is not success
TEST_PASS storage.virtioblk.notifywindowexec default result is not success
TEST_START storage.virtioblk.notifywindowexec preserves no-live-device boundary
TEST_PASS storage.virtioblk.notifywindowexec preserves no-live-device boundary
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

- This is host-tested notify register-window execution metadata safety only. It
  does not prove live virtio-blk device notification, BAR mapping, volatile
  MMIO, real memory barriers, DMA-safe allocation, hardware polling, block I/O,
  cloud readiness, Kestrel support, or production readiness.
