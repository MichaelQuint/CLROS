# Step 555 - Virtio-blk notify-offset default-result guard

## Summary

Added explicit initialized-state gating to
`VirtioBlockQueueNotifyOffsetPlanResult` so a default result no longer satisfies
`Succeeded`.

## Why

Notify-offset planning feeds virtio PCI notify register-window planning. Because
the success enum value is zero, a default result could otherwise look like a
valid checked notify-aperture offset before planning had run.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step555.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to notify-offset markers and `TEST_PASS host.tests`.
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
TEST_START storage.virtioblk.notifyoffset derives relative offset
TEST_PASS storage.virtioblk.notifyoffset derives relative offset
TEST_START storage.virtioblk.notifyoffset rejects failed notification metadata
TEST_PASS storage.virtioblk.notifyoffset rejects failed notification metadata
TEST_START storage.virtioblk.notifyoffset rejects relative offset overflow
TEST_PASS storage.virtioblk.notifyoffset rejects relative offset overflow
TEST_START storage.virtioblk.notifyoffset default result is not success
TEST_PASS storage.virtioblk.notifyoffset default result is not success
TEST_START storage.virtioblk.notifyoffset preserves no-live-device boundary
TEST_PASS storage.virtioblk.notifyoffset preserves no-live-device boundary
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

- This is host-tested notify-offset metadata safety only. It does not prove live
  virtio-blk device notification, BAR mapping, volatile MMIO, real memory
  barriers, DMA-safe allocation, hardware polling, block I/O, cloud readiness,
  Kestrel support, or production readiness.

