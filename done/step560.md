# Step 560 - Virtio-blk read-notify completion default-result guard

## Summary

Added explicit initialized-state gating to
`VirtioBlockReadQueueNotifyCompletionResult` so a default read-notify completion
result no longer satisfies `Succeeded`.

## Why

Read-notify completion consumes a completed notify-readiness result and then
delegates to used-ring/status-byte completion and descriptor ownership reclaim.
Because the success enum value is zero, a default result could otherwise look
like that completion path succeeded before any readiness or completion work had
run.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step560.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to `storage.virtioblk.readnotifycompletion`,
  `TEST_PASS host.tests`, and `TEST_FAIL`.
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
TEST_START storage.virtioblk.readnotifycompletion completes after notify readiness
TEST_PASS storage.virtioblk.readnotifycompletion completes after notify readiness
TEST_START storage.virtioblk.readnotifycompletion rejects failed readiness
TEST_PASS storage.virtioblk.readnotifycompletion rejects failed readiness
TEST_START storage.virtioblk.readnotifycompletion rejects no completion
TEST_PASS storage.virtioblk.readnotifycompletion rejects no completion
TEST_START storage.virtioblk.readnotifycompletion propagates ownership failure
TEST_PASS storage.virtioblk.readnotifycompletion propagates ownership failure
TEST_START storage.virtioblk.readnotifycompletion default result is not success
TEST_PASS storage.virtioblk.readnotifycompletion default result is not success
TEST_START storage.virtioblk.readnotifycompletion preserves no-live-device boundary
TEST_PASS storage.virtioblk.readnotifycompletion preserves no-live-device boundary
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

- This is host-tested read-notify completion metadata safety only. It does not
  prove live virtio-blk device notification, BAR mapping, volatile MMIO, real
  memory barriers, DMA-safe allocation, hardware polling, block I/O, cloud
  readiness, Kestrel support, or production readiness.
