# Step 550 - Virtio-blk notification metadata default-result guard

## Summary

Added an explicit initialized-state guard to
`VirtioBlockQueueNotificationMetadataResult` so a default result no longer
satisfies `Succeeded`.

## Why

Notification metadata feeds notify-offset and register-window planning. A
zero/default metadata value should not be treated as a valid future notify
write or allowed to produce trusted notify-offset metadata.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step550.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to notify/notifyoffset markers and `TEST_PASS host.tests`.
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
TEST_START storage.virtioblk.notify plans readqueue notification metadata
TEST_PASS storage.virtioblk.notify plans readqueue notification metadata
TEST_START storage.virtioblk.notify rejects failed publication
TEST_PASS storage.virtioblk.notify rejects failed publication
TEST_START storage.virtioblk.notify default result is not success
TEST_PASS storage.virtioblk.notify default result is not success
TEST_START storage.virtioblk.notify preserves no-live-device boundary
TEST_PASS storage.virtioblk.notify preserves no-live-device boundary
TEST_START storage.virtioblk.notifyoffset derives relative offset
TEST_PASS storage.virtioblk.notifyoffset derives relative offset
TEST_START storage.virtioblk.notifyoffset rejects failed notification metadata
TEST_PASS storage.virtioblk.notifyoffset rejects failed notification metadata
TEST_START storage.virtioblk.notifyoffset rejects relative offset overflow
TEST_PASS storage.virtioblk.notifyoffset rejects relative offset overflow
TEST_START storage.virtioblk.notifyoffset preserves no-live-device boundary
TEST_PASS storage.virtioblk.notifyoffset preserves no-live-device boundary
TEST_START storage.virtioblk.notifywindow plans access
TEST_PASS storage.virtioblk.notifywindow plans access
TEST_START storage.virtioblk.notifywindow rejects failed offset plan
TEST_PASS storage.virtioblk.notifywindow rejects failed offset plan
TEST_START storage.virtioblk.notifywindow propagates register window failure
TEST_PASS storage.virtioblk.notifywindow propagates register window failure
TEST_START storage.virtioblk.notifywindow preserves no-live-device boundary
TEST_PASS storage.virtioblk.notifywindow preserves no-live-device boundary
TEST_START storage.virtioblk.notifywindowexec writes planned notify access
TEST_PASS storage.virtioblk.notifywindowexec writes planned notify access
TEST_START storage.virtioblk.notifywindowexec rejects failed plan
TEST_PASS storage.virtioblk.notifywindowexec rejects failed plan
TEST_START storage.virtioblk.notifywindowexec propagates access failure
TEST_PASS storage.virtioblk.notifywindowexec propagates access failure
TEST_START storage.virtioblk.notifywindowexec preserves no-live-device boundary
TEST_PASS storage.virtioblk.notifywindowexec preserves no-live-device boundary
TEST_START storage.virtioblk.notifywindowbarrier writes planned notify access
TEST_PASS storage.virtioblk.notifywindowbarrier writes planned notify access
TEST_START storage.virtioblk.notifywindowbarrier rejects missing barrier
TEST_PASS storage.virtioblk.notifywindowbarrier rejects missing barrier
TEST_START storage.virtioblk.notifywindowbarrier rejects failed plan
TEST_PASS storage.virtioblk.notifywindowbarrier rejects failed plan
TEST_START storage.virtioblk.notifywindowbarrier propagates access failure
TEST_PASS storage.virtioblk.notifywindowbarrier propagates access failure
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

- This is host-tested future notification metadata contract safety only. It
  does not prove live device notification, MMIO, memory barriers, DMA-safe
  allocation, hardware polling, block I/O, cloud readiness, Kestrel support, or
  production readiness.

