# Step 050 - EFI Configuration Table ACPI RSDP Scanner

## What Changed

- Added `EfiConfigurationTableScanner` as a host-testable, no-stdlib-friendly UEFI configuration table scanner contract.
- Added value-type contracts for UEFI configuration table GUIDs, table entries, scan results, ACPI table kind, and status-coded failures.
- Recognized the UEFI ACPI 2.0 configuration table GUID and ACPI 1.0 configuration table GUID.
- Preferred ACPI 2.0 when ACPI 2.0 and ACPI 1.0 entries are both present.
- Supported ACPI 1.0 fallback when ACPI 2.0 is absent.
- Rejected zero RSDP pointers as `InvalidPointer`.
- Added pointer-based scanning for EFI-style raw configuration table metadata without dereferencing or parsing the RSDP.
- Added span-based scanning for host tests and future owned metadata paths.
- Updated boot, driver, and completion-gate docs to record scanner status and evidence boundaries.
- Follow-up integration source-linked the pointer scanner into the no-stdlib EFI project and wired `EfiMain` to emit `UEFI_ACPI_RSDP_READY`, `UEFI_ACPI_RSDP_MISSING`, or `UEFI_ACPI_RSDP_INVALID <status-code>`.

## Why It Changed

Gitea #68 needs a narrow ACPI RSDP discovery contract before ACPI table parsing. This step adds the scanner core, host tests, documentation, and live EFI marker wiring without parsing or dereferencing RSDP bytes.

## Files Changed

- `src/OS.Kernel/Boot/EfiConfigurationTableScanner.cs`
- `src/OS/Boot/EfiEntry.cs`
- `src/OS/OS.csproj`
- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `tests/OS.HostTests/EfiConfigurationTableScannerTests.cs`
- `docs/boot.md`
- `docs/drivers.md`
- `docs/qemu.md`
- `docs/completion-gates.md`
- `done/step050.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\run-qemu-smoke.ps1 -BuildEfi -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15
git diff --check
```

## Evidence

- Solution build passed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS uefi.config acpi2 preferred`.
- Host tests emitted `TEST_PASS uefi.config acpi1 fallback`.
- Host tests emitted `TEST_PASS uefi.config acpi missing`.
- Host tests emitted `TEST_PASS uefi.config acpi zero pointer rejected`.
- Host tests emitted `TEST_PASS uefi.config invalid metadata rejected`.
- Full host test run emitted `TEST_PASS host.tests`.
- EFI publish emitted `TEST_PASS efi.shell.publish`.
- QEMU smoke emitted `TEST_PASS qemu.smoke` while requiring the current default marker set including `UEFI_ACPI_RSDP_READY`.
- `artifacts/logs/qemu-smoke.summary.log` reported `MISSING_MARKERS none`.
- `artifacts/logs/qemu-smoke.log` contains `UEFI_ACPI_RSDP_READY` before `TEST_START boot.sequence`.
- `git diff --check` passed.

## Failures

- One rebuild after the scanner property cleanup initially failed with `MSB3027`/`MSB3021` because a completed `OS.HostTests` process was still holding `tests\OS.HostTests\bin\Debug\net10.0\OS.Kernel.dll`. Stopped that stale test process and reran the build successfully.
- One host-test run exited during an unrelated image-builder qcow2 test before printing failure detail. A rerun of the unchanged host test command completed successfully with `TEST_PASS host.tests`.

## Deferred Work

- Carry the selected RSDP address into the boot handoff contract.
- Parse and validate RSDP bytes only after live pointer provenance and safe memory-access rules are defined.
- Add ACPI SDT parsing host tests after RSDP discovery is wired.

## Evidence Boundary

This step proves host-side scanner behavior and a firmware-visible QEMU marker for OVMF ACPI RSDP discovery. It does not parse or dereference the RSDP, does not validate ACPI table contents, and does not yet carry the RSDP pointer through the typed handoff.
