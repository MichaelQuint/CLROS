# Host Requirements

## Current Status

The repository currently builds on Windows with .NET 10. The normal solution build is managed-code host tooling, while the EFI shell publish also requires the Windows NativeAOT platform linker stack.

The EFI publish requirement is a host toolchain requirement. It is not caused by Hyper-V. Hyper-V can affect later QEMU acceleration choices, but `OS.Tooling efi build` fails or passes before any VM boot path is involved.

Open `CLROS.slnx` in Visual Studio 2026. The `OS` project retains its explicit
no-stdlib NativeAOT configuration. Its imported serializer targets use path
normalization supported by both Visual Studio's full-framework MSBuild and
`dotnet` MSBuild. If an older checkout reports `MSB4186` for
`Path.TrimEndingDirectorySeparator`, update the checkout and reload the project.
Visual Studio 2026 Insiders 18.11.12224.323 project evaluation was checked using
its MSBuild with `DesignTimeBuild=true` and `BuildingInsideVisualStudio=true`;
the user also confirmed that all solution projects load after this fix.
Publish EFI images through
`dotnet run --project tools/OS.Tooling -- efi build`, which also verifies the artifact.

## Implemented Behavior

Required for all current host builds:

- Windows 10 or Windows 11 x64.
- A stable .NET SDK in the `10.0.400` feature band. `global.json` requests
  `10.0.401`, permits latest-patch roll-forward within that band, and rejects
  prerelease SDKs. The new Windows host selects `10.0.401`.
- NuGet access to restore .NET 10 packages.

The commands below use PowerShell syntax. Maintained build and test automation
runs in compiled .NET console tools and does not require a PowerShell subprocess.

Required for `OS.Tooling efi build`:

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
The native fixtures still record the selected executable and SHA-256. A process
override takes precedence; on Windows the saved user setting is also read when
the current terminal has not inherited it yet.

Check discovery before a full host run:

