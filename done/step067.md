# Step 067 - Port I/O Operation Byte Plan

## What Changed

- Added `PortIoOperationPlan`, a host-tested C# contract for emitting one-byte x64 port-I/O operation stubs.
- Added deterministic opcode constants for:
  - `out dx, al`
  - `in al, dx`
- Added status-coded validation for invalid operation kinds and too-small caller-owned output buffers.
- Added host tests for opcode emission and invalid inputs.

## Why It Changed

Issue #84 needed a narrow bridge between the abstract COM1 writer and the future OS-owned C# shellcode backend. This step records the exact instruction bytes needed for byte-port reads and writes without executing privileged port I/O in host tests or claiming that a real serial driver exists.

## Files Changed

- `src/OS.Kernel/Diagnostics/PortIoOperationPlan.cs`
- `tests/OS.HostTests/PortIoOperationPlanTests.cs`
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

- Build passed with 0 warnings and 0 errors.
- Host tests emitted:
  - `TEST_PASS port io plan encodes out8 operation`
  - `TEST_PASS port io plan encodes in8 operation`
  - `TEST_PASS port io plan rejects invalid inputs`
  - `TEST_PASS host.tests`

## Failures

- No remaining test failures.

## Deferred Work

- This does not execute `out` or `in`.
- This is not wired into `Com1SerialWriter`.
- This does not prove serial output after firmware services are gone.

## Evidence Boundary

This step proves deterministic instruction-byte planning only. It is not COM1 hardware-driver, post-firmware serial, or QEMU serial evidence.
