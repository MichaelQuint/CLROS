# Step 295 - Guarded Platform Timer Boundary

## Summary

Added the first `IPlatformTimer` guard boundary for firmware-backed monotonic timer reads. The boundary returns monotonic tick/frequency metadata before UEFI-gone, denies reads after UEFI-gone simulation or marked `ExitBootServices`, and reports invalid zero-frequency metadata through status codes.

## Why

UEFI-gone simulation needs every firmware-backed platform surface to prove that it stops using boot services before a real firmware shutdown path is enabled. Timer reads are now covered by the same guard shape as console, memory-map, and configuration-table access without claiming a real hardware timer implementation.

## Files changed

- `src/OS.Kernel/Platform/IPlatformTimer.cs`
- `src/OS.Kernel/Platform/GuardedPlatformTimer.cs`
- `tests/OS.HostTests/PlatformTimerGuardTests.cs`
- `docs/boot.md`
- `docs/pal.md`
- `docs/completion-gates.md`
- `done/step295.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence

- Build completed with `Build succeeded`, `0 Warning(s)`, and `0 Error(s)`.
- Host tests completed with `TEST_PASS host.tests`.
- Timer guard markers emitted:
  - `TEST_PASS platform.timer monotonic allowed before UEFI gone`
  - `TEST_PASS platform.timer monotonic denied in UEFI-gone simulation`
  - `TEST_PASS platform.timer monotonic denied after ExitBootServices`
  - `TEST_PASS platform.timer monotonic reports invalid metadata`
  - `TEST_PASS platform.timer metadata contains no managed references`

## Failures

- None.

## Deferred work

- No HPET, APIC timer, TSC calibration, scheduler wake, wait integration, `Task.Delay`, hosted .NET timer, or Kestrel timeout behavior is implemented by this step.
- The focused timer guard tests cover direct `FirmwareServicesGuard` simulation/exited states; the live `PlatformUefiGoneSimulation` marker flow still verifies console, memory-map, and configuration-table adapters only.

