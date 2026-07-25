# Step 548 - Virtio-blk read-queue publication default-result guard

## Summary

Added an explicit initialized-state guard to
`VirtioBlockReadQueuePublishResult` so a default result no longer satisfies
`Succeeded`.

## Why

Read-queue publication is the first reusable result in the virtio-blk read
pipeline. A zero/default publication result should not be treated as a
successful descriptor publication or allowed to feed completion, notification,
readiness, or pipeline metadata.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step548.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to readqueue/notify markers and `TEST_PASS host.tests`.
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
TEST_START storage.virtioblk.readqueue publishes valid block range
TEST_PASS storage.virtioblk.readqueue publishes valid block range
TEST_START storage.virtioblk.readqueue rejects out-of-range block request
TEST_PASS storage.virtioblk.readqueue rejects out-of-range block request
TEST_START storage.virtioblk.readqueue preserves cursor on publish failure
TEST_PASS storage.virtioblk.readqueue preserves cursor on publish failure
TEST_START storage.virtioblk.readqueue publish default result is not success
TEST_PASS storage.virtioblk.readqueue publish default result is not success
TEST_START storage.virtioblk.readqueue preserves no-live-device boundary
TEST_PASS storage.virtioblk.readqueue preserves no-live-device boundary
TEST_START storage.virtioblk.readqueue completes and advances used index
TEST_PASS storage.virtioblk.readqueue completes and advances used index
TEST_START storage.virtioblk.readqueue completion rejects failed publication
TEST_PASS storage.virtioblk.readqueue completion rejects failed publication
TEST_START storage.virtioblk.readqueue completion rejects no completion
TEST_PASS storage.virtioblk.readqueue completion rejects no completion
TEST_START storage.virtioblk.readqueue completion preserves no-live-device boundary
TEST_PASS storage.virtioblk.readqueue completion preserves no-live-device boundary
TEST_START storage.virtioblk.readqueue block result reports completed read
TEST_PASS storage.virtioblk.readqueue block result reports completed read
TEST_START storage.virtioblk.readqueue block result maps publication failure
TEST_PASS storage.virtioblk.readqueue block result maps publication failure
TEST_START storage.virtioblk.readqueue block result maps completion failure
TEST_PASS storage.virtioblk.readqueue block result maps completion failure
TEST_START storage.virtioblk.readqueue block result preserves no-live-device boundary
TEST_PASS storage.virtioblk.readqueue block result preserves no-live-device boundary
TEST_START storage.virtioblk.notify plans readqueue notification metadata
TEST_PASS storage.virtioblk.notify plans readqueue notification metadata
TEST_START storage.virtioblk.notify rejects failed publication
TEST_PASS storage.virtioblk.notify rejects failed publication
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

- This is host-tested read-queue publication result contract safety only. It
  does not prove live virtqueue publication, DMA-safe allocation, device
  notification, interrupts, hardware completion waiting, block I/O, cloud
  readiness, Kestrel support, or production readiness.

