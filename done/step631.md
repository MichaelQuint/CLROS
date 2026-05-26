# Step 631 - QEMU Image Preflight

## Summary

Added a host-side QEMU runner preflight mode. `OS.QemuRunner preflight` and
`--preflight` validate the configured image artifact, raw/qcow2 format hint,
optional explicit OVMF path, serial-log directory metadata, marker contract,
and generated QEMU command arguments without launching QEMU or requiring
libvirt.

## Why

Issue #7 needed a bounded cloud-tooling slice that validates an existing
runner/artifact contract without starting QEMU or libvirt. Libvirt already had
XML validation and serial-log checking, so the smallest non-overlapping gap was
QEMU input/image preflight.

## Files changed

- `tools/OS.QemuRunner/Program.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/qemu.md`
- `done/step631.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\run-qemu-smoke.ps1 -DryRun`
- `git diff --check`

## Evidence

- The targeted host-test build initially hit a transient output DLL lock from
  another `OS.HostTests` process, then passed on retry with 0 warnings and 0
  errors.
- The host-test run emitted `TEST_PASS host.tests`.
- New QEMU preflight host-test markers:
  - `TEST_PASS qemu preflight validates raw image artifact`
  - `TEST_PASS qemu preflight rejects missing image artifact`
  - `TEST_PASS qemu preflight rejects disk format mismatch`
- QEMU dry-run emitted `TEST_PASS qemu.runner.dryrun` and preserved the default
  required marker list:
  `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY`.
- `git diff --check` passed with line-ending warnings for existing dirty files
  but no whitespace errors.

## Failures

- First build attempt failed because existing `OS.HostTests`/`.NET Host`
  processes were holding `tests\OS.HostTests\bin\Debug\net10.0\OS.Kernel.dll`
  and `OS.Std.NoRuntime.Shared.dll`. The lock cleared and the exact command
  passed on retry.

## Deferred work

- Add deeper raw GPT/FAT32 artifact validation only if the QEMU runner needs to
  prove ESP contents directly. The new preflight intentionally checks only
  bounded runner/image input shape and does not parse the disk layout.
- Keep real QEMU boot, libvirt lifecycle execution, and cloud-provider import
  validation behind their separate evidence gates.

## Evidence boundary

This is host-side runner preflight only. It does not launch QEMU, connect to
libvirt, parse GPT/FAT32 contents, capture serial output, prove `ExitBootServices`,
prove live storage/networking, or satisfy cloud/Kestrel readiness gates.
