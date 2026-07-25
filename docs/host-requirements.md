# Host Requirements

## Current Status

The repository currently builds on Windows with .NET 10. The normal solution build is managed-code host tooling, while the EFI shell publish also requires the Windows NativeAOT platform linker stack.

The EFI publish requirement is a host toolchain requirement. It is not caused by Hyper-V. Hyper-V can affect later QEMU acceleration choices, but `scripts/build-efi-shell.ps1` fails or passes before any VM boot path is involved.

## Implemented Behavior

Required for all current host builds:

- Windows 10 or Windows 11 x64.
- PowerShell 5.1 or newer.
- .NET SDK 10.0.300 or newer compatible .NET 10 SDK.
- NuGet access to restore .NET 10 packages.

Required for `scripts/build-efi-shell.ps1`:

- Visual Studio Build Tools 2022 or Visual Studio with C++ build tools.
- MSVC x64 toolchain, including `link.exe` and `cl.exe`.
- Windows SDK with x64 UM libraries, including `kernel32.lib`.
- NativeAOT runtime packs restored by `dotnet restore`.

Recommended Visual Studio components:

- `Microsoft.VisualStudio.Workload.VCTools`
- `Microsoft.VisualStudio.Component.VC.Tools.x86.x64`
- Windows 11 SDK `10.0.26100.0` or another SDK version compatible with the installed .NET SDK.

Required for real QEMU smoke:

- `qemu-system-x86_64`.
- OVMF firmware files for UEFI QEMU boot.

Optional for cloud artifact tooling:

- `qemu-img` for real qcow2 conversion through `tools/OS.ImageBuilder convert-qcow2`.
- libvirt on a Linux host for later libvirt smoke tests.

## Setup

Install .NET 10 SDK from the official .NET installer or existing Visual Studio layout.

Install Visual Studio Build Tools from winget:

```powershell
winget install --id Microsoft.VisualStudio.2022.BuildTools --exact --accept-package-agreements --accept-source-agreements --override "--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"
```

If the installer reports `VSProcessesRunning`, close Visual Studio, SQL Server Management Studio, and Visual Studio diagnostic tools, then retry. A pending reboot can also block Visual Studio installer changes.

Install QEMU from winget:

```powershell
winget install --id SoftwareFreedomConservancy.QEMU --silent --accept-package-agreements --accept-source-agreements
```

The current tested Windows package provides:

```text
C:\Program Files\qemu\qemu-system-x86_64.exe
C:\Program Files\qemu\share\edk2-x86_64-code.fd
```

## Verification

Verify the .NET SDK:

```powershell
dotnet --info
dotnet build .\OS.slnx --nologo -m:1
```

Verify Visual Studio Build Tools are installed:

```powershell
$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
& $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
```

Verify MSVC and Windows SDK files exist. `link.exe` does not need to be on the normal PowerShell `PATH`; the .NET NativeAOT targets can discover it from the Visual Studio installation.

```powershell
Get-ChildItem "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools" -Recurse -Filter link.exe |
    Where-Object { $_.FullName -match "Hostx64\\x64" } |
    Select-Object -First 1 FullName

Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\Lib" -Recurse -Filter kernel32.lib |
    Where-Object { $_.FullName -match "\\um\\x64\\" } |
    Select-Object -First 1 FullName
```

Verify the EFI publish:

```powershell
.\scripts\build-efi-shell.ps1
```

Expected current success markers:

```text
TEST_PASS efi.shell.publish
EFI_ARTIFACT artifacts/efi/BOOTX64.EFI
```

`scripts/build-efi-shell.ps1` now runs a host-toolchain preflight before
`dotnet publish`. The preflight resolves the MSVC x64 `link.exe`
(`Hostx64\x64`) and the Windows SDK UM x64 `kernel32.lib` (`um\x64`) using
`vswhere` plus well-known install roots, and it emits:

```text
EFI_PREFLIGHT_START
EFI_PREFLIGHT_MSVC_LINKER <path>
EFI_PREFLIGHT_WINDOWS_SDK <path>
EFI_PREFLIGHT_OK
```

If a prerequisite is missing, the script fails before `dotnet publish` with a
deterministic reason and the log path, without dumping host environment data:

```text
TEST_FAIL efi.shell.publish platform_linker_missing log=artifacts/logs/efi-shell-publish.log
TEST_FAIL efi.shell.publish windows_sdk_um_x64_missing log=artifacts/logs/efi-shell-publish.log
```

This preflight is host-toolchain evidence only. `EFI_PREFLIGHT_OK` proves that
the linker and SDK library files are discoverable on this host. It does not
prove a successful publish, a staged `artifacts/efi/BOOTX64.EFI`, QEMU boot,
`ExitBootServices`, or any runtime behavior. The search roots are injectable
through `-MsvcLinkerSearchRoot`, `-WindowsSdkLibSearchRoot`, and `-VsWherePath`,
and `-PreflightOnly` runs the checks without publishing so host tests can cover
the contract with fakes instead of uninstalling toolchains.

