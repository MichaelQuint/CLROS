# Step 353 - Virtio PCI register-window fake execution

## Summary

Added `VirtioPciBarRegisterWindowAccessExecutor`, a host-tested fake executor
for already-validated virtio PCI BAR register-window access plans over
caller-owned BAR-window byte spans.

## Why

The register-window planner can now prove future common, notify, ISR, and
device-configuration register addresses. A narrow fake executor lets host tests
exercise the checked access metadata against byte spans before a live
BAR-backed volatile accessor, page mapping, cacheability policy, and MMIO
ownership rules exist.

## Files changed

- `src/OS.Kernel/Drivers/VirtioPciBarRegisterWindowPlan.cs`
- `tests/OS.HostTests/VirtioPciBarRegisterWindowPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step353.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtiopci\.registerwindow\.fakeexec|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- `TEST_PASS drivers.virtiopci.registerwindow.fakeexec writes planned word access`
- `TEST_PASS drivers.virtiopci.registerwindow.fakeexec reads planned dword access`
- `TEST_PASS drivers.virtiopci.registerwindow.fakeexec rejects missing access plan`
- `TEST_PASS drivers.virtiopci.registerwindow.fakeexec rejects short window buffer`
- `TEST_PASS drivers.virtiopci.registerwindow.fakeexec preserves no-live-mmio boundary`
- `TEST_PASS host.tests`
- Host test build passed.
- Full solution build passed.
- `git diff --check` passed.

## Failures

- None.

## Deferred work

- Add a live BAR-backed volatile accessor that consumes checked
  register-window access plans.
- Define page mapping, cacheability, and MMIO ownership rules before live BAR
  access.
- Replace fake byte-span register execution and fake virtio-net accessors with
  a live accessor only after memory-ordering and DMA ownership rules are ready.
- Prove one live queue setup and notification path under QEMU before claiming
  live virtio-net TX.

## Evidence boundary

This is host-tested fake register-window execution evidence over caller-owned
byte spans representing already-validated virtio PCI BAR windows. It proves
that a successful `VirtioPciBarRegisterWindowAccessPlan` can be used to read or
write little-endian 1, 2, 4, or 8 byte values in caller-owned memory with
status-coded failures. It does not map BARs, create page mappings, change
cacheability attributes, dereference physical addresses, perform volatile/live
MMIO, execute memory barriers, configure virtqueues, notify a live device,
process interrupts, allocate or map DMA memory, poll hardware, transfer
packets, or prove live PCI, networking, cloud, hosted .NET 10, ASP.NET Minimal
API, Kestrel, or production readiness.
