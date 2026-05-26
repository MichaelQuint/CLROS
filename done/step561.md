# Step 561 - Virtio-blk read-notify block projection default-result guard

## Summary

Added explicit initialized-state gating to
`VirtioBlockReadQueueNotifyBlockReadProjectionResult` so a default read-notify
block projection result no longer satisfies `Succeeded`.

## Why

Read-notify block projection turns notify-completion metadata into a future
`BlockReadResult`. Because the success enum value is zero, a default projection
could otherwise look like a completed block-read projection before notify
completion had been validated.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step561.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to `storage.virtioblk.readnotifyblock`, `TEST_PASS host.tests`, and
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
TEST_START storage.virtioblk.readnotifyblock projects completed read
TEST_PASS storage.virtioblk.readnotifyblock projects completed read
TEST_START storage.virtioblk.readnotifyblock maps readiness failure
TEST_PASS storage.virtioblk.readnotifyblock maps readiness failure
TEST_START storage.virtioblk.readnotifyblock maps completion failure
TEST_PASS storage.virtioblk.readnotifyblock maps completion failure
TEST_START storage.virtioblk.readnotifyblock default result is not success
TEST_PASS storage.virtioblk.readnotifyblock default result is not success
TEST_START storage.virtioblk.readnotifyblock preserves no-live-device boundary
TEST_PASS storage.virtioblk.readnotifyblock preserves no-live-device boundary
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

- This is host-tested read-notify block projection metadata safety only. It does
  not prove live virtio-blk device notification, BAR mapping, volatile MMIO,
  real memory barriers, DMA-safe allocation, hardware polling, block I/O, cloud
  readiness, Kestrel support, or production readiness.
