# Step 302 - Libvirt Raw/qcow2 Smoke Intent And Missing-Tool Evidence

## What Changed

- Added `scripts/run-libvirt-smoke.ps1` for raw/qcow2 libvirt smoke dry-run and missing-tool evidence.
- Extended `tools/OS.LibvirtRunner` with `--disk-format raw|qcow2` and default marker requirements matching the current QEMU smoke boundary.
- Added host tests for qcow2 domain XML, invalid disk-format rejection, and script-level dry-run/missing-tool marker coverage.
- Updated libvirt, cloud-import, and completion-gate docs.

## Why It Changed

Issue #302 needed a libvirt evidence path that remains honest on hosts without libvirt. Raw and qcow2 QEMU smoke are now real local boot evidence, but libvirt still lacked a wrapper that could record the intended domain XML, disk format, serial log, required markers, and missing-host-tool status without claiming a boot.

## Files Changed

- `scripts/run-libvirt-smoke.ps1`
- `tools/OS.LibvirtRunner/Program.cs`
- `tests/OS.HostTests/LibvirtRunnerTests.cs`
- `docs/libvirt.md`
- `docs/cloud-import.md`
- `docs/completion-gates.md`
- `done/step302.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
.\scripts\run-libvirt-smoke.ps1 -DryRun -Image artifacts/images/os.qcow2 -DiskFormat qcow2 -SerialLog artifacts/logs/libvirt-smoke-qcow2.log -DomainXml artifacts/logs/libvirt-smoke-qcow2.xml
.\scripts\run-libvirt-smoke.ps1 -Image artifacts/images/os.qcow2 -DiskFormat qcow2 -SerialLog artifacts/logs/libvirt-smoke-qcow2.log -DomainXml artifacts/logs/libvirt-smoke-qcow2.xml -Virsh C:\missing\virsh.exe
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
git diff --check
```

## Logs And Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors after stopping stale `OS.HostTests` processes that were holding test output binaries.
- The dry-run script emitted:

```text
TEST_START libvirt.smoke
LIBVIRT_DISK_FORMAT qcow2
REQUIRED_MARKERS OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY
LIBVIRT_XML_ARTIFACT artifacts/logs/libvirt-smoke-qcow2.xml
LIBVIRT_DRY_RUN
TEST_PASS libvirt.smoke.dryrun
```

- The missing-tool script path emitted:

```text
TEST_SKIP libvirt.smoke missing_tool=virsh
TEST_PASS libvirt.smoke.missing_tool_reported
```

- Full host tests emitted `TEST_PASS host.tests`, including `TEST_PASS libvirt --dry-run emits qcow2 disk XML`, `TEST_PASS libvirt rejects invalid disk format`, and `TEST_PASS libvirt smoke script documents dry-run and missing-tool evidence`.
- The naming/source guard emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited 0. Git reported existing LF-to-CRLF normalization warnings only.

## Failures

- The first script implementation surfaced PowerShell native stderr as `NativeCommandError` while capturing `OS.LibvirtRunner --diagnostics`; the script now generates XML without relying on diagnostic stderr.
- Two build attempts were blocked by stale local `OS.HostTests` processes holding output binaries. The stale test processes were stopped and the solution build then passed.

## Deferred Work

- No libvirt connection is opened.
- No transient domain is created or destroyed.
- No libvirt serial log is polled.
- No libvirt real boot, provider import, guest storage/networking, hosted .NET, ASP.NET Minimal API, or Kestrel behavior is proven.

## Evidence Boundary

This step proves libvirt XML/disk-format command shape, script-level raw/qcow2 smoke intent, and honest missing-`virsh` reporting. It does not prove a libvirt VM boot.
