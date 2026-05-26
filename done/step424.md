# Step 424 - Virtio-net RX readiness default-result guard

## Summary

Hardened `VirtioNetReceiveReadinessResult` so a default/zero result is not
reported as successful. Planner-created results now carry explicit
initialization provenance, and `Succeeded` requires that provenance in addition
to the existing success status.

## Why

The RX completion and dispatch register-window planners depend on
`VirtioNetReceiveReadinessResult.Succeeded` before reading caller-owned queue
memory and receive buffers. Because the success enum value is zero,
`default(VirtioNetReceiveReadinessResult)` could previously satisfy
`Succeeded`. This step preserves the existing host-tested RX readiness behavior
while blocking accidental default-result promotion.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetReceiveReadinessPlan.cs`
  - Added `IsInitialized` to `VirtioNetReceiveReadinessResult`.
  - Set the initialization bit in the private result constructor.
  - Updated `Succeeded` to require initialization plus success status.
- `tests/OS.HostTests/VirtioNetReceiveReadinessPlanTests.cs`
  - Added the default-result regression marker.
- `docs/networking.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
  - Documented the default-result guard and marker while preserving the
    host-tested, no-live-MMIO evidence boundary.
- `done/step424.md`
  - Recorded this milestone.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Host test build succeeded with 0 warnings and 0 errors.
- Full host test run completed with `TEST_PASS host.tests`.
- Full solution build succeeded with 0 warnings and 0 errors.
- Naming/source firewall succeeded:
  - `TEST_PASS naming.guard`
  - `TEST_PASS source.firewall`
- `git diff --check` exited 0; output contained only CRLF normalization
  warnings for already-edited text files.
- Added marker:
- `TEST_PASS drivers.virtionet.rx.readiness default result is not success`

## Failures

None.

## Deferred work

- Audit neighboring virtio-net result structs for the same default-success
  pattern and split any fixes into small issues.
- Keep this evidence scoped to host-tested caller-owned/register-window fake
  execution. It does not prove live virtio-net MMIO, DMA, interrupts, packet
  I/O, hosted .NET 10, ASP.NET Minimal API/Kestrel, cloud readiness, or
  production readiness.
