# Step 660 - Explicit EFI publish host-prerequisite preflight (#578)

## Summary

Added a host-toolchain preflight to `scripts/build-efi-shell.ps1` so the EFI
publish fails earlier and more clearly when the Windows NativeAOT linker
prerequisites are missing. Before `dotnet publish`, the script resolves the
MSVC x64 `link.exe` (`Hostx64\x64`) and the Windows SDK UM x64 `kernel32.lib`
(`um\x64`) and emits machine-readable preflight markers. Missing prerequisites
short-circuit with a deterministic `TEST_FAIL efi.shell.publish` reason instead
of waiting for the linker step inside `dotnet publish` to fail.

## Why

Issue #578: on hosts without the Visual Studio C++ build tools or the Windows
SDK x64 UM libraries, the publish previously only failed deep inside
`dotnet publish` (or via post-publish log scraping for `platform_linker_missing`).
That is slow and easy to misread as a kernel/Hyper-V problem. An explicit
preflight surfaces the exact missing prerequisite up front, keeps the failure
reason deterministic, and keeps successful publishes unchanged.

## Files changed

- `scripts/build-efi-shell.ps1`
  - Added `-VsWherePath`, `-MsvcLinkerSearchRoot`, `-WindowsSdkLibSearchRoot`,
    and `-PreflightOnly` parameters.
  - Added a preflight that resolves the MSVC x64 linker and Windows SDK UM x64
    library before `dotnet publish` and emits `EFI_PREFLIGHT_START`,
    `EFI_PREFLIGHT_MSVC_LINKER <path>`, `EFI_PREFLIGHT_WINDOWS_SDK <path>`, and
    `EFI_PREFLIGHT_OK`.
  - Emits `TEST_FAIL efi.shell.publish platform_linker_missing` or
    `TEST_FAIL efi.shell.publish windows_sdk_um_x64_missing` (with the log path)
    when a prerequisite is missing, before running `dotnet publish`.
  - Seeds the publish log with the preflight lines and appends `dotnet`
    output; preserves the post-publish `platform_linker_missing` backstop and
    the existing `TEST_PASS efi.shell.publish` / `EFI_ARTIFACT` success path.
  - Accepts an absolute `-LogPath` so tests can write to a temp log.
- `tests/OS.HostTests/BuildEfiShellPreflightTests.cs`
  - New host test that invokes the script with injected fake search roots and
    `-PreflightOnly` to cover missing-linker, missing-SDK, and
    discoverable-toolchain contracts. Mirrors the existing
    `DoneStepAllocatorScriptTests` script-invocation pattern.
- `docs/host-requirements.md`
  - Documented the preflight markers, deterministic failure reasons, injectable
    search roots, and the host-toolchain-only evidence boundary; updated the
    next-steps entry that previously proposed this work.
- `docs/efi-build.md`
  - Documented the preflight behavior, the `windows_sdk_um_x64_missing` reason,
    the backstop, and the host-toolchain-only evidence boundary plus the new
    host test.
- `scripts/AGENTS.override.md`
  - Updated the EFI Publish Rules to include the preflight markers, the second
    failure reason, the injectable parameters, and the no-host-dump rule.
- `done/step660.md`
  - This record.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\run-qemu-smoke.ps1 -DryRun`
- `git diff --check`

## Evidence

- Focused host build passed with 0 warnings and 0 errors.
- Host tests passed and emitted:
  - `TEST_PASS efi preflight reports missing msvc linker`
  - `TEST_PASS efi preflight reports missing windows sdk`
  - `TEST_PASS efi preflight passes with discoverable toolchain`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- QEMU smoke dry-run passed with `TEST_PASS qemu.runner.dryrun`.
- `git diff --check` reported no whitespace errors; Git only printed existing
  LF-to-CRLF normalization warnings for edited files.
- New EFI preflight markers:
  `EFI_PREFLIGHT_START`, `EFI_PREFLIGHT_MSVC_LINKER <path>`,
  `EFI_PREFLIGHT_WINDOWS_SDK <path>`, `EFI_PREFLIGHT_OK`.
- Deterministic failure reasons preserved/extended:
  `TEST_FAIL efi.shell.publish platform_linker_missing log=<path>` and
  `TEST_FAIL efi.shell.publish windows_sdk_um_x64_missing log=<path>`.
- Success path unchanged: `TEST_PASS efi.shell.publish` and
  `EFI_ARTIFACT artifacts/efi/BOOTX64.EFI`.

## Failures

- No code failures remain. During the later QEMU preflight slice, one
  development host-test run exposed a PowerShell null-to-empty-string fallthrough
  in the QEMU wrapper preflight; that script bug was fixed before the passing
  verification run and did not affect the EFI preflight contract.

## Deferred work

- Optionally extend the preflight to report resolved MSVC and SDK versions if a
  future step needs version pinning.
- Run `.\scripts\build-efi-shell.ps1` on a Windows host with the full Visual
  Studio C++/Windows SDK toolchain to capture live `EFI_PREFLIGHT_OK`,
  `TEST_PASS efi.shell.publish`, and `EFI_ARTIFACT artifacts/efi/BOOTX64.EFI`
  evidence.

## Evidence boundary

This slice adds host-toolchain preflight diagnostics only. `EFI_PREFLIGHT_OK`
proves the MSVC linker and Windows SDK library files are discoverable on the
host. It does not prove a successful publish, a staged EFI artifact, QEMU boot,
`ExitBootServices`, live storage, live networking, hosted .NET, or Kestrel.
