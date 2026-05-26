# Step 387 - Virtio-blk read-notify block-result projector

## Summary
Added a host-tested virtio-blk read-notify block-result projector that maps gated read-notify completion results into future `BlockReadResult` metadata.

## Why
The read-notify completion planner now gates caller-owned completion/reclaim on successful notify readiness. This step adds the next metadata-only projection layer so later `IReadOnlyBlockDevice` adapter work can consume one result shape while preserving notify-readiness and completion provenance.

## Files changed
- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step387.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence
- Focused host build passed with 0 warnings and 0 errors.
- Focused marker scan emitted:
  - `TEST_PASS storage.virtioblk.readnotifyblock projects completed read`
  - `TEST_PASS storage.virtioblk.readnotifyblock maps readiness failure`
  - `TEST_PASS storage.virtioblk.readnotifyblock maps completion failure`
  - `TEST_PASS storage.virtioblk.readnotifyblock preserves no-live-device boundary`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` exited 0 with only LF-to-CRLF working-copy normalization warnings.

## Failures
None.

## Deferred work
- `VirtioBlockReadQueueNotifyBlockReadProjector` proves only host-tested projection from `VirtioBlockReadQueueNotifyCompletionResult` into future `BlockReadResult` metadata. It preserves notify-readiness, notify-completion, read-queue completion, reclaim, completion-reader, and ownership provenance, reports requested/completed block-byte counts on success, and maps failed readiness or completion/reclaim failure to `IoError`.
- It does not read queue/status memory, write memory, mutate cursor or descriptor state, map BARs, perform volatile/live MMIO, execute real memory barriers, allocate/map DMA, notify hardware, poll or wait for hardware, process interrupts, perform block I/O, read sectors, or prove QEMU/cloud, hosted .NET 10, ASP.NET Minimal API, Kestrel, or production readiness.

