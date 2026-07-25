# Step 289 - Platform Configuration-Table Contract

## What Changed

- Added `IPlatformConfigurationTables` as the guarded platform contract for ACPI
  RSDP discovery from UEFI configuration-table metadata.
- Added `IPlatformConfigurationTableSource` and
  `PlatformConfigurationTableMetadata` so the platform boundary exposes only a
  base address and entry count.
- Added `PlatformConfigurationTableScanResult` and
  `PlatformConfigurationTableScanStatus` so configuration-table scans are
  status-coded and compatible with UEFI-gone guard behavior.
- Wired `GuardedPlatformConfigurationTables` through `IPlatformFirmware` so
  scans are denied when firmware boot services are simulated gone or already
  exited.
- Reused the existing `EfiConfigurationTableScanner` for ACPI 2.0 preference,
  ACPI 1.0 fallback, missing-table status, and invalid metadata reporting.
- Added host tests covering allowed scans, UEFI-gone denial, post-ExitBootServices
  denial, invalid metadata, ACPI 2.0 preference, ACPI 1.0 fallback, missing
  ACPI entries, and no-managed-reference metadata.

## Why It Changed

The live EFI entry path already performs firmware configuration-table scanning
directly. The OS needs the same behavior behind a platform interface before
broader UEFI encapsulation and eventual UEFI-gone enforcement can be trusted.
This step creates the small guarded contract without parsing ACPI table bytes or
calling `ExitBootServices`.

## Files Changed

- `src/OS.Kernel/Platform/IPlatformConfigurationTables.cs`
- `tests/OS.HostTests/PlatformConfigurationTableGuardTests.cs`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/drivers.md`
- `docs/pal.md`
- `docs/completion-gates.md`
- `done/step289.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

The solution build passed with 0 warnings and 0 errors.

The full host-test run emitted:

```text
TEST_PASS platform.config acpi scan allowed before UEFI gone
TEST_PASS platform.config acpi scan denied in UEFI-gone simulation
TEST_PASS platform.config acpi scan denied after ExitBootServices
TEST_PASS platform.config acpi scan reports invalid metadata
TEST_PASS platform.config acpi2 preferred
TEST_PASS platform.config acpi1 fallback
TEST_PASS platform.config acpi missing
TEST_PASS platform.config metadata contains no managed references
TEST_PASS host.tests
```

## Failures

No failures were observed in the final verification pass.

## Evidence Boundary

This is host-test evidence for a guarded configuration-table platform contract.
It does not prove physical-memory reads, ACPI RSDP byte validation in the live
EFI path, RSDT/XSDT walking from real firmware memory, APIC/PCI discovery, or a
real `ExitBootServices` transition.

## Deferred Work

- Route live ACPI RSDP handoff through the platform adapter once the no-stdlib
  boundary can carry that abstraction safely.
- Add a safe physical-memory snapshot/read path for RSDP and SDT validation.
- Keep configuration-table access denied after simulated or real firmware
  shutdown.
- Use this contract as one input to later ACPI table discovery, not as ACPI
  parser completion evidence.
