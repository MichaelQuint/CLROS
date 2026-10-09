# Host Requirements

## Current Status

The repository currently builds on Windows with .NET 10. The normal solution build is managed-code host tooling, while the EFI shell publish also requires the Windows NativeAOT platform linker stack.

The EFI publish requirement is a host toolchain requirement. It is not caused by Hyper-V. Hyper-V can affect later QEMU acceleration choices, but `scripts/build-efi-shell.ps1` fails or passes before any VM boot path is involved.

Open `CLROS.slnx` in Visual Studio 2026. The `OS` project retains its explicit
no-stdlib NativeAOT configuration. Its imported serializer targets use path
normalization supported by both Visual Studio's full-framework MSBuild and
`dotnet` MSBuild. If an older checkout reports `MSB4186` for
`Path.TrimEndingDirectorySeparator`, update the checkout and reload the project.
Visual Studio 2026 Insiders 18.11.12224.323 project evaluation was checked using
its MSBuild with `DesignTimeBuild=true` and `BuildingInsideVisualStudio=true`;
the user also confirmed that all solution projects load after this fix.
Publish EFI images through
`scripts/build-efi-shell.ps1`, which also verifies the artifact.

## Implemented Behavior

Required for all current host builds:

- Windows 10 or Windows 11 x64.
- PowerShell 5.1 or newer.
- A stable .NET SDK in the `10.0.400` feature band. `global.json` requests
  `10.0.401`, permits latest-patch roll-forward within that band, and rejects
  prerelease SDKs. The new Windows host selects `10.0.401`.
- NuGet access to restore .NET 10 packages.

Required for `scripts/build-efi-shell.ps1`:

- Visual Studio Build Tools or Visual Studio with C++ build tools, including
  Visual Studio 2026 Insiders. Discovery includes prerelease installations.
- MSVC x64 toolchain, including `link.exe` and `cl.exe`.
- Windows SDK with x64 UM libraries, including `kernel32.lib`.
- NativeAOT runtime packs restored by `dotnet restore`.

Recommended Visual Studio components:

- `Microsoft.VisualStudio.Workload.VCTools`
- `Microsoft.VisualStudio.Component.VC.Tools.x86.x64`
- Windows 11 SDK `10.0.26100.0` or another SDK version compatible with the installed .NET SDK.

Required for the complete `OS.HostTests` native-inspection fixtures:

- An x64 `llvm-objdump.exe`, supplied by Visual Studio's **C++ Clang Compiler
  for Windows** individual component (`Microsoft.VisualStudio.Component.VC.Llvm.Clang`)
  or a verified user-local LLVM tool cache. The object, array and
  String fixtures use it to inspect actual native
  instructions and relocations. MSVC x64 tools alone do not install this
  optional component. A successful managed build or EFI publish does not
  establish that the disassembler is available.
- Add the component through Visual Studio Installer → Modify → Individual
  components. See Microsoft's [component inventory](https://learn.microsoft.com/en-us/visualstudio/install/workload-component-id-vs-community?view=visualstudio).

For a user-local copy, retain the publisher/package checksum and tool identity,
then set `CLROS_LLVM_OBJDUMP` to its absolute executable path. An explicit missing,
relative or differently named path is rejected rather than silently falling back.
Without an override, the existing Visual Studio x64 discovery is used.
The native fixtures still record the selected executable and SHA-256.

Check discovery before a full host run:

```powershell
$clrosLlvmObjdump = $env:CLROS_LLVM_OBJDUMP
if ([string]::IsNullOrEmpty($clrosLlvmObjdump)) {
    $clrosLlvmObjdump = (Get-ChildItem "$env:ProgramFiles\Microsoft Visual Studio" -Filter llvm-objdump.exe -File -Recurse |
        Where-Object FullName -Match '\\x64\\' | Sort-Object FullName | Select-Object -First 1).FullName
}
if ([string]::IsNullOrEmpty($clrosLlvmObjdump) -or !(Test-Path -LiteralPath $clrosLlvmObjdump -PathType Leaf)) {
    throw 'Install the C++ Clang Compiler component or configure CLROS_LLVM_OBJDUMP before full HostTests.'
}
& $clrosLlvmObjdump --version
```

To persist a verified user-local selection for new processes, set both the
current process and user environment; already running apps need the process
setting or a restart to inherit it:

```powershell
$env:CLROS_LLVM_OBJDUMP = 'C:\Tools\LLVM\bin\llvm-objdump.exe' # replace with the verified actual path
[Environment]::SetEnvironmentVariable('CLROS_LLVM_OBJDUMP', $env:CLROS_LLVM_OBJDUMP, 'User')
```

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

### Invoke Windows PowerShell from a Core host