Verify the real QEMU smoke:

```powershell
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15
```

Expected current success markers:

```text
TEST_PASS imagebuilder.raw
TEST_PASS qemu.smoke.preflight
TEST_PASS qemu.smoke
```

Real smoke mode now runs a wrapper preflight before launching `OS.QemuRunner`.
Dry-run mode does not require QEMU, OVMF, or an existing image. Missing
real-smoke prerequisites fail with concise machine-readable reasons:

```text
TEST_FAIL qemu.smoke qemu_not_found
TEST_FAIL qemu.smoke ovmf_not_found
TEST_FAIL qemu.smoke image_not_found path=<image>
TEST_FAIL qemu.smoke image_empty path=<image>
TEST_FAIL qemu.smoke disk_format_mismatch expected=<raw|qcow2> detected=<format>
```

The preflight also emits `QEMU_SCRIPT_PREFLIGHT_QEMU`,
`QEMU_SCRIPT_PREFLIGHT_OVMF`, `QEMU_SCRIPT_PREFLIGHT_IMAGE`, and
`QEMU_SCRIPT_PREFLIGHT_DISK_FORMAT` lines so missing host tools can be
distinguished from OS/image failures. This is prerequisite evidence only; boot
proof still requires a real QEMU run with captured serial markers.

Verify baseline `qemu-img` discovery diagnostics without requiring conversion:

```powershell
.\scripts\run-baseline-evidence.ps1 -PlanOnly
```

Expected host-prerequisite markers:

```text
TEST_START baseline.qemu_img.discovery
BASELINE_QEMU_IMG_DISCOVERY requested=<auto|path> source=<configured|environment|PATH|well-known|auto> available=<true|false> resolved=<path|not_found>
BASELINE_QCOW2_CONVERSION_INTENT mode=<dry-run|real> status=<planned|blocked_by_missing_qemu_img> qemuImgRequired=<true|false> qemuImgAvailable=<true|false>
TEST_PASS baseline.qemu_img.discovery
```

In default plan-only mode, missing `qemu-img` does not fail the baseline plan
or unrelated raw QEMU/libvirt intent lanes because qcow2 conversion is only
planned as dry-run intent. With `-RunQcow2RealSmoke`, the summary records
`blocked_by_missing_qemu_img` when the configured or discovered tool is absent.
That blocker is a host prerequisite boundary, not an OS image-builder failure.

Verify CI smoke `qemu-img` discovery diagnostics without requiring conversion:

```powershell
.\scripts\run-ci-smoke.ps1 -DryRun
```

Expected host-prerequisite markers:

```text
TEST_START ci.smoke.qemu_img.discovery
CI_SMOKE_QEMU_IMG_DISCOVERY requested=<auto|path> source=<configured|environment|PATH|well-known|auto> available=<true|false> resolved=<path|not_found>
CI_SMOKE_QCOW2_CONVERSION_INTENT mode=<dry-run|real> status=<planned|blocked_by_missing_qemu_img> qemuImgRequired=<true|false> qemuImgAvailable=<true|false>
TEST_PASS ci.smoke.qemu_img.discovery
```

`ci-smoke.artifacts.json` and `ci-smoke.summary.json` record matching
`qemuImgDiscovery` and `qcow2ConversionIntent` objects at the top level and on
the qcow2 smoke step.

## Windows CI Agent Bring-Up

Use this section as the canonical host-prerequisite checklist for a Windows CI
agent. It documents the agent shape and validation commands only; it does not
create a repository CI workflow and does not turn dry-run evidence into boot
proof.

Required agent capabilities:

- Windows 10 or Windows 11 x64 with PowerShell 5.1 or newer.
- .NET SDK 10.0.300 or newer compatible .NET 10 SDK.
- Visual Studio Build Tools 2022 with `Microsoft.VisualStudio.Workload.VCTools`
  and `Microsoft.VisualStudio.Component.VC.Tools.x86.x64`.
- Windows 10/11 SDK UM x64 libraries, including `kernel32.lib`.
- QEMU for dry-run command discovery and optional real smoke:
  `qemu-system-x86_64.exe`, `qemu-img.exe`, and an OVMF code image such as
  `edk2-x86_64-code.fd`.

Prefer explicit script parameters on CI agents so logs show the intended tool
inputs without dumping PATH contents:

```powershell
$qemu = "C:\Program Files\qemu\qemu-system-x86_64.exe"
$qemuImg = "C:\Program Files\qemu\qemu-img.exe"
$ovmf = "C:\Program Files\qemu\share\edk2-x86_64-code.fd"
```

