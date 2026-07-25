# Step 154 - Virtio Device Initialization Planning Contract

## What Changed

- Added `VirtioDeviceInitializationPlan` as a generic, pure virtio status/feature planning contract.
- Added status constants for `ACKNOWLEDGE`, `DRIVER`, `FEATURES_OK`, `DRIVER_OK`, and `FAILED` status-write planning.
- Added status-coded handling for missing required features, `FEATURES_OK` rejection, invalid required queue counts, and insufficient available queues.
- Added selected feature bitset reporting from caller-provided device, required, and optional feature masks.
- Added host tests for the issue #172 markers.
- Updated driver, storage, networking, and completion-gate docs.

## Why It Changed

Gitea issue #172 needs the next virtio planning layer after PCI identity/capability discovery and split-ring layout. This contract lets later virtio-blk and virtio-net work share a small deterministic initialization sequence without claiming live feature negotiation, queue setup, MMIO, DMA, interrupts, disk reads, or packet transfer.

## Files Changed

- `src/OS.Kernel/Drivers/VirtioDeviceInitializationPlan.cs`
- `tests/OS.HostTests/VirtioDeviceInitializationPlanTests.cs`
- `docs/drivers.md`
- `docs/cstorage.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step154.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
# Temporary focused harness outside the repository:
dotnet run --project $env:TEMP\os-virtio-init-focused-<id>\VirtioInitFocused.csproj --nologo
```

## Logs And Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- The focused temporary Virtio initialization harness emitted:
  - `TEST_PASS drivers.virtio init plans status sequence`
  - `TEST_PASS drivers.virtio init rejects missing features`
  - `TEST_PASS drivers.virtio init rejects features-ok failure`
  - `TEST_PASS drivers.virtio init records selected features`

## Failures

- A broad `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` attempt exited with code 1 before reaching the new Virtio initialization tests, during an existing ImageBuilder module-initializer path. It is not counted as issue #172 evidence.
- The first temporary focused harness compile failed because top-level local functions cannot be overloaded. The harness was corrected to use distinct helper names and then passed all issue #172 markers.

## Deferred Work

- No live device status register is read or written.
- No live feature registers are read.
- No live `FEATURES_OK` response is observed.
- No PCI configuration space, BAR, MMIO, or ECAM path is touched.
- No virtqueue is created or published.
- No DMA-safe memory is allocated.
- No interrupt, disk, or packet I/O is performed.

## Evidence Boundary

This step is host-testable planning only. It proves that caller-provided virtio feature and queue metadata can be reduced to a deterministic status-write sequence, selected feature bitset, missing-feature mask, feature-ack rejection status, and queue-count validation result.
