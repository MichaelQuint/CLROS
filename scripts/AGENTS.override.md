# `scripts` Override

This directory contains PowerShell automation for build, publish, and smoke evidence. Root `AGENTS.md` applies, with these local rules.

## Purpose

Scripts are the stable command surface for developers, AI agents, and future CI.

Current scripts:

- `build-efi-shell.ps1`
- `check-naming.ps1`
- `run-baseline-evidence.ps1`
- `run-qemu-smoke.ps1`

## Script Rules

- Use PowerShell compatible with Windows PowerShell 5.1 unless a script explicitly documents a newer requirement.
- Set `$ErrorActionPreference = "Stop"` for scripts that orchestrate build/test steps.
- Emit stable `TEST_*` markers.
- Return nonzero exit codes on failure.
- Write logs under `artifacts/logs/`.
- Stage generated EFI/image artifacts under `artifacts/`.
- Do not print secrets.
- Avoid destructive filesystem operations.
- If cleanup is needed, verify paths are under the repository or an explicit artifact directory before deleting.
- Keep commands reproducible and copyable from docs.

## Evidence Rules

Scripts used for evidence should emit:

- `TEST_START <name>`
- `TEST_PASS <name>`
- `TEST_FAIL <name> <reason>`

Evidence scripts should record:

- Command run.
- Log path.
- Exit code.
- Start/end time.
- Summary JSON where useful.

Baseline and CI evidence planning should keep host-tool blockers separate from
OS failures. `run-baseline-evidence.ps1` and `run-ci-smoke.ps1` should record
`qemu-img` discovery and qcow2 conversion intent through the script-specific
`*_QEMU_IMG_DISCOVERY`/`*_QCOW2_CONVERSION_INTENT` markers plus
`qemuImgDiscovery` and `qcow2ConversionIntent` summary objects without dumping
PATH contents or secrets.

## EFI Publish Rules

`build-efi-shell.ps1` should:

- Preflight the MSVC x64 linker and Windows SDK UM x64 library before `dotnet publish`, emitting `EFI_PREFLIGHT_START`, `EFI_PREFLIGHT_MSVC_LINKER`, `EFI_PREFLIGHT_WINDOWS_SDK`, and `EFI_PREFLIGHT_OK`.
- Build `src/OS/OS.csproj`.
- Write a log under `artifacts/logs/`.
- Detect and report `platform_linker_missing` (MSVC x64 `link.exe`) and `windows_sdk_um_x64_missing` (Windows SDK `kernel32.lib`).
- Stage `artifacts/efi/BOOTX64.EFI` on success.
- Emit `EFI_ARTIFACT artifacts/efi/BOOTX64.EFI`.

Keep the preflight discovery injectable (`-MsvcLinkerSearchRoot`, `-WindowsSdkLibSearchRoot`, `-VsWherePath`, `-PreflightOnly`) so host tests can cover it with fakes. Do not make the EFI script hide compiler/linker failures. Surface the exact log path. Do not dump host environment data or secrets in the preflight output.

## QEMU Rules

`run-qemu-smoke.ps1` must preserve dry-run behavior. Dry-run must not require
QEMU, OVMF, or an existing image artifact.

Real QEMU mode must preflight host prerequisites before launching the runner:

- Resolve QEMU and OVMF and emit `TEST_START qemu.smoke.preflight`,
  `QEMU_SCRIPT_PREFLIGHT_QEMU`, `QEMU_SCRIPT_PREFLIGHT_OVMF`, and
  `TEST_PASS qemu.smoke.preflight`.
- Fail early with `TEST_FAIL qemu.smoke qemu_not_found` or
  `TEST_FAIL qemu.smoke ovmf_not_found` when host tools are missing.
- Validate the selected image path and disk format before runner launch with
  `QEMU_SCRIPT_PREFLIGHT_IMAGE` and `QEMU_SCRIPT_PREFLIGHT_DISK_FORMAT`.
- Keep failures concise and path-specific. Do not dump environment variables,
  PATH contents, or secrets.

Real QEMU mode must eventually:

- Capture serial output.
- Enforce timeout.
- Fail if `OS_SMOKE_PASS` is absent.
- Save logs under `artifacts/logs/`.

## Generated C Exception

Committed source must be C# only. A script may generate ephemeral C code only if a documented build/linker step requires it. Generated C/C++/header/assembly files must not be committed.
