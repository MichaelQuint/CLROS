# Step 563 - Virtio-blk read adapter default-result guard

## Summary

Added explicit initialized-state gating to
`VirtioBlockReadQueuePipelineAdapterResult` so a default read adapter result no
longer satisfies `Succeeded`.

## Why

The adapter validates caller-owned buffers before delegating to the read
pipeline. Because the adapter success enum value is zero, a default result could
otherwise look like a completed adapter read before preflight or pipeline
delegation had run.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step563.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to `storage.virtioblk.readadapter`, `TEST_PASS host.tests`, and
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
TEST_START storage.virtioblk.readadapter executes pipeline read
TEST_PASS storage.virtioblk.readadapter executes pipeline read
TEST_START storage.virtioblk.readadapter rejects short request buffer
TEST_PASS storage.virtioblk.readadapter rejects short request buffer
TEST_START storage.virtioblk.readadapter rejects short notify window
TEST_PASS storage.virtioblk.readadapter rejects short notify window
TEST_START storage.virtioblk.readadapter propagates pipeline failure
TEST_PASS storage.virtioblk.readadapter propagates pipeline failure
TEST_START storage.virtioblk.readadapter default result is not success
TEST_PASS storage.virtioblk.readadapter default result is not success
TEST_START storage.virtioblk.readadapter preserves no-live-device boundary
TEST_PASS storage.virtioblk.readadapter preserves no-live-device boundary
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

- This is host-tested read adapter metadata safety only. It does not prove live
  virtio-blk device notification, BAR mapping, volatile MMIO, real memory
  barriers, DMA-safe allocation, hardware polling, block I/O, cloud readiness,
  Kestrel support, or production readiness.

