# Step 559 - Virtio-blk read-notify readiness default-result guard

## Summary

Added explicit initialized-state gating to
`VirtioBlockReadQueueNotifyReadinessResult` so a default read-notify readiness
result no longer satisfies `Succeeded`.

## Why

Read-notify readiness composes publication, notification metadata, checked
notify-offset planning, barrier readiness, and fake register-window notify
execution. Because the success enum value is zero, a default result could
otherwise look like that full readiness chain completed before any of those
steps had run.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step559.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to `storage.virtioblk.readnotify`, `TEST_PASS host.tests`, and
  `TEST_FAIL`.
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
TEST_START storage.virtioblk.readnotify composes publication and barrier notify
TEST_PASS storage.virtioblk.readnotify composes publication and barrier notify
TEST_START storage.virtioblk.readnotify rejects failed publication
TEST_PASS storage.virtioblk.readnotify rejects failed publication
TEST_START storage.virtioblk.readnotify rejects missing barrier
TEST_PASS storage.virtioblk.readnotify rejects missing barrier
TEST_START storage.virtioblk.readnotify propagates notify execution failure
TEST_PASS storage.virtioblk.readnotify propagates notify execution failure
TEST_START storage.virtioblk.readnotify default result is not success
TEST_PASS storage.virtioblk.readnotify default result is not success
TEST_START storage.virtioblk.readnotify preserves no-live-device boundary
TEST_PASS storage.virtioblk.readnotify preserves no-live-device boundary
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

- This is host-tested read-notify readiness metadata safety only. It does not
  prove live virtio-blk device notification, BAR mapping, volatile MMIO, real
  memory barriers, DMA-safe allocation, hardware polling, block I/O, cloud
  readiness, Kestrel support, or production readiness.
