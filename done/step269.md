# Step 269 - Guarded Platform Console Contract

## What Changed

- Added `IPlatformConsole` as the host-testable platform contract for guarded firmware-console marker-line output.
- Added `PlatformConsoleResult` and `PlatformConsoleStatus` with deterministic statuses for success, unavailable firmware services, invalid marker input, and missing output sink.
- Added host tests that exercise firmware-console writes before UEFI-gone, after UEFI-gone simulation, after `ExitBootServices`, invalid marker input, and unavailable sink behavior.
- Updated PAL, EFI build, and completion-gate docs to record the host-tested boundary without claiming live `EfiEntry` integration.

## Why It Changed

Issue #269 needed the console side of the Phase E platform-interface guard pattern before more UEFI calls are moved behind platform abstractions. This keeps firmware-backed output denyable through `IPlatformFirmware` and `FirmwareServicesGuard` while preserving the current live EFI behavior.

## Files Changed

- `src/OS.Kernel/Platform/IPlatformConsole.cs`
- `tests/OS.HostTests/PlatformConsoleGuardTests.cs`
- `docs/pal.md`
- `docs/efi-build.md`
- `docs/completion-gates.md`
- `done/step269.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
dotnet build .\OS.slnx --nologo -m:1
.\scripts\check-naming.ps1
git diff --check
```

## Logs And Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS host.tests`.
- Host tests emitted `TEST_PASS platform.console allows firmware console before UEFI gone`.
- Host tests emitted `TEST_PASS platform.console rejects firmware console in UEFI-gone simulation`.
- Host tests emitted `TEST_PASS platform.console rejects firmware console after ExitBootServices`.
- Host tests emitted `TEST_PASS platform.console rejects invalid marker input`.
- Host tests emitted `TEST_PASS platform.console reports sink unavailable`.
- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited successfully; Git printed LF-to-CRLF working-copy warnings for existing dirty files but reported no whitespace errors.

## Failures

None.

## Evidence Boundary

This is a host-tested platform contract only:

- no change to `src/OS/Boot/EfiEntry.cs`;
- no live UEFI `OutputString` implementation behind `IPlatformConsole`;
- no serial fallback integration behind `IPlatformConsole`;
- no owned post-firmware serial output;
- no real `ExitBootServices` evidence;
- no UEFI encapsulation gate completion claim.

## Deferred Work

- Wire a real UEFI console implementation behind `IPlatformConsole` when `EfiEntry` platform encapsulation begins.
- Replace firmware-console output with owned serial/framebuffer diagnostics before any real post-`ExitBootServices` path depends on output.