When explicit parameters are not practical, current discovery also supports
`QEMU_IMG` for `qemu-img`, `OS_OVMF_CODE`/`OVMF_CODE`/`QEMU_OVMF_CODE` for
OVMF, PATH lookup for QEMU tools, and the well-known QEMU install paths listed
above. Do not print environment dumps, PATH contents, package feed tokens, or
other secrets in CI logs.

Minimum non-boot CI agent validation:

```powershell
dotnet --info
dotnet build .\OS.slnx --nologo -m:1
.\scripts\build-efi-shell.ps1 -PreflightOnly
.\scripts\run-qemu-smoke.ps1 -DryRun
.\scripts\run-ci-smoke.ps1 -DryRun
.\scripts\run-baseline-evidence.ps1 -PlanOnly
git diff --check
```

The first repository workflow for this lane is
`.gitea/workflows/ci-smoke.yml`. It targets a Windows runner labelled
`windows` and runs the non-boot validation commands above, then attempts to
upload `artifacts/logs/**` when the runner supports `actions/upload-artifact`.
Adjust only the runner label or artifact action source for local Gitea runner
policy; keep the default lane dry-run/preflight/plan-only until a separate
issue captures real QEMU/qcow2 smoke evidence. Host tests guard this workflow
contract so the default lane keeps the expected triggers, PowerShell shell,
timeout, upload-on-failure behavior, commands, and absence of real-smoke flags.
`scripts/run-ci-smoke.ps1` also plans and captures `git diff --check` as
`ci.smoke.git.diffcheck`, so whitespace evidence is present in the CI smoke
artifact plan and summary.

Expected marker families for CI bring-up:

```text
EFI_PREFLIGHT_START
EFI_PREFLIGHT_OK
TEST_PASS qemu.runner.dryrun
TEST_START ci.smoke.qemu_img.discovery
TEST_PASS ci.smoke.qemu_img.discovery
TEST_PASS ci.smoke.dryrun
TEST_PASS ci.smoke.dryrun includes git diff check
TEST_START baseline.qemu_img.discovery
TEST_PASS baseline.qemu_img.discovery
TEST_PASS baseline.artifact_summary
```

Optional real smoke commands for a configured agent:

```powershell
.\scripts\run-qemu-smoke.ps1 -Qemu $qemu -OvmfCode $ovmf -TimeoutSeconds 15
.\scripts\run-ci-smoke.ps1 -RunQcow2RealSmoke -Qemu $qemu -QemuImg $qemuImg -OvmfCode $ovmf
```

Real smoke requires the same serial marker evidence as local runs. CI dry-run,
preflight-only, and plan-only logs are useful prerequisite evidence, but they do
not prove QEMU boot, qcow2 conversion, libvirt real boot, cloud import, hosted
.NET, Minimal API, or Kestrel.

## Unsupported Behavior

The host requirements above only prove build and publish prerequisites. They do not prove QEMU boot, ExitBootServices, drivers, storage, networking, hosted .NET 10 applications, ASP.NET Minimal API hosting, or Kestrel.

Linux/macOS hosts are not documented for the EFI publish path yet. A future cross-build path can be added after the Windows NativeAOT/EFI path is stable.

## Tests

Current host validation commands:

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun
.\scripts\build-efi-shell.ps1
```

Current evidence from 2026-05-21:

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS host.tests`.
- Naming guard emitted `TEST_PASS naming.guard`.
- QEMU dry-run emitted `TEST_PASS qemu.runner.dryrun`.
- EFI publish emitted `TEST_PASS efi.shell.publish` and staged `artifacts/efi/BOOTX64.EFI`.
- QEMU real smoke emitted `TEST_PASS qemu.smoke` with QEMU 11.0.0 and OVMF from `C:\Program Files\qemu`.

## Known Limitations

- `artifacts/efi/BOOTX64.EFI` plus `artifacts/images/os.raw` now prove only the EFI smoke path, ordered boot markers, and UEFI memory-map readiness marker, not a functional kernel.
- QEMU and OVMF are required only for real smoke runs; dry-run and host unit tests still work without them.
- Hyper-V is not required for build or publish. Later QEMU acceleration can use TCG, WHPX, KVM, or another accelerator depending on host support.
- The current EFI shell uses placeholder no-stdlib runtime helpers that are not a complete runtime contract.

## Next Steps

1. `scripts/build-efi-shell.ps1` now preflights the MSVC x64 linker and Windows
   SDK UM x64 library before `dotnet publish`. Extend the preflight to report
   the resolved MSVC and SDK versions if a future step needs version pinning.
2. Add repository CI workflow files that run the documented validation commands
   on a compatible Windows agent and upload logs/artifacts.
