# Step 597 - Virtio-Net BAR Live-Accessor Boundary

## Summary

Added a host-tested boundary between virtio PCI BAR register-window access plans
and an injected register accessor for virtio-net queue setup reads/writes.

## Why

Virtio-net queue setup already produced checked BAR-relative access plans and
fake byte-span execution evidence. The next dependency is a narrow contract that
future live MMIO/PIO accessors can implement without letting discovery/planning
code perform live hardware access or claim packet I/O.

## Files changed

- `src/OS.Kernel/Drivers/VirtioPciBarRegisterWindowPlan.cs`
- `tests/OS.HostTests/VirtioNetBarLiveAccessorBoundaryTests.cs`
- `docs/networking.md`
- `done/step597.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.bar\.liveaccessor|TEST_FAIL|TEST_PASS host\.tests'`

## Evidence

- Host-test project build passed with 0 warnings and 0 errors after clearing a
  stale `OS.HostTests` process that was locking output DLLs.
- Focused host-test output included:
  - `TEST_PASS drivers.virtionet.bar.liveaccessor reads planned queue-size access`
  - `TEST_PASS drivers.virtionet.bar.liveaccessor writes planned descriptor access`
  - `TEST_PASS drivers.virtionet.bar.liveaccessor rejects missing accessor and plan`
  - `TEST_PASS drivers.virtionet.bar.liveaccessor rejects unsupported width`
  - `TEST_PASS drivers.virtionet.bar.liveaccessor propagates accessor status`
  - `TEST_PASS drivers.virtionet.bar.liveaccessor preserves boundary contract`
  - `TEST_PASS drivers.virtionet.bar.liveaccessor default result is not success`
  - `TEST_PASS host.tests`

## Failures

- The first build attempt failed because an existing `OS.HostTests` process
  (`PID 12796`) held `OS.Kernel.dll` and `OS.Std.NoRuntime.Shared.dll` in the
  host-test output directory. That stale process was stopped and the build then
  passed.

## Deferred work

- This does not implement a live MMIO or port-I/O accessor.
- This does not map BARs, configure a live virtio-net queue, notify hardware,
  poll used rings, handle interrupts, allocate or map DMA memory, transfer
  packets, obtain DHCP, resolve DNS, open TCP, serve HTTP, or prove Kestrel.
