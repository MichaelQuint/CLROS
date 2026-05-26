# Step 318 - COM1 Platform Serial Transport Boundary

## What Changed

- Added `Com1PlatformSerialTransport`, an owned `IPlatformSerialTransport` adapter over `Com1SerialWriter`.
- Extended `PlatformSerialTransportResult.Written(...)` with a backend-status overload so partial successful transport writes can still carry compact COM1 status metadata through `GuardedPlatformSerial` as `ShortWrite`.
- Added host tests for fake COM1 backend writes, UEFI-gone owned serial writes through `GuardedPlatformSerial`, unavailable readiness, initialization failure mapping, transmit-timeout mapping, short-write reporting, and no-managed-reference result layout.
- Updated diagnostics, boot, driver, and completion-gate docs to record the new host-tested adapter and its evidence boundary.

## Why

Issue #316 needed a narrow owned-serial adapter before live post-firmware diagnostics can be wired into the boot boundary. The repository already had COM1 configuration and writer contracts plus the guarded serial abstraction, but there was no concrete transport connecting them.

This step keeps the boundary fakeable and status-coded. It proves the owned serial path can flow through `GuardedPlatformSerial` after UEFI-gone simulation without claiming real CPU port I/O or `ExitBootServices`.

## Files Changed

- `src/OS.Kernel/Diagnostics/Com1PlatformSerialTransport.cs`
- `src/OS.Kernel/Platform/IPlatformSerial.cs`
- `tests/OS.HostTests/Com1PlatformSerialTransportTests.cs`
- `docs/diagnostics.md`
- `docs/drivers.md`
- `docs/boot.md`
- `docs/completion-gates.md`
- `done/step318.md`

## Evidence

Commands run:

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'diagnostics\.com1_transport|TEST_PASS host\.tests|TEST_FAIL'
dotnet build .\OS.slnx --nologo -m:1
git diff --check
```

Observed markers:

```text
TEST_PASS diagnostics.com1_transport writes through fake com1 backend
TEST_PASS diagnostics.com1_transport writes owned serial after uefi gone
TEST_PASS diagnostics.com1_transport reports unavailable readiness
TEST_PASS diagnostics.com1_transport maps initialization failure
TEST_PASS diagnostics.com1_transport maps transmit timeout
TEST_PASS diagnostics.com1_transport reports short write
TEST_PASS diagnostics.com1_transport results contain no managed references
TEST_PASS host.tests
```

`dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.

`git diff --check` exited 0. Git reported LF-to-CRLF working-copy warnings for existing text files, but no whitespace errors.

## Failures

- The first test pass incorrectly counted the COM1 divisor-latch write to the data register as payload data. The fake backend was corrected to treat the first seven COM1 initialization writes as initialization traffic before asserting payload bytes.
- A second focused run failed because the test asserted the older `diagnostics.owned_serial` marker prefix while writing a `diagnostics.com1_transport` marker. The assertion was narrowed to the marker actually written by the test.

## Deferred Work

- No real CPU port-I/O backend is installed.
- No generated `out dx, al` / `in al, dx` stubs are mapped executable or invoked.
- No real `ExitBootServices` path uses this transport.
- No QEMU evidence proves serial output after firmware shutdown.
- Panic-safe owned serial output still needs a live backend and panic-path integration.

## Evidence Boundary

This step proves a host-tested owned serial transport over a fakeable COM1 byte-port backend. It is not live COM1 hardware access, not post-`ExitBootServices` serial output, and not fault-diagnostic proof.
