# Step 232 - ENA DMA Address Capability Planner

## Summary

Added a host-tested ENA DMA address and admin queue-memory capability planner.

## Why

Issue #257 needed a deterministic validation boundary before any real DMA
allocator, IOMMU policy, BAR programming, or live ENA queue setup exists. The
new planner checks caller-provided physical region facts against the
device-reported physical address width from `EnaDeviceAttributes` and derives
AQ/ACQ physical regions from an already-valid `EnaAdminQueueLayoutPlanResult`.

## Files changed

- `src/OS.Kernel/Drivers/EnaDmaAddressPlan.cs`
- `tests/OS.HostTests/EnaDmaAddressPlanTests.cs`
- `docs/ena-driver-plan.md`
- `done/step232.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.ena\.dma|host\.tests'`
- `git diff --check`

## Evidence

- Solution build passed with `Build succeeded.`, `0 Warning(s)`, and
  `0 Error(s)`.
- Host tests emitted:
  - `TEST_PASS drivers.ena.dma validates address width`
  - `TEST_PASS drivers.ena.dma rejects overflow`
  - `TEST_PASS drivers.ena.dma validates queue regions`
  - `TEST_PASS drivers.ena.dma rejects invalid regions`
  - `TEST_PASS host.tests`
- `git diff --check` exited 0. It printed line-ending normalization warnings
  for existing modified files only.

## Failures

- None in this slice.

## Deferred work

- The planner does not allocate DMA memory, map pages, program an IOMMU, write
  BAR registers, ring doorbells, configure interrupts, send packets, receive
  packets, DHCP, TCP, HTTP, Kestrel, or perform real AWS Nitro network I/O.
- Future live ENA work still needs owned DMA pages, cache/memory ordering,
  bus-master enablement, register programming, MSI/MSI-X, bounded completion
  waits, and packet-path integration.
