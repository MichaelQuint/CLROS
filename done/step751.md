# Step 751 - PAL guarded service-denial result adapters

## Summary
Added a small host-tested UEFI-gone diagnostic adapter slice that formats
guarded platform result structs through the existing
`UEFI_SERVICE_DENIED operation=<token> mode=<mode>` marker contract. The marker
formatter also gained a direct operation/mode overload so adapters do not need
to synthesize a new firmware-call result just to format diagnostics.

## Why
The existing denial marker accepted only a raw `FirmwareCallResult`. Most
guarded platform call sites retain typed result structs instead, so this adds a
narrow bridge without changing boot order, calling firmware, or exporting PAL
symbols.

## Files changed
- `src/OS.Kernel/Platform/PlatformFirmwareServiceDenialMarkerAdapters.cs`
- `src/OS.Kernel/Platform/PlatformFirmwareServiceDenialMarker.cs`
- `tests/OS.HostTests/PlatformFirmwareServiceDenialMarkerAdapterTests.cs`
- `docs/pal.md`
- `work/PAL/pal-design.md`
- `done/step751.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project artifacts\issue-pal-slice-smoke\issue-pal-slice-smoke.csproj`
- `dotnet .\artifacts\issue710-tcp-close-formatter\bin\Debug\net10.0\OS.HostTests.dll`
  completed in the combined parent verification pass after this slice landed.
- `git diff --check`
- `git diff --check -- docs\pal.md work\PAL\pal-design.md src\OS.Kernel\Platform\PlatformFirmwareServiceDenialMarker.cs`
- `rg -n "[ \t]$" src\OS.Kernel\Platform\PlatformFirmwareServiceDenialMarker.cs src\OS.Kernel\Platform\PlatformFirmwareServiceDenialMarkerAdapters.cs tests\OS.HostTests\PlatformFirmwareServiceDenialMarkerAdapterTests.cs docs\pal.md work\PAL\pal-design.md done\step751.md artifacts\issue-pal-slice-smoke\Program.cs`

## Evidence
- `artifacts/logs/issue-pal-slice-hosttests-build.log` shows the focused host
  test project build succeeded with 0 warnings and 0 errors.
- `artifacts/logs/issue-pal-slice-adapter-smoke.log` contains:
  - `TEST_PASS platform.firmware denial adapters format guarded results`
  - `TEST_PASS platform.firmware denial adapters query required bytes`
  - `TEST_PASS platform.firmware denial adapters reject non-denial results`
  - `TEST_PASS platform.firmware denial adapters result layout contains no managed references`
- `artifacts/logs/issue710-hosttests-20260525.log` contains the combined full
  host harness pass after this slice landed, including:
  - `TEST_PASS platform.firmware denial adapters format guarded results`
  - `TEST_PASS platform.firmware denial adapters query required bytes`
  - `TEST_PASS platform.firmware denial adapters reject non-denial results`
  - `TEST_PASS platform.firmware denial adapters result layout contains no managed references`
  - `TEST_PASS host.tests`
- `artifacts/logs/issue-pal-slice-hosttests.log` contains the earlier partial
  full host harness run with many `TEST_PASS` markers before interruption.
- `artifacts/logs/issue-pal-slice-git-diff-check-scoped.log` contains only
  Git LF-to-CRLF working-copy warnings for the scoped docs and exited 0.

## Failures
- The original worker's full `OS.HostTests` harness did not complete because it
  was stopped to avoid competing with another concurrent host-test run. The
  parent combined verification later completed the full harness with the PAL
  adapter markers present.
- Global `git diff --check` returned nonzero while reporting repository-wide
  LF-to-CRLF working-copy warnings from the already-dirty tree. The scoped docs
  check exited 0, and a direct trailing-whitespace scan of the new source/test
  files found no matches.

## Deferred work
- Run the full host harness once the concurrent worker finishes.
- Keep this diagnostic adapter host-only until a later slice wires additional
  UEFI-gone diagnostics into the live EFI path.

