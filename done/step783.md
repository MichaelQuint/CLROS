# Step 783 - PAL status-domain formatter shared ASCII builder

## Summary
Migrated the PAL `OSHostStatusContract.FormatDomainName` successful write path
from direct span copying to the shared no-runtime `AsciiTextBuilder.AppendBytes`
append path, and added a host source guard for that formatter.

## Why
The status-domain formatter is part of the PAL-facing status contract surface.
Routing its caller-owned ASCII formatting through the shared builder keeps PAL
diagnostic/status helpers aligned with the no-runtime std text surface while
preserving status-coded validation and failure behavior.

## Files changed
- `src/OS.Kernel/Pal/OSHostStatusContract.cs`
- `tests/OS.HostTests/PalStatusContractTests.cs`
- `docs/pal.md`
- `done/step783.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue740-pal-status-contract-builder\bin\Debug\net10.0 *>&1 | Tee-Object -FilePath .\artifacts\logs\issue740-host-build-20260525.log`
- `dotnet .\artifacts\issue740-pal-status-contract-builder\bin\Debug\net10.0\OS.HostTests.dll *>&1 | Tee-Object -FilePath .\artifacts\logs\issue740-hosttests-20260525.log`
- `git diff --check -- .\src\OS.Kernel\Pal\OSHostStatusContract.cs .\tests\OS.HostTests\PalStatusContractTests.cs .\docs\pal.md .\done\step783.md *>&1 | Tee-Object -FilePath .\artifacts\logs\issue740-git-diff-check-scoped-20260525.log`
- `rg -n "[ \t]$" .\src\OS.Kernel\Pal\OSHostStatusContract.cs .\tests\OS.HostTests\PalStatusContractTests.cs .\docs\pal.md .\done\step783.md`

## Evidence
- `artifacts/logs/issue740-host-build-20260525.log` shows the focused host-test
  build succeeded with `0 Warning(s)` and `0 Error(s)`.
- `artifacts/logs/issue740-hosttests-20260525.log` ends with
  `TEST_PASS host.tests` and includes:
  - `TEST_PASS pal.status_contract formats domain names`
  - `TEST_PASS pal.status_contract formats extended domain names`
  - `TEST_PASS pal.status_contract domain formatter uses shared ascii text builder`
- `artifacts/logs/issue740-git-diff-check-scoped-20260525.log` records only
  Git LF-to-CRLF normalization warnings for the scoped files and
  `GIT_DIFF_CHECK_LASTEXITCODE=0`; no whitespace errors were reported.
- `artifacts/logs/issue740-rg-trailing-whitespace-20260525.log` records
  `TEST_PASS issue740.scoped_whitespace_source`, confirming the scoped
  trailing-whitespace scan returned no matches.

## Failures
- None.

## Deferred work
- Exported unmanaged `OSHost_*` PAL diagnostics/status functions.
- Hosted .NET 10/CoreCLR PAL consumption.
- Real PAL memory/file/config/socket/timer/thread/wait exports.
- Live guest PAL status diagnostics, ASP.NET Minimal API, Kestrel, cloud, or
  production readiness evidence.

## Evidence boundary
This step proves host-tested PAL status-domain name formatting now uses the
shared no-runtime ASCII text builder append path while preserving existing
status-coded validation. It does not export PAL symbols, allocate ABI-owned
return buffers, write serial diagnostics, start hosted .NET, run Kestrel, prove
live firmware shutdown, or change cloud-readiness status.

