# Step 069 - Callable Port I/O Stub Byte Plan

## What Changed

- Added `PortIoStubPlan`, a host-tested C# byte-plan contract for future callable COM1 port-I/O stubs.
- Added deterministic callable stubs for:
  - `Out8`: move `CX` to `DX`, move `DL` to `AL`, emit `out dx, al`, then `ret`.
  - `In8`: move `CX` to `DX`, emit `in al, dx`, then `ret`.
- Reused `PortIoOperationPlan` for the final privileged operation byte.
- Added status-coded validation for invalid operation kinds and too-small caller-owned output buffers.
- Added host tests for both callable stub byte sequences and invalid inputs.

## Why It Changed

Issue #87 needed a narrow bridge from the abstract COM1 writer contract to a future C#-emitted executable stub. This records the exact bytes without executing privileged port I/O in host tests.

## Files Changed

- `src/OS.Kernel/Diagnostics/PortIoStubPlan.cs`
- `tests/OS.HostTests/PortIoStubPlanTests.cs`
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

- Worker evidence showed build passed with 0 warnings and 0 errors.
- Worker evidence showed host tests emitted:
  - `TEST_PASS port io stub plan encodes callable out8 stub`
  - `TEST_PASS port io stub plan encodes callable in8 stub`
  - `TEST_PASS port io stub plan rejects invalid inputs`
  - `TEST_PASS host.tests`

## Failures

- No source failure was reported.
- The parent run later hit stale `OS.HostTests.exe` file locks from old harness processes; those were stopped before continuing verification.

## Deferred Work

- This does not execute `out` or `in`.
- This is not wired into `Com1SerialWriter`.
- This does not allocate executable stub memory or define the W^X transition policy.
- This does not prove serial output after firmware services are gone.

## Evidence Boundary

This step proves deterministic callable stub byte planning only. It is not COM1 hardware-driver, post-firmware serial, executable-memory, or QEMU serial evidence.
