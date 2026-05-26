# Step 230 - ENA Admin Command Boundary Planner

## Summary

Added a host-tested ENA admin command execution-boundary planner for
`GET_FEATURE DEVICE_ATTRIBUTES`.

## Why

Issue #255 needed a deterministic contract between the existing ENA descriptor
helpers and any future live admin queue executor. This keeps command planning,
submission descriptor writing, completion matching, timeout-budget validation,
and device-attributes response validation testable before BAR access, DMA,
doorbells, interrupts, or real network I/O exist.

## Files changed

- `src/OS.Kernel/Drivers/EnaAdminCommandPlan.cs`
- `tests/OS.HostTests/EnaAdminCommandPlanTests.cs`
- `docs/ena-driver-plan.md`
- `done/step230.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1 --no-restore`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.ena\.admin\.command|host\.tests'`
- `git diff --check`

## Evidence

- The first solution build after adding the ENA planner passed with
  `Build succeeded.`, `0 Warning(s)`, and `0 Error(s)`.
- A later solution build was temporarily blocked by an unrelated concurrent
  ManifestTool edit referencing `KestrelSmokePackageCommand`; that file landed
  afterward, and the final solution build passed with `Build succeeded.`,
  `0 Warning(s)`, and `0 Error(s)`.
- Focused `OS.Kernel` build passed with `Build succeeded.`, `0 Warning(s)`,
  and `0 Error(s)`.
- Focused host test output emitted:
  - `TEST_PASS drivers.ena.admin.command plans get feature`
  - `TEST_PASS drivers.ena.admin.command validates completion`
  - `TEST_PASS drivers.ena.admin.command rejects timeout budget`
  - `TEST_PASS drivers.ena.admin.command rejects invalid inputs`
  - `TEST_PASS drivers.ena.admin.command rejects completion failures`
  - `TEST_PASS host.tests`
- `git diff --check` exited 0. It printed line-ending normalization warnings
  for existing modified files only.

## Failures

- A rerun of the full solution build failed once because a stale `OS.HostTests`
  process held `OS.Kernel.dll` and `OS.Std.NoRuntime.Shared.dll` open. The
  process had exited by inspection time, and the next full build passed.

## Deferred work

- The planner is not a live admin queue executor.
- It performs no live BAR reads/writes, MMIO, DMA allocation, doorbells,
  MSI/MSI-X setup, interrupts, packet I/O, DHCP, TCP, HTTP, or real network I/O.
- Future work must provide DMA-backed AQ/ACQ memory, mapped register apertures,
  memory-ordering rules, a doorbell writer, and a bounded completion wait policy
  before ENA admin results can be treated as hardware evidence.
