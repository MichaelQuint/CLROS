# Step 059 - COM1 Serial Configuration Plan

## What Changed

- Added `SerialPortConfiguration`, a host-testable COM1 configuration and initialization-plan contract.
- Added deterministic COM1 defaults for base port `0x3F8`, 115200 baud, and 8N1 line control.
- Added divisor-latch calculation from input clock and baud rate.
- Added deterministic initialization port/value write sequencing into caller-provided storage.
- Added status-coded failures for invalid base port, input clock, baud rate, divisor range, and output buffer size.

## Why It Changed

Issue #77 needed a serial-driver precursor that defines configuration math and write ordering without performing real port I/O. This prepares the next OS-owned serial writer while preserving the evidence boundary.

## Files Changed

- `src/OS.Kernel/Diagnostics/SerialPortConfiguration.cs`
- `tests/OS.HostTests/SerialPortConfigurationTests.cs`
- `docs/diagnostics.md`
- `docs/boot-order.md`
- `docs/completion-gates.md`
- `docs/drivers.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Build passed with 0 warnings and 0 errors.
- Host tests emitted:
  - `TEST_PASS serial port config exposes com1 defaults`
  - `TEST_PASS serial port config calculates divisor latch`
  - `TEST_PASS serial port config emits init sequence`
  - `TEST_PASS serial port config rejects invalid inputs`
  - `TEST_PASS host.tests`

## Failures

- No remaining test failures.

## Deferred Work

- This does not perform real port I/O.
- This does not detect hardware.
- This does not implement a serial writer.
- This does not prove post-`ExitBootServices` serial output.

## Evidence Boundary

This step proves COM1 configuration math and planned initialization write ordering only. It is not serial-driver evidence.
