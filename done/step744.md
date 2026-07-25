# Step 744 - PAL UEFI Service Denial Marker

## Summary

Added a host-tested platform diagnostic contract for denied firmware-service
calls after the UEFI-gone boundary.

## Why

`FirmwareServicesGuard` already denied firmware-backed calls after simulated or
real boot-services shutdown modes, but the denial value did not have a compact
caller-buffer marker formatter. This adds a small service-denial evidence slice
for issue #4 without touching boot ordering or live firmware calls.

## Files changed

- `src/OS.Kernel/Platform/PlatformFirmwareServiceDenialMarker.cs`
  - Added `UEFI_SERVICE_DENIED operation=<token> mode=<mode>` formatting for
    denied `FirmwareCallResult` values.
  - Added status/result structs for invalid/default results, allowed calls,
    invalid operation tokens, short destinations, and successful formatting.
- `tests/OS.HostTests/PlatformFirmwareServiceDenialMarkerTests.cs`
  - Added host tests for simulated UEFI-gone denials, `BootServicesExited`
    denials, destination sizing, allowed/default result rejection, invalid
    operation tokens, and POD-only result metadata.
- `docs/pal.md`
  - Documented the service-denial marker contract and evidence boundary.
- `done/step744.md`
  - Recorded this implementation slice and evidence.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- `artifacts/logs/issue4-pal-uefi-host-build-20260525-121739.log`
  - Host-test project build succeeded with 0 warnings and 0 errors.
- `artifacts/logs/issue4-pal-uefi-hosttests-20260525-121750.log`
  - Full host harness ended with `TEST_PASS host.tests`.
  - Focused service-denial marker tests passed.
- `artifacts/logs/issue4-pal-uefi-git-diff-check-final.log`
  - `git diff --check` exited 0.
  - Output contains existing LF/CRLF working-copy warnings only.

Expected focused markers:

- `TEST_PASS platform.firmware denial marker formats uefi gone`
- `TEST_PASS platform.firmware denial marker formats exited services`
- `TEST_PASS platform.firmware denial marker reports short destination`
- `TEST_PASS platform.firmware denial marker rejects allowed and default results`
- `TEST_PASS platform.firmware denial marker rejects invalid operation token`
- `TEST_PASS platform.firmware denial marker result contains no managed references`

## Failures

No final verification failures.

## Deferred work

- Feed this marker into a future boot or probe path only after a specific
  firmware-service denial needs serial evidence.
- Keep real `ExitBootServices` blocked until owned diagnostics and post-firmware
  platform rules are proven.

## Evidence boundary

Host-tested PAL/platform diagnostic evidence only. This does not call real
`ExitBootServices`, change `BootSequence`, export unmanaged PAL symbols, touch
networking/storage/std/QEMU tooling, or prove live post-firmware execution,
hosted .NET, Kestrel, cloud readiness, or production readiness.
