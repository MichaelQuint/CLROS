# Step 053 - Host-Tested ACPI RSDP Byte Validator

## What Changed

- Added `AcpiRsdpValidator`, an allocation-free RSDP byte validation contract.
- Added status-coded results for ACPI 1.0 and ACPI 2.0+ RSDP validation failures.
- Added host tests for valid ACPI 1.0 and ACPI 2.0+ records, bad signatures, zero OEM ID, unsupported revision, checksum failures, and invalid extended lengths.

## Why It Changed

Issue #71 needed a small ACPI validation slice separate from pointer discovery and typed handoff. The validator gives later boot code a deterministic contract once safe physical-memory reads are available.

## Files Changed

- `src/OS.Kernel/Boot/AcpiRsdpValidator.cs`
- `tests/OS.HostTests/AcpiRsdpValidatorTests.cs`
- `docs/boot.md`
- `docs/completion-gates.md`
- `docs/drivers.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Build passed with 0 warnings and 0 errors.
- Host tests emitted:
  - `TEST_PASS acpi.rsdp validates acpi1 checksum`
  - `TEST_PASS acpi.rsdp validates acpi2 extended checksum`
  - `TEST_PASS acpi.rsdp rejects signature metadata and revision`
  - `TEST_PASS acpi.rsdp rejects checksum failures`
  - `TEST_PASS acpi.rsdp rejects extended length failures`
  - `TEST_PASS host.tests`

## Failures

- No test failures remained after integration.

## Deferred Work

- The validator is host-tested only and currently accepts caller-provided byte spans.
- The live EFI path does not dereference the firmware-provided RSDP pointer.
- A safe physical-memory read contract is required before validating the live RSDP bytes.

## Evidence Boundary

This step proves byte-level ACPI RSDP validation against synthetic host buffers. It does not prove live firmware pointer dereference, SDT discovery, ACPI table parsing, or APIC enumeration.
