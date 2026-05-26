# Step 350 - Virtio PCI BAR register-window planning

## Summary

Added a host-tested `VirtioPciBarRegisterWindowPlanner` that validates
caller-provided mapped-BAR window records against successful virtio PCI
binding/aperture metadata and computes aligned register-access address plans for
a future BAR-backed accessor.

## Why

The fake queue setup and notification accessors now have host-tested sequencing
coverage, but the next live-MMIO prerequisite is a checked bridge from validated
virtio PCI apertures to caller-provided mapped-window metadata. This step adds
that bridge without mapping BARs or performing volatile register access.

## Files changed

- `src/OS.Kernel/Drivers/VirtioPciBarRegisterWindowPlan.cs`
- `tests/OS.HostTests/VirtioPciBarRegisterWindowPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/drivers.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step350.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtiopci\.registerwindow|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- `TEST_PASS drivers.virtiopci.registerwindow binds common notify isr device apertures`
- `TEST_PASS drivers.virtiopci.registerwindow plans aligned register access`
- `TEST_PASS drivers.virtiopci.registerwindow rejects missing mapping`
- `TEST_PASS drivers.virtiopci.registerwindow rejects base mismatch`
- `TEST_PASS drivers.virtiopci.registerwindow rejects range overflow`
- `TEST_PASS drivers.virtiopci.registerwindow rejects unaligned access`
- `TEST_PASS drivers.virtiopci.registerwindow preserves no-live-mmio boundary`
- `TEST_PASS host.tests`
- Host test build passed.
- Full solution build passed.
- `git diff --check` passed.

## Failures

- None.

## Deferred work

- Implement the live BAR-backed volatile accessor that consumes the register
  window plan.
- Define post-firmware MMIO ownership and cacheability/page mapping rules.
- Replace explicit barrier-readiness metadata with a real kernel-safe ordering
  primitive.
- Add DMA ownership/allocation rules before live virtio-net packet I/O.
- Prove one live notification/completion path under QEMU before claiming live
  virtio-net TX.

## Evidence boundary

This is host-tested BAR/MMIO register-window planning metadata only. It
validates caller-provided BAR mapping and aperture metadata for a future
volatile accessor and computes future register-access addresses. It does not map
BARs, perform volatile/live MMIO, execute memory barriers, read or write live
device registers, notify a live device, allocate or map DMA memory, process
interrupts, poll hardware, transfer packets, or prove networking, cloud, hosted
.NET 10, ASP.NET Minimal API, Kestrel, or production readiness.
