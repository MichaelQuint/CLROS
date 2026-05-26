# Step 773 - Platform firmware denial marker shared ASCII builder

## Summary
Migrated `PlatformFirmwareServiceDenialMarker` to use the shared
`OS.Std.Text.AsciiTextBuilder` append path for the stable
`UEFI_SERVICE_DENIED operation=<token> mode=<mode>` marker. The existing marker
text, required-byte query behavior, short-destination behavior,
invalid-operation-token rejection, allowed-call rejection, default-result
rejection, mode validation, and POD result shape remain unchanged.

## Why
The firmware denial marker is part of the Phase E platform/UEFI encapsulation
diagnostic surface. Reusing the shared no-runtime ASCII text builder removes
formatter-local span-copy append paths before more platform diagnostics are
wired into UEFI-gone simulation and future post-firmware boundaries.

## Files changed
- `src/OS.Kernel/Platform/PlatformFirmwareServiceDenialMarker.cs`
- `tests/OS.HostTests/PlatformFirmwareServiceDenialMarkerTests.cs`
- `docs/pal.md`
- `done/step773.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue731-platform-firmware-denial-formatter\bin\Debug\net10.0 *>&1 | Tee-Object -FilePath .\artifacts\logs\issue731-host-build-20260525.log`
- `dotnet .\artifacts\issue731-platform-firmware-denial-formatter\bin\Debug\net10.0\OS.HostTests.dll *>&1 | Tee-Object -FilePath .\artifacts\logs\issue731-hosttests-20260525.log`
- `git diff --check *>&1 | Tee-Object -FilePath .\artifacts\logs\issue731-git-diff-check-20260525.log`
- `rg -n "[ \t]$" src/OS.Kernel/Platform/PlatformFirmwareServiceDenialMarker.cs tests/OS.HostTests/PlatformFirmwareServiceDenialMarkerTests.cs docs/pal.md done/step773.md`

## Evidence
- Build log: `artifacts/logs/issue731-host-build-20260525.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- Host test log: `artifacts/logs/issue731-hosttests-20260525.log`
  - `TEST_PASS platform.firmware denial marker formats uefi gone`
  - `TEST_PASS platform.firmware denial marker formats exited services`
  - `TEST_PASS platform.firmware denial marker reports short destination`
  - `TEST_PASS platform.firmware denial marker rejects allowed and default results`
  - `TEST_PASS platform.firmware denial marker rejects invalid operation token`
  - `TEST_PASS platform.firmware denial marker uses shared ascii text builder`
  - `TEST_PASS platform.firmware denial marker result contains no managed references`
  - `TEST_PASS host.tests`
- Source guard evidence:
  - `PlatformFirmwareServiceDenialMarker.cs` imports `OS.Std.Text`.
  - Marker bytes now flow through
    `AsciiTextBuilder.AppendBytes(destination, offset, source)`.
  - Operation tokens now flow through
    `AsciiTextBuilder.AppendChars(destination, offset, operation)`.
  - The source guard rejects formatter-local
    `CopyTo(destination...)`, `destination.Slice(offset)`, and the old
    `WriteOperation` append helper.
- Documentation evidence:
  - `docs/pal.md` records the shared `AsciiTextBuilder` append path and keeps
    the claim scoped to host-tested marker formatting only.
- Whitespace evidence:
  - `rg -n "[ \t]$" ...` found no trailing whitespace in the owned files.

## Failures
- The required `git diff --check` command wrote
  `artifacts/logs/issue731-git-diff-check-20260525.log` and the PowerShell
  pipeline returned nonzero because Git emitted broad working-copy
  LF-to-CRLF warnings on stderr for many pre-existing dirty files. The log shows
  line-ending warnings, not file:line whitespace-error records for this slice.
- No QEMU smoke was run for this slice because the change is a host-tested
  formatter migration only.

## Deferred work
- Keep using shared no-runtime text builders for future platform diagnostic
  formatters before adding local byte-copy or integer-formatting loops.
- Wire denial markers into any future live post-firmware diagnostic path only
  after real owned serial diagnostics and the `ExitBootServices` retry path have
  evidence.

## Evidence boundary
This step proves caller-owned host formatting of already-produced firmware
service-denial metadata through the shared no-runtime ASCII text builder. It
does not call real firmware, perform `ExitBootServices`, prove firmware
shutdown, prove post-firmware execution, add live platform drivers, or provide
cloud-provider evidence.

