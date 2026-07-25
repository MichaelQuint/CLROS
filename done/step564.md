# Step 564 - Virtio-blk read session default-result guard

## Summary

Added explicit initialized-state gating to
`VirtioBlockReadOnlyReadSessionResult` so a default read-session result no
longer satisfies `Succeeded`.

## Why

The read session wraps the pipeline adapter and carries caller-owned cursor
state across repeated reads. Because the session success enum value is zero, a
default result could otherwise look like a completed session read before any
adapter invocation had run.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step564.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to `storage.virtioblk.readsession`, `TEST_PASS host.tests`, and
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
TEST_START storage.virtioblk.readsession delegates adapter read
TEST_PASS storage.virtioblk.readsession delegates adapter read
TEST_START storage.virtioblk.readsession preserves cursor state
TEST_PASS storage.virtioblk.readsession preserves cursor state
TEST_START storage.virtioblk.readsession rejects invalid setup
TEST_PASS storage.virtioblk.readsession rejects invalid setup
TEST_START storage.virtioblk.readsession default result is not success
TEST_PASS storage.virtioblk.readsession default result is not success
TEST_START storage.virtioblk.readsession preserves no-live-device boundary
TEST_PASS storage.virtioblk.readsession preserves no-live-device boundary
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

- This is host-tested read-session metadata safety only. It does not prove live
  virtio-blk device notification, BAR mapping, volatile MMIO, real memory
  barriers, DMA-safe allocation, hardware polling, block I/O, cloud readiness,
  Kestrel support, or production readiness.

