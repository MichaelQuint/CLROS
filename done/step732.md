# Step 732 - Serial Smoke Marker Contract

## Summary

Centralized the owned-serial smoke marker contract in `OwnedSerialSmokeMarkers`. The contract now exposes marker constants for `TEST_START`, `TEST_PASS`, `TEST_FAIL`, `OS_BOOT_OK`, `OS_SMOKE_PASS`, and `OS_SMOKE_FAIL`, recognizes valid test-marker prefixes, validates bounded ASCII smoke markers, and keeps owned-serial marker formatting in caller-owned buffers.

## Why

The diagnostics path needs one reusable source for machine-readable serial markers before more boot, panic, and QEMU smoke paths depend on them. This slice makes marker names and validation host-testable without adding allocations or changing the live serial transport boundary.

## Files changed

- `src/OS.Kernel/Diagnostics/OwnedSerialSmokeMarkers.cs`
- `src/OS.Kernel/Diagnostics/OwnedSerialDiagnostics.cs`
- `tests/OS.HostTests/OwnedSerialSmokeMarkersTests.cs`
- `tests/OS.HostTests/OwnedSerialDiagnosticsTests.cs`
- `docs/diagnostics.md`
- `done/step732.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- `artifacts\logs\issue697-host-build-20260525.log`: host test project build succeeded.
- `artifacts\logs\issue697-hosttests-20260525.log`: host tests passed with:
  - `TEST_PASS diagnostics.serialmarkers exposes smoke marker constants`
  - `TEST_PASS diagnostics.serialmarkers recognizes test marker prefixes`
  - `TEST_PASS diagnostics.serialmarkers rejects invalid markers`
  - `TEST_PASS diagnostics.serialmarkers preserves no-allocation contract`
  - `TEST_PASS host.tests`
- `artifacts\logs\issue697-git-diff-check-postdone-20260525.log`: `git diff --check` exited 0; output contains only existing LF-to-CRLF working-copy warnings.

## Failures

- No test failure is known for this slice.

## Deferred work

- Route future boot, panic, and QEMU smoke emitters through the centralized constants where they still build marker text directly.
- Add serial log parser tests for full boot-smoke transcripts after the next QEMU runner slice.

## Evidence boundary

This is host-tested marker formatting and validation only. It does not prove live COM1 port I/O, IDT fault handling, QEMU boot, `ExitBootServices`, panic unwinding, cloud-provider support, or production readiness.
