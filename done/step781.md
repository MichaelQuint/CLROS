# Step 781 - Owned serial smoke marker shared ASCII builder

## Summary
Migrated `OwnedSerialSmokeMarkers` formatting paths from direct span copies and
byte writes to `OS.Std.Text.AsciiTextBuilder` while preserving the stable
`TEST_*`, `OS_BOOT_OK`, `OS_SMOKE_PASS`, and `OS_SMOKE_FAIL` marker contract.

## Why
Owned serial markers sit on the diagnostics path that will remain useful after
UEFI services are gone. Routing marker text construction through the shared
no-runtime ASCII builder keeps early diagnostics aligned with the common std
surface and removes local copy/write helpers from marker formatting.

## Files changed
- `src/OS.Kernel/Diagnostics/OwnedSerialSmokeMarkers.cs`
- `tests/OS.HostTests/OwnedSerialSmokeMarkersTests.cs`
- `docs/diagnostics.md`
- `done/step781.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue739-owned-serial-smoke-marker-builder\bin\Debug\net10.0 *>&1 | Tee-Object -FilePath .\artifacts\logs\issue739-host-build-20260525.log`
- `dotnet .\artifacts\issue739-owned-serial-smoke-marker-builder\bin\Debug\net10.0\OS.HostTests.dll *>&1 | Tee-Object -FilePath .\artifacts\logs\issue739-hosttests-20260525.log`
- `git diff --check -- .\src\OS.Kernel\Diagnostics\OwnedSerialSmokeMarkers.cs .\tests\OS.HostTests\OwnedSerialSmokeMarkersTests.cs .\docs\diagnostics.md .\done\step781.md *>&1 | Tee-Object -FilePath .\artifacts\logs\issue739-git-diff-check-scoped-final2-20260525.log`
- `rg -n "[ \t]$" .\src\OS.Kernel\Diagnostics\OwnedSerialSmokeMarkers.cs .\tests\OS.HostTests\OwnedSerialSmokeMarkersTests.cs .\docs\diagnostics.md .\done\step781.md`

## Evidence
- `artifacts/logs/issue739-host-build-20260525.log` shows the focused host-test
  build succeeded with `0 Warning(s)` and `0 Error(s)`.
- `artifacts/logs/issue739-hosttests-20260525.log` ends with
  `TEST_PASS host.tests` and includes:
  - `TEST_PASS diagnostics.serialmarkers exposes smoke marker constants`
  - `TEST_PASS diagnostics.serialmarkers recognizes test marker prefixes`
  - `TEST_PASS diagnostics.serialmarkers rejects invalid markers`
  - `TEST_PASS diagnostics.serialmarkers preserves no-allocation contract`
  - `TEST_PASS diagnostics.serialmarkers uses shared ascii text builder`
- `artifacts/logs/issue739-git-diff-check-scoped-final2-20260525.log` exited 0 while
  reporting only the known LF-to-CRLF normalization warning for
  `docs/diagnostics.md`.
- The scoped trailing-whitespace source scan returned
  `TEST_PASS issue739.scoped_whitespace_source_final2`.

## Failures
- None.

## Deferred work
- Live COM1 port-I/O backend invocation, real post-`ExitBootServices` serial
  writes, panic-safe framebuffer output, live IDT/GDT/TSS/IST installation,
  null-deref/invalid-opcode QEMU fault probes, hosted .NET, ASP.NET Minimal
  API, and Kestrel remain outside this formatter migration.

## Evidence boundary
This step is intended to prove host-tested caller-owned formatting of serial
smoke marker text through the shared ASCII text builder. It does not prove live
serial hardware output, real firmware-service teardown, panic handling on CPU
faults, QEMU boot behavior, hosted .NET, or cloud-provider behavior.

