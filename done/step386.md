# Step 386 - Virtio-blk read-notify completion planner

## Summary
Added a host-tested virtio-blk read-notify completion planner that gates caller-owned used-ring/status-buffer completion parsing and descriptor-state reclaim on successful read-notify readiness.

## Why
Read queue completion should not be modeled as eligible until the read publication has reached the notification-readiness boundary. This step adds a small status-coded composition layer between the read-notify readiness result and the existing read queue completion path, preserving the host-only evidence boundary while making the future live-driver order explicit.

## Files changed
- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step386.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence
- Focused host build passed with 0 warnings and 0 errors.
- Focused marker scan emitted:
  - `TEST_PASS storage.virtioblk.readnotifycompletion completes after notify readiness`
  - `TEST_PASS storage.virtioblk.readnotifycompletion rejects failed readiness`
  - `TEST_PASS storage.virtioblk.readnotifycompletion rejects no completion`
  - `TEST_PASS storage.virtioblk.readnotifycompletion propagates ownership failure`
  - `TEST_PASS storage.virtioblk.readnotifycompletion preserves no-live-device boundary`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` exited 0 with only LF-to-CRLF working-copy normalization warnings.

## Failures
None.

## Deferred work
- This proves only host-tested composition from successful read-notify readiness into caller-owned used-ring/status-buffer completion parsing and descriptor-state reclaim through the existing read queue completion path. It refuses completion/reclaim when read-notify readiness failed and preserves nested readiness/completion status provenance.
- It does not map BARs, perform volatile/live MMIO, execute real memory barriers, notify hardware, allocate or map DMA, poll or wait for hardware, process interrupts, copy payload bytes, read sectors, or prove QEMU/cloud, hosted .NET 10, ASP.NET Minimal API, Kestrel, or production readiness.

