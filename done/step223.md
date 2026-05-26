# Step 223 - ENA register map and admin readiness metadata

## Summary

Added the host-testable ENA register-map and metadata-only admin readiness
planner for issue #247.

## Why

Live ENA bring-up needs centralized register offsets, bit masks, and
deterministic readiness rejection before any BAR mapping, MMIO access, DMA
queue allocation, admin descriptor execution, interrupts, or packet I/O are
introduced.

## Files changed

- `src/OS.Kernel/Drivers/EnaRegisterMap.cs`
- `tests/OS.HostTests/EnaRegisterMapTests.cs`
- `docs/ena-driver-plan.md`
- `docs/drivers.md`
- `done/step223.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- Build passed with `0 Warning(s)` and `0 Error(s)`.
- Host tests passed and emitted:
  - `TEST_PASS drivers.ena.registermap register offsets`
  - `TEST_PASS drivers.ena.registermap capability and status bits`
  - `TEST_PASS drivers.ena.registermap validates admin aperture`
  - `TEST_PASS drivers.ena.registermap readiness accepts ready metadata`
  - `TEST_PASS drivers.ena.registermap readiness rejects unsupported reset busy states`
  - `TEST_PASS host.tests`
- `git diff --check` exited successfully.

## Failures

- None for this slice.
- `git diff --check` printed existing line-ending warnings for modified docs in
  the shared worktree, but returned success.

## Deferred work

- No ENA PCI probing, BAR sizing/mapping, MMIO reads/writes, DMA allocation,
  admin descriptor submission, interrupt setup, MAC retrieval, TX/RX queues,
  DHCP, TCP, HTTP, hosted .NET 10, or Kestrel evidence.

