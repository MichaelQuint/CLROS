# Step 315 - Owned Serial Diagnostics Boundary

## What Changed

- Added `IPlatformSerial` and `IPlatformSerialTransport` as the host-testable serial-output boundary for boot and panic markers.
- Added `GuardedPlatformSerial`, which allows firmware-backed serial before UEFI-gone and requires an owned serial transport after UEFI-gone simulation or firmware shutdown modes.
- Added `OwnedSerialDiagnostics`, which formats marker and panic preamble lines into caller-owned byte storage with CR/LF terminators and writes them through `IPlatformSerial`.
- Added host tests for firmware-backed writes, owned-serial writes after UEFI-gone simulation, firmware-backed rejection after UEFI-gone, missing owned transport, unavailable owned serial, short writes, marker formatting, panic preamble formatting, and no-managed-reference result structs.
- Updated diagnostics, boot, and completion-gate documentation to describe the boundary as preparation only.

## Why It Changed

Issue #312 needed a concrete boundary between firmware-console output and future OS-owned serial output before `ExitBootServices` work can advance. This step keeps the implementation host-testable and status-coded while avoiding a premature claim that real COM1 port I/O or post-firmware serial output exists.

## Files Changed

- `src/OS.Kernel/Platform/IPlatformSerial.cs`
- `src/OS.Kernel/Platform/GuardedPlatformSerial.cs`
- `src/OS.Kernel/Diagnostics/OwnedSerialDiagnostics.cs`
- `tests/OS.HostTests/OwnedSerialDiagnosticsTests.cs`
- `docs/boot.md`
- `docs/diagnostics.md`
- `docs/completion-gates.md`
- `done/step315.md`

## Evidence

Focused host-test project compile:

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo --no-dependencies
```

Result: passed with 0 warnings and 0 errors.

Full host-test run:

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

Result: emitted `TEST_PASS host.tests`.

Focused markers observed:

```text
TEST_PASS diagnostics.owned_serial writes firmware serial before uefi gone
TEST_PASS diagnostics.owned_serial writes owned serial after uefi gone
TEST_PASS diagnostics.owned_serial rejects firmware serial after uefi gone
TEST_PASS diagnostics.owned_serial reports missing owned transport
TEST_PASS diagnostics.owned_serial reports owned serial unavailable
TEST_PASS diagnostics.owned_serial reports short write
TEST_PASS diagnostics.owned_serial formats boot and panic markers
TEST_PASS diagnostics.owned_serial results contain no managed references
```

## Deferred Work

- No COM1 port-I/O backend is installed.
- No generated port-I/O stubs are invoked.
- No real `ExitBootServices` call is made.
- No live post-firmware serial output is proven.
- The live EFI path is not yet wired to this managed serial boundary.

## Evidence Boundary

This is host-tested boundary evidence only. It proves the status model, marker formatting, and UEFI-gone simulation guard behavior over injected transports. It does not prove hardware serial output, panic-safe serial in the live kernel, or output after real firmware shutdown.
