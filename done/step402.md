# Step 402 - Virtio Block Config Register-Window Capacity Reader

## Summary
Added a host-tested virtio-blk device-configuration register-window reader that fake-reads the capacity field from caller-owned BAR-window bytes into caller-owned scratch storage before reusing the existing `VirtioBlockDeviceConfig` parser.

## Why
The future live virtio-blk driver needs a checked path from bound PCI device-configuration aperture metadata to block geometry. This slice proves the register-window planning, fake access execution, scratch-buffer sizing, and parser failure propagation while keeping the boundary explicit: no live MMIO, no queue setup, no device notification, and no block I/O.

## Files changed
- `src/OS.Kernel/Storage/VirtioBlockDeviceConfig.cs`
- `tests/OS.HostTests/StorageVirtioBlockDeviceConfigTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step402.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence
- Focused host-test build completed with 0 warnings and 0 errors.
- Host-test marker scan emitted:
  - `TEST_PASS storage.virtioblk.config.windowexec reads capacity`
  - `TEST_PASS storage.virtioblk.config.windowexec rejects short scratch buffer`
  - `TEST_PASS storage.virtioblk.config.windowexec propagates register window failure`
  - `TEST_PASS storage.virtioblk.config.windowexec propagates access failure`
  - `TEST_PASS storage.virtioblk.config.windowexec propagates config parse failure`
  - `TEST_PASS storage.virtioblk.config.windowexec preserves no-live-mmio boundary`
  - `TEST_PASS host.tests`

## Failures
- None.

## Deferred work
- Wire the fake register-window config reader into virtio device descriptor/catalog composition only after the binding source owns the caller-provided BAR-window bytes.
- Replace fake byte-span reads with a real post-`ExitBootServices` MMIO accessor only after BAR mapping, volatile access rules, memory barriers, and diagnostics are ready.
- Keep real virtio-blk, boot-time package reads, hosted .NET 10, Kestrel, cloud readiness, and production readiness claims blocked until completion gates have live evidence.

## Evidence boundary
- This is host-tested fake register-window execution over caller-owned byte spans. It does not map BARs, read live device registers, stabilize config generation, negotiate features, configure queues, notify a device, wait for interrupts/completions, perform block I/O, read sectors from a disk, or prove cloud/provider readiness.

