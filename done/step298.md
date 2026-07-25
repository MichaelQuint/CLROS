# Step 298 - qcow2 QEMU Smoke Evidence

## What Changed

- Added a script-level qcow2 smoke wrapper in `scripts/run-qemu-qcow2-smoke.ps1`.
- Extended `scripts/run-qemu-smoke.ps1` with a validated `-DiskFormat raw|qcow2` parameter and comma-delimited marker expansion for script-level required/forbidden marker lists.
- Added host coverage in `tests/OS.HostTests/QemuRunnerTests.cs` for script-level qcow2 disk-format propagation and the qcow2 wrapper contract.
- Updated QEMU, image-builder, cloud-import, and completion-gate docs to record real local qcow2 smoke evidence.

## Why It Changed

Cloud image tooling already had a host-side `convert-qcow2` wrapper and QEMU runner support for `--disk-format qcow2`, but the project still only had dry-run qcow2 evidence. This step ties raw image creation, qcow2 conversion, and QEMU/OVMF boot together so Gate 11 can cite real local qcow2 boot proof without claiming provider import compatibility.

## Files Changed

- `scripts/run-qemu-smoke.ps1`
- `scripts/run-qemu-qcow2-smoke.ps1`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/qemu.md`
- `docs/image-builder.md`
- `docs/cloud-import.md`
- `docs/completion-gates.md`
- `done/step298.md`

## Tests Run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\run-qemu-qcow2-smoke.ps1 -DryRun -RawImage artifacts/images/os.raw -Qcow2Image artifacts/images/os.qcow2 -SerialLog artifacts/logs/qemu-smoke-qcow2.log -SummaryLog artifacts/logs/qemu-smoke-qcow2.summary.log`
- `.\scripts\run-qemu-qcow2-smoke.ps1 -RawImage artifacts/images/os.raw -Qcow2Image artifacts/images/os.qcow2 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -QemuImg "C:\Program Files\qemu\qemu-img.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Logs And Evidence

Dry-run evidence emitted:

```text
TEST_START qemu.qcow2.smoke
TEST_PASS imagebuilder.qcow2
if=none,id=esp,format=qcow2,file=artifacts/images/os.qcow2
TEST_PASS qemu.runner.dryrun
TEST_PASS qemu.qcow2.smoke
```

Real qcow2 smoke evidence emitted:

```text
TEST_START qemu.qcow2.smoke
TEST_PASS imagebuilder.raw
TEST_PASS imagebuilder.qcow2
TEST_PASS qemu.smoke
TEST_PASS qemu.qcow2.smoke
```

Integrated build/test evidence emitted:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
TEST_PASS host.tests
TEST_PASS naming.guard
TEST_PASS source.firewall
```

`artifacts/logs/qemu-smoke-qcow2.log` contained:

```text
BOOT_MEMORY_REGIONS_READY
BOOT_MEMORY_REGION_COUNT 102
OS_BOOT_OK
OS_SMOKE_PASS
```

`artifacts/logs/qemu-smoke-qcow2.summary.log` contained:

```text
QEMU_SMOKE_STATUS pass
IMAGE_ARTIFACT artifacts/images/os.qcow2
MISSING_MARKERS none
FORBIDDEN_MARKER_HIT none
FAIL_REASON none
```

`artifacts/images/os.qcow2` was created at 720,896 bytes on this host.

## Failures

- Initial qcow2 dry-run wrapper attempt failed because repeated `-RequireMarker` arguments were forwarded to `run-qemu-smoke.ps1` in a way PowerShell did not accept. The wrapper now joins markers for the child script, and `run-qemu-smoke.ps1` expands comma-delimited marker strings.
- `git diff --check` exited 0 but printed existing LF-to-CRLF normalization warnings for many modified files.

## Deferred Work

- libvirt real boot for raw/qcow2 artifacts.
- ISO generation only if a concrete deployment path needs it.
- Provider cloud import validation for AWS, Azure, GCE, and generic providers.
- Guest storage/networking/app/Kestrel evidence remains separate and unclaimed.
