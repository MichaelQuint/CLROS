# Step 784 - Issue 741 owned serial diagnostics builder formatter

## Summary
Migrated `OwnedSerialDiagnostics.FormatMarkerLine` from direct marker
`CopyTo` plus direct CR/LF byte assignments to checked
`OS.Std.Text.AsciiTextBuilder.AppendBytes` and `AppendByte` calls.

## Why
The owned serial diagnostics formatter is part of the post-firmware diagnostics
readiness path. Using the shared no-runtime ASCII text builder keeps marker
line formatting aligned with the common `std/no-runtime/shared` surface while
preserving the existing validation and status-coded failure contract.

## Files changed
- `src/OS.Kernel/Diagnostics/OwnedSerialDiagnostics.cs`
- `tests/OS.HostTests/OwnedSerialDiagnosticsTests.cs`
- `docs/diagnostics.md`
- `done/step784.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue741-owned-serial-diagnostics-builder\bin\Debug\net10.0 *>&1 | Tee-Object -FilePath .\artifacts\logs\issue741-host-build-20260525.log`
- `dotnet .\artifacts\issue741-owned-serial-diagnostics-builder\bin\Debug\net10.0\OS.HostTests.dll *>&1 | Tee-Object -FilePath .\artifacts\logs\issue741-hosttests-20260525.log`
- Scoped source guard:
  `TEST_PASS diagnostics.owned_serial formatter uses shared ascii text builder`
- `git diff --check -- .\src\OS.Kernel\Diagnostics\OwnedSerialDiagnostics.cs .\tests\OS.HostTests\OwnedSerialDiagnosticsTests.cs .\docs\diagnostics.md .\done\step784.md *>&1 | Tee-Object -FilePath .\artifacts\logs\issue741-git-diff-check-scoped-20260525.log`
- `rg -n "[ \t]$" .\src\OS.Kernel\Diagnostics\OwnedSerialDiagnostics.cs .\tests\OS.HostTests\OwnedSerialDiagnosticsTests.cs .\docs\diagnostics.md .\done\step784.md`

## Evidence
- `artifacts/logs/issue741-host-build-20260525.log` shows the focused host-test
  build succeeded with `0 Warning(s)` and `0 Error(s)`.
- `artifacts/logs/issue741-hosttests-20260525.log` captured the required host
  test command but it failed before reaching the owned-serial diagnostics test
  group because `CiSmokeScriptTests` reported
  `TEST_FAIL ci.smoke returns nonzero on failed command`.
- `artifacts/logs/issue741-sourceguard-20260525.log` records
  `TEST_PASS diagnostics.owned_serial formatter uses shared ascii text builder`.
- `artifacts/logs/issue744-hosttests-20260525.log` provides later integrated
  full host-suite evidence after the out-of-scope CI smoke failure was cleared:
  it contains `TEST_PASS diagnostics.owned_serial formatter uses shared ascii
  text builder` and final `TEST_PASS host.tests`.
- `artifacts/logs/issue741-git-diff-check-scoped-20260525.log` contains only
  Git LF-to-CRLF normalization warnings for the tracked scoped files. A
  follow-up `$LASTEXITCODE` probe for the same `git diff --check` inputs
  reported `GIT_DIFF_CHECK_LASTEXITCODE 0`.
- `artifacts/logs/issue741-trailing-whitespace-20260525.log` records
  `TEST_PASS issue741.scoped_trailing_whitespace` after `rg -n "[ \t]$"` found
  no matches in the changed source, test, doc, or done files.
- `OwnedSerialDiagnostics.FormatMarkerLine` still validates markers and
  destination size before writing. Any unexpected shared-builder failure maps
  to the existing `InvalidMarker` or `DestinationTooSmall` formatter statuses.

## Failures
- `dotnet .\artifacts\issue741-owned-serial-diagnostics-builder\bin\Debug\net10.0\OS.HostTests.dll` exited 1. The observed failure is in the pre-existing
  CI smoke script host tests:
  `TEST_FAIL ci.smoke failures=3`,
  `TEST_FAIL ci.smoke returns nonzero on failed command`, and
  `System.InvalidOperationException: CI smoke script tests failed: 1`.
  The failing scenario expected only the fake `dotnet` build/host-test lanes to
  fail, but the generated CI smoke run also recorded
  `TEST_FAIL ci.smoke.qemu.dryrun exit_code=1`. This is outside the owned
  serial formatter scope.
- The scoped `git diff --check ... *>&1 | Tee-Object ...` PowerShell pipeline
  returned a nonzero shell status because Git wrote LF-to-CRLF warnings to
  stderr and PowerShell surfaced them as `NativeCommandError` records. The same
  Git inputs reported `$LASTEXITCODE` 0 when run without the Tee pipeline.

## Deferred work
- Live COM1 port-I/O backend invocation.
- Real post-`ExitBootServices` serial writes.
- Panic-safe framebuffer output.
- Live IDT/GDT/TSS/IST installation and CPU fault handling.
- Hosted .NET, ASP.NET Minimal API, Kestrel, and cloud-provider evidence.

## Evidence boundary
This step proves the host-tested owned serial marker line formatter uses the
shared ASCII text builder and preserves status-coded formatter behavior. It does
not prove live COM1 hardware output, a real firmware shutdown, `ExitBootServices`
execution, QEMU boot behavior, hosted .NET, Kestrel, cloud readiness, or
production readiness.