When an orchestration host uses PowerShell Core, invoke receipt-bound EFI/QEMU
commands through `powershell.exe`. On the new destination, one nested publish
failed to resolve `Get-FileHash` with the inherited module search path; a retry
using the Desktop module paths passed that restore stage. Use this process-local
setup for that invocation (it does not change the machine environment):

```powershell
$env:PSModulePath = 'C:\Windows\system32\WindowsPowerShell\v1.0\Modules;C:\Program Files\WindowsPowerShell\Modules'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./scripts/build-efi-shell.ps1
```

Retain the failed command when diagnosing a launch problem. A standalone
`Get-Command Get-FileHash` success does not establish that a nested publish has
the same effective module context. See [step1077](../done/step1077.md).
### Restore the pinned EFI runtime pack

The EFI project selects NativeAOT runtime pack `10.0.12` through
`ClrosRuntimePackagePath`. Its ILCompiler metapackage and win-x64 host compiler
are also pinned to `10.0.12`; the current SDK is `10.0.401`.
The EFI artifact verifier needs the actual selected `10.0.12` package tree;
an ordinary managed build alone does not establish that prerequisite.

If the expected directory below is missing, restore the exact package through
NuGet (do not rename another version's directory):

```powershell
Test-Path "$env:USERPROFILE\.nuget\packages\microsoft.netcore.app.runtime.nativeaot.win-x64\10.0.12"
New-Item -ItemType Directory -Force artifacts/pinned-efi-runtime | Out-Null
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup>
    <PackageDownload Include="Microsoft.NETCore.App.Runtime.NativeAOT.win-x64" Version="[10.0.12]" />
  </ItemGroup>
</Project>
'@ | Set-Content artifacts/pinned-efi-runtime/Restore.csproj
dotnet restore artifacts/pinned-efi-runtime/Restore.csproj --source https://api.nuget.org/v3/index.json --nologo
```

This restores a host prerequisite without changing the project's toolchain
selection or weakening artifact checks. On the earlier `10.0.8` baseline,
the 2026-10-01 full host suite reported 19 EFI verifier failures because that
version's directory was absent. Restoring
it made the existing linked-artifact acceptance test pass independently; the
subsequent full suite passed with 3,509 pass markers and zero failures (see
`done/step925.md`). Package presence and host checks are not EFI boot evidence.

The new `10.0.12` compiler qualification is recorded in
[step1078](../done/step1078.md); the preceding SDK401/ILC108 development
checkpoint is [step1077](../done/step1077.md). These are freestanding build
inputs, not a hosted .NET runtime running inside CLROS.

## Host suite execution and resume

Prefer the incremental-build wrapper during implementation:

```powershell
.\scripts\run-host-tests.ps1 -Contracts
.\scripts\run-host-tests.ps1 -List
.\scripts\run-host-tests.ps1 -Fixture BootInfoTests.RunAll
.\scripts\run-host-tests.ps1 -Results artifacts/host-suite/current
.\scripts\run-host-tests.ps1 -Resume artifacts/host-suite/current
```

Copy exact fixture IDs from `-List`. `-Resume` also accepts the printed
`result.json` path. Keep the same fixture selection when resuming a partial run;
omitting `-Fixture` selects the full current catalog. Unknown selections fail.

The harness explicitly discovers its attributed fixtures. A project-local alias
preserves existing `[ModuleInitializer]` source annotations for independent
capture projects, while preventing initialization-time execution in the host
harness. Each selected fixture runs serially in a separate process. A thrown
exception, native crash, or `TEST_FAIL` output fails that fixture and the runner
continues with the remaining fixtures. Existing group markers remain unchanged.
The two original boot smoke checks run through `BootSmokeTests.RunAll`.

Before expensive fixtures, the runner executes all 273 catalogued source
contracts (or those belonging to selected fixtures), collecting every failure.
`-Contracts` stops there and never claims full-suite success. The catalog in
`tests/OS.HostTests/HostSourceChecks.txt` contains reviewed lightweight methods;
do not add build, subprocess, native-execution or network methods. A compiled
input manifest rejects stale binaries after source edits, including `--no-build`.

Each run writes atomic per-fixture state, individual logs and `result.json` under
the printed results directory. Resume reruns failed, interrupted, new or changed
fixtures. It reuses passes only when their fingerprints and retained evidence
match. Fingerprints cover shared source/scripts/docs/configuration, host producer
binaries, runtime/SDK, environment (digest only), native compiler/linker/LLVM and
NativeAOT package bytes, plus the fixture source and transitive lexical references
to other fixture/helper source. Broad source scans conservatively include all
test files. The rebuilt host assembly itself is checked against its build
manifest but excluded from individual resume keys, so an unrelated fixture edit
does not automatically discard every pass.

Fixture logs and generated retained artifacts are hash-checked on reuse. Existing
caller console logs are excluded from artifact capture. Reused results emit
`HOST_FIXTURE_REUSED` with their originating run; they never replay `TEST_PASS`.
`HOST_RUN_SUMMARY` and the receipt distinguish executed and reused coverage.
`TEST_PASS host.tests` means every currently selected full-suite fixture passed
or has valid reused evidence. Partial selection emits `host.tests.selected`.
Old marker-count-only acceptance scripts must inspect the receipt for reused
coverage, or use a new results directory for wholly fresh execution.

Resume is local to the same workspace and machine. Do not run two suites against
shared outputs, change inputs while running, or move/edit checkpoint files. A
results-directory lock prevents duplicate owners; a surviving fixture process
blocks resumption after a hard parent interruption. Ctrl+C terminates the active
fixture process tree and leaves the remaining work pending. Corrupt receipts are
rejected. Unclassified environment-selected inputs and external reference checkouts disable
reuse conservatively. Other dynamic external inputs are outside this local
fingerprint model: fixtures consuming them must run fresh, and reuse is not guest/native acceptance
by itself. The audit found cross-fixture source dependencies but did not prove
complete artifact independence for every existing integration fixture.

Byte-exact native provenance remains strict. Three new cheap source-authority
guards catch stale frozen native inputs before publication. LF/CRLF text-only
contracts normalize line endings; artifact/source-closure provenance does not.
Git attributes preserve the accepted CRLF bytes for the project and two EFI
authority scripts on fresh checkouts. A checkout whose raw bytes still differ
from accepted frozen inputs fails that guard early. Do not refresh provenance
pins merely to make it pass.

## Focused host fixture runs

During implementation, run selected existing fixture files with PowerShell:

```powershell
.\scripts\run-host-tests-focused.ps1 -TestFile DiagnosticsTests.cs
.\scripts\run-host-tests-focused.ps1 -TestFile DiagnosticsTests.cs,BootInfoTests.cs
```

Use basenames from `tests/OS.HostTests`; add any required helper or companion
fixture files to the explicit list. `HostToolProcess.cs` is included automatically.
The script builds a unique project under `artifacts/host-tests-focused`, retaining
the original project references and settings. It compiles the selected original
files directly, dispatching their attributed fixtures without rewriting tests.
`Program.cs` and paths outside the fixture directory are rejected.

Each run retains the selected paths/hashes, native commands and exits, build/run
logs and `result.json`. Success requires actual fixture markers, no failure
markers, unchanged selected sources and `TEST_PASS host.tests.focused`.
Fixtures without fixture attributes do not run automatically; selecting only
helpers fails the marker check. Selected fixtures can still publish EFI images
or invoke external tools, so choose the smallest relevant set and serialize
fixtures that share outputs. `-Configuration Release` selects a release build.

Focused success is partial host evidence. It does not emit `TEST_PASS host.tests`
or replace a required complete suite or guest capture. The existing
`dotnet run --project tests/OS.HostTests/OS.HostTests.csproj --no-build` command
still runs the complete harness after a coherent implementation batch; the
wrapper above builds changed assertions first and also exposes resume.

## Verification

Verify the .NET SDK:

```powershell
dotnet --info
dotnet build .\CLROS.slnx --nologo -m:1
```

Verify Visual Studio Build Tools are installed:

```powershell
$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
$vsInstall = & $vswhere -latest -prerelease -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$vsInstall
```

Verify MSVC and Windows SDK files exist. `link.exe` does not need to be on the normal PowerShell `PATH`; the .NET NativeAOT targets can discover it from the Visual Studio installation.

```powershell
Get-ChildItem (Join-Path $vsInstall "VC\Tools\MSVC") -Recurse -Filter link.exe |
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
- The stable .NET SDK 10.0.400 feature band, minimum `10.0.401`, with patch selection governed by `global.json`.
- Visual Studio or Build Tools with `Microsoft.VisualStudio.Workload.VCTools`
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
dotnet build .\CLROS.slnx --nologo -m:1
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
dotnet build .\CLROS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun
.\scripts\build-efi-shell.ps1
```

Historical baseline evidence from 2026-05-21:

- `dotnet build .\CLROS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
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

1. Keep `scripts/build-efi-shell.ps1` tool discovery and reported MSVC/Windows
   SDK versions bound to the actual selected tools when the host changes.
2. Validate the existing `.gitea/workflows/ci-smoke.yml` on a compatible Windows
   runner and retain its logs/artifacts. Workflow presence alone is not CI
   execution evidence.
