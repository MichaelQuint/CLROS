# Step 221 - ENA Admin Queue Layout Planner

## Summary

Added `EnaAdminQueueLayoutPlan`, a host-tested memory layout planner for the
future ENA Admin Queue and Admin Completion Queue.

## Why

Issue #245 needed the next ENA/Nitro slice to stop before DMA allocation or BAR
programming. The planner gives later ENA admin-queue code deterministic offsets,
lengths, alignment metadata, total required bytes, contiguous-memory intent, and
initial AQ/ACQ index and phase metadata while preserving status-coded failure
paths.

## Files changed

- `src/OS.Kernel/Drivers/EnaAdminQueueLayoutPlan.cs`
- `tests/OS.HostTests/EnaAdminQueueLayoutPlanTests.cs`
- `docs/drivers.md`
- `docs/ena-driver-plan.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence

- `TEST_PASS drivers.ena.admin.queue plans aq acq layout`
- `TEST_PASS drivers.ena.admin.queue rejects invalid depth`
- `TEST_PASS drivers.ena.admin.queue rejects invalid entry size`
- `TEST_PASS drivers.ena.admin.queue rejects insufficient memory`
- `TEST_PASS drivers.ena.admin.queue reports arithmetic overflow`
- `TEST_PASS host.tests`

## Failures

None in the focused build and host-test run.

## Deferred work

- No pages are allocated or mapped.
- No physical addresses are assigned to the planned regions.
- No ENA BAR registers are written.
- No admin queue doorbell is rung.
- No completion is polled.
- No AENQ, TX/RX queue, packet I/O, DHCP, TCP, hosted .NET, Minimal API, or
  Kestrel behavior is claimed.
