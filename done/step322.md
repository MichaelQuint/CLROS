# Step 322 - Opt-in ACPI physical discovery QEMU proof

## Summary

Issue #317 local work is complete. The EFI shell now has an explicit opt-in
ACPI physical discovery probe that snapshots and validates the firmware RSDP,
root SDT, and MCFG table before `ExitBootServices`, and the QEMU smoke runner
now treats the ACPI marker sequence as ordered evidence instead of loose
substring matching.

## Why

The previous ACPI evidence stopped at host-tested table parsers and firmware
configuration-table handoff markers. The blocked issue needed a real QEMU/OVMF
boot path that dereferences firmware-provided ACPI physical addresses in a
controlled pre-`ExitBootServices` probe, emits stable serial markers, and fails
the smoke run if the marker set or order is incomplete.

## Files changed

- `src/OS/OS.csproj`
- `src/OS/Boot/EfiEntry.cs`
- `src/OS.Kernel/Diagnostics/Probes.cs`
- `scripts/build-efi-shell.ps1`
- `scripts/run-qemu-smoke.ps1`
- `tools/OS.QemuRunner/SmokeMarkerPreset.cs`
- `tools/OS.QemuRunner/SerialMarkerSummary.cs`
- `tools/OS.QemuRunner/Program.cs`
- `tests/OS.HostTests/DiagnosticsTests.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `tests/OS.HostTests/SerialMarkerSummaryTests.cs`
- `README.md`
- `docs/architecture.md`
- `docs/boot-order.md`
- `docs/boot.md`
- `docs/completion-gates.md`
- `docs/diagnostics.md`
- `docs/drivers.md`
- `docs/efi-build.md`
- `docs/pal.md`
- `docs/qemu.md`
- `docs/repo-audit.md`

## Tests run

- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\build-efi-shell.ps1 -EnableAcpiPhysicalDiscoveryProbe -EfiOutputPath artifacts/efi/BOOTX64-ACPI.EFI -LogPath artifacts/logs/efi-shell-publish-acpi.log`
- `.\scripts\run-qemu-smoke.ps1 -EfiArtifact artifacts/efi/BOOTX64-ACPI.EFI -Image artifacts/images/os-acpi.raw -SerialLog artifacts/logs/qemu-smoke-acpi.log -SummaryLog artifacts/logs/qemu-smoke-acpi.summary.log -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -MarkerPreset acpi`
- `git diff --check`
- `rg --files src std apps | Where-Object { $_ -match '\.(c|cpp|h|asm|s)$' }`

## Evidence

- Host tests ended with `TEST_PASS host.tests`.
- Focused host build succeeded with `0 Warning(s)` and `0 Error(s)`.
- Full solution build succeeded with `0 Warning(s)` and `0 Error(s)`.
- EFI publish emitted:
  - `TEST_START efi.shell.publish`
  - `EFI_PROBE ACPI_PHYSICAL_DISCOVERY`
  - `TEST_PASS efi.shell.publish`
  - `EFI_ARTIFACT artifacts/efi/BOOTX64-ACPI.EFI`
- Real QEMU/OVMF ACPI smoke emitted:
  - `TEST_PASS imagebuilder.raw`
  - `TEST_START qemu.smoke`
  - `MARKER_PRESETS acpi`
  - `TEST_PASS qemu.smoke`
- `artifacts/logs/qemu-smoke-acpi.log` includes the ordered ACPI markers:
  - `ACPI_RSDP_SNAPSHOT_READY`
  - `ACPI_RSDP_VALIDATED`
  - `ACPI_ROOT_SDT_SNAPSHOT_READY`
  - `ACPI_ROOT_SDT_VALIDATED`
  - `ACPI_MCFG_DISCOVERY_READY`
  - `OS_SMOKE_PASS`
- `artifacts/logs/qemu-smoke-acpi.summary.log` includes:
  - `MISSING_MARKERS none`
  - `OUT_OF_ORDER_MARKERS none`
- `git diff --check` exited 0. Git reported only existing line-ending normalization warnings for modified text files.
- The forbidden first-party source scan produced no output.

## Failures

- Gitea issue updates and closure remain blocked in this environment because the API rejects writes with `token is required`; no credential-bearing environment variable was available. Credentials were not written to disk or logs.

## Deferred work

- This proves an opt-in pre-`ExitBootServices` physical ACPI discovery probe under QEMU/OVMF. It does not implement a general physical-memory mapper, ACPI interpreter, APIC enablement, PCI ECAM scan, interrupt routing, or post-`ExitBootServices` ACPI access.
- Keep the probe opt-in until the platform memory and ACPI ownership model is promoted into the normal boot path.
