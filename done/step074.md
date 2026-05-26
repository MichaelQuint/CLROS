# Step 074 - COM1 Port I/O Backend Readiness Plan

## What Changed

- Added `Com1PortIoBackendPlan`, a host-tested readiness contract for a future OS-owned COM1 port-I/O backend.
- Validates:
  - COM1 serial configuration;
  - `Out8` and `In8` operation plans;
  - callable `Out8` and `In8` stub plans;
  - executable-stub memory layout capacity.
- Added distinct status codes for invalid serial configuration, missing operation plan, missing stub plan, unavailable memory plan, and insufficient memory plan.
- Added host tests for ready prerequisites and each failure category.

## Why It Changed

Issue #92 needed a concrete readiness gate between byte plans and a future live COM1 backend. This lets later boot code refuse to treat firmware console output as post-firmware diagnostics until the serial backend prerequisites are explicit.

## Files Changed

- `src/OS.Kernel/Diagnostics/Com1PortIoBackendPlan.cs`
- `tests/OS.HostTests/Com1PortIoBackendPlanTests.cs`
- `docs/diagnostics.md`
- `docs/drivers.md`
- `docs/boot-order.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Build passed with 0 warnings and 0 errors in worker evidence.
- Parent build passed after a minor `nuint` signature fix; one run printed transient file-copy retry warnings from an active host-test process, then completed successfully.
- Host tests emitted:
  - `TEST_PASS com1 port io backend plan accepts ready prerequisites`
  - `TEST_PASS com1 port io backend plan rejects invalid serial config`
  - `TEST_PASS com1 port io backend plan rejects missing stub readiness`
  - `TEST_PASS com1 port io backend plan rejects insufficient stub memory plan`
  - `TEST_PASS host.tests`

## Failures

- Initial parent build saw `int`/`nuint` mismatches in the new readiness result. The result signature was corrected to use `nuint` for available stub bytes.

## Deferred Work

- This does not execute port I/O.
- This does not allocate executable memory.
- This does not install a real backend into `Com1SerialWriter`.
- This does not prove serial output after firmware services are gone.

## Evidence Boundary

This step proves host-side COM1 backend prerequisite validation only. It is not COM1 hardware-driver, post-firmware serial, executable-memory, or QEMU serial evidence.
