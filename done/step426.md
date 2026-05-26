# Step 426 - Virtio-net queue-plan default-result guard

## Summary

Hardened `VirtioNetQueuePlanResult` so a default/zero result is not reported
as successful. Planner-created results now carry explicit initialization
provenance, and `Succeeded` requires that provenance in addition to the existing
success status.

## Why

The virtio-net RX/TX lifecycle code trusts successful queue-plan metadata before
publishing descriptors into caller-owned split-ring memory. Because the queue
plan success enum value is zero, `default(VirtioNetQueuePlanResult)` could
previously satisfy `Succeeded`. This step blocks that accidental promotion
without changing successful planning or existing failure-status behavior.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetQueuePlan.cs`
  - Added `IsInitialized` to `VirtioNetQueuePlanResult`.
  - Set the initialization bit in the private result constructor.
  - Updated `Succeeded` to require initialization plus success status.
- `tests/OS.HostTests/VirtioNetQueuePlanTests.cs`
  - Added default-result regression coverage.
- `docs/networking.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
  - Documented the default-result guard and marker while preserving the
    host-tested queue-planning evidence boundary.
- `done/step426.md`
  - Recorded this milestone.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet queue plan default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Host test project build: passed with 0 warnings and 0 errors.
- Host test run: passed with `TEST_PASS host.tests`.
- Filtered marker capture:
  - `TEST_PASS drivers.virtionet queue plan default result is not success`
  - `TEST_PASS host.tests`
- Full solution build: passed with 0 warnings and 0 errors.
- Naming/source guard:
  - `TEST_PASS naming.guard`
  - `TEST_PASS source.firewall`
- `git diff --check`: exit code 0. Git emitted CRLF normalization warnings
  for existing working-copy paths only.

## Failures

None.

## Deferred work

- Continue auditing neighboring virtio-net and storage result structs for
  default-success shapes in small issue-sized slices.
- Keep this evidence scoped to host-tested queue planning. It does not prove
  live virtio-net MMIO, DMA, interrupts, packet I/O, networking readiness,
  hosted .NET 10, ASP.NET Minimal API/Kestrel, cloud readiness, or production
  readiness.