```powershell
$clrosLlvmObjdump = $env:CLROS_LLVM_OBJDUMP
if ([string]::IsNullOrEmpty($clrosLlvmObjdump)) {
    $clrosLlvmObjdump = [Environment]::GetEnvironmentVariable('CLROS_LLVM_OBJDUMP', 'User')
}
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


### C# evidence orchestration

`OS.Tooling ci smoke` and `OS.Tooling baseline evidence` create typed command
plans and execute compiled tools directly. Each execution builds the solution
once; the old second host-test build is removed. Both commands explicitly run
the complete host lane and require exactly one terminal `TEST_PASS host.tests`
with no host failure terminal. Fast or filtered results cannot satisfy this gate.
Independent steps continue after failures and record their own exit codes,
start/end times, durations and log paths. A summary is written after every
completed step and remains `incomplete` after cancellation. Planning does not
build, run native tools or create intent images. Execution requires an empty
log directory to prevent stale receipts from being reused.

The default host lane and focused C# fixture calls are for implementation
iteration. Full evidence remains explicit. The full-host orchestration timeout
is bounded at two hours because the recorded legacy gate took approximately
94 minutes; the CI job allows 150 minutes for that gate plus build and planning.
Those limits are ceilings, not performance claims. Compare actual durations in
new `results.json` and orchestration summaries before tightening them. CI marker
preset plans share `EvidenceOrchestration.CiMarkerPresets`; independent byte,
security and runtime assertions remain explicit tests.
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

### Compiled host commands

EFI, QEMU, verifier, CI and evidence commands execute .NET console applications
directly. They do not invoke Windows PowerShell or depend on `PSModulePath`.
The earlier shell-module failure remains historical evidence in
[step1077](../done/step1077.md).
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

## C# host test runner

The .NET 10 console runner discovers `[HostTest]` cases and `[HostFixture]`
groups without executing them. Module-initializer execution and the generated
subset-project script have been removed. Build the complete project once;
focused selection no longer needs companion source files.

```text
dotnet run --project tests/OS.HostTests
dotnet run --project tests/OS.HostTests -- --list
dotnet run --project tests/OS.HostTests -- --filter StdArrayHelpersTests --case "sort unmanaged spans"
dotnet run --project tests/OS.HostTests -- --lane full --filter DiagnosticsTests
dotnet run --project tests/OS.HostTests -- --lane full --filter M02OwnedX64Gdt
dotnet run --project tests/OS.HostTests -- --lane full
dotnet run --project tools/OS.Tooling -- contracts check
dotnet run --project tools/OS.Tooling -- repo check
dotnet run --project tools/OS.Tooling -- done reserve
```

The default fast lane emits `TEST_PASS host.tests.fast`. Case names and fixture
identifiers support case-insensitive substring filters; empty selections fail.
`--case <marker substring>` narrows grouped fixtures further. Native execution
is explicit with `--lane native` or `--lane full`. A filtered run only emits
`host.tests.focused`; only unfiltered full host success emits `host.tests`.

Fast fixtures run in isolated worker processes with bounded parallelism (up to
four workers by default). Use `--jobs 1` for a serial comparison or `--jobs N`
to set the bound. Native fixtures wait for active fast workers and execute
exclusively because they share native build outputs. Ordinary failures do not
stop subsequent cases; a crashed or timed-out fixture does not stop other fixtures
when process cleanup is confirmed. Unconfirmed cleanup cancels remaining work and
drains already active workers before returning failure.
Output is bounded and streamed to disk immediately. Ctrl+C cancels the active
worker and writes an incomplete, unsuccessful receipt. Terminal success requires
paired case markers, a clean exit, and no failures. Logs and an updated `results.json`, including
the selected inventory, live under `artifacts/logs/host-tests/<run>/`.

On the qualified Windows host, each child starts suspended, enters its owned
job, and then begins execution. Only its three standard-stream handles are
inherited. Root exit, cancellation, timeout, output overflow and capture failure
all require the job to be empty before returning; stdout and stderr remain
separate. Callback failures propagate after cleanup. This also prevents an early
parent exit from leaving descendants holding files or pipes. The generic
non-Windows process fallback cannot confirm the complete descendant tree and
reports that limitation as an unsuccessful result; it does not qualify host
acceptance evidence.

The build embeds source/dependency hashes and CI configuration. `--no-build`
runs reject changed, missing, or newly added source
inputs and check them again before reporting success. Rebuild
after changing inputs. `--list` only inspects metadata and does not run tests.

`tools/OS.Tooling/EfiContracts.cs` owns shared reviewed EFI expectations. Compile
counts and the inventory digest derive from its ordered inventory. Content
checks normalize CRLF to LF; actual build/artifact provenance still uses raw
bytes. A contract audit reports all mismatches and never updates expectations.

EFI build and verification, QEMU/libvirt, CI/baseline evidence, backlog sync,
package publishing and fixture controls now use compiled C# APIs. The maintained
PowerShell scripts have been removed. Fast success does not qualify native paths
or replace guest captures. Historical `done` commands remain records of the tools
used at their original revisions. CI and baseline callers explicitly select
`--lane full`. Per-fixture timings and the total wall time are saved in each
run's `results.json`; compare those values on identical inputs before claiming
a performance improvement.

The completed migration in [step1097](../done/step1097.md) records a 38.548-second
unfiltered fast run (3,071 checks) and a 1,412.723-second unfiltered full run
(4,119 checks), both with unchanged compiled inputs. The historical full run in
step1094 took 5,607.843 seconds. This observed reduction is approximately 4x;
the new run retains original coverage and adds runner controls and leaf markers,
so these are qualification measurements rather than a fixed-workload benchmark.
The separate 39-entry real QEMU matrix passed in 742.803 seconds. Full and guest
evidence remain separate from the daily fast command.

[Step1098](../done/step1098.md) removes repeated source-tree copies from the
single-file semantic rejection matrix. One isolated clone is reused, with exact
raw bytes restored in `finally` after every vector. Every vector still invokes
the producer and checks the original rejection; no validation result or native
artifact is cached. Restoration controls cover failure and exception paths.
The measured matrix fell from 20.178 to 15.783 seconds; the complete preflight
fixture passed its unchanged 44 checks in 183.746 seconds, versus 194.754 seconds
before the change. These are one before/after pair on this host, not a timing
guarantee. Native process-timeout controls and exclusive execution remain intact.
The final follow-up qualification passed 3,072 fast checks in 40.345 seconds and
all 4,120 full checks across 450 fixture groups in 1,398.014 seconds (23 minutes
18 seconds), with unchanged compiled inputs and no failures. The 582 guest
source inputs and four tool assemblies still match the step1097 real-run receipt.

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
dotnet run --project tools/OS.Tooling -- efi build
```

Expected current success markers:

```text
TEST_PASS efi.shell.publish
EFI_ARTIFACT artifacts/efi/BOOTX64.EFI
```

`OS.Tooling efi build` runs a host-toolchain preflight before
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
dotnet run --project tools/OS.Tooling -- qemu smoke -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15
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
dotnet run --project tools/OS.Tooling -- baseline evidence --plan-only
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
dotnet run --project tools/OS.Tooling -- ci smoke --dry-run
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
dotnet run --project tools/OS.Tooling -- efi build -PreflightOnly
dotnet run --project tools/OS.Tooling -- qemu smoke -DryRun
dotnet run --project tools/OS.Tooling -- ci smoke --dry-run
dotnet run --project tools/OS.Tooling -- baseline evidence --plan-only
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
`OS.Tooling ci smoke` also plans and captures `git diff --check` as
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
dotnet run --project tools/OS.Tooling -- qemu smoke -Qemu $qemu -OvmfCode $ovmf -TimeoutSeconds 15
dotnet run --project tools/OS.Tooling -- ci smoke --run-qcow2-real-smoke --qemu $qemu --qemu-img $qemuImg --ovmf-code $ovmf
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
dotnet run --project tools/OS.Tooling -- repo check
dotnet run --project tools/OS.Tooling -- qemu smoke -DryRun
dotnet run --project tools/OS.Tooling -- efi build
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

1. Keep `OS.Tooling efi build` tool discovery and reported MSVC/Windows
   SDK versions bound to the actual selected tools when the host changes.
2. Validate the existing `.gitea/workflows/ci-smoke.yml` on a compatible Windows
   runner and retain its logs/artifacts. Workflow presence alone is not CI
   execution evidence.
