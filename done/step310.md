# Step 310 - ACPI Root SDT Snapshot Boundary

## What Changed

- Added `AcpiRootSdtSnapshotReader`, which consumes a successful `AcpiRsdpSnapshotResult` and snapshots the root RSDT or XSDT through `IPlatformPhysicalMemory`.
- Added `AcpiRootSdtSnapshotResult` and status codes for invalid RSDP input, missing root table addresses, scratch/output capacity failures, header/full physical-read failures, invalid signatures, invalid table lengths, invalid pointer-array length, and invalid checksums.
- Extended `AcpiRsdpSnapshotResult` to preserve validated RSDT and XSDT physical addresses for downstream root table selection.
- Added host tests for ACPI 1.0 RSDT selection, ACPI 2.0+ XSDT preference, ACPI 2.0+ RSDT fallback when XSDT is absent, physical-read failures, malformed root table metadata, and no-managed-reference result layout.
- Updated boot, driver, and completion-gate docs to record the new boundary and its evidence limit.

## Why It Changed

After the RSDP snapshot boundary, the next ACPI slice needs to copy the root table bytes before any child SDT selection can be trusted. This step keeps that work narrow: it reads and validates exactly one root RSDT/XSDT snapshot into caller-owned storage and leaves MCFG/MADT/HPET selection to later issues.

## Files Changed

- `src/OS.Kernel/Boot/AcpiRsdpSnapshot.cs`
- `src/OS.Kernel/Boot/AcpiRootSdtSnapshot.cs`
- `tests/OS.HostTests/AcpiRsdpSnapshotTests.cs`
- `tests/OS.HostTests/AcpiRootSdtSnapshotTests.cs`
- `docs/boot.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step310.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo
```

## Logs And Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo` passed with 0 warnings and 0 errors.

Expected focused markers once the shared host-test project is unblocked:

```text
TEST_PASS acpi.root_sdt.snapshot reads rsdt from acpi1 rsdp
TEST_PASS acpi.root_sdt.snapshot prefers xsdt from acpi2 rsdp
TEST_PASS acpi.root_sdt.snapshot falls back to rsdt when xsdt address is zero
TEST_PASS acpi.root_sdt.snapshot rejects invalid rsdp input
TEST_PASS acpi.root_sdt.snapshot rejects missing root address
TEST_PASS acpi.root_sdt.snapshot reports scratch and destination capacity
TEST_PASS acpi.root_sdt.snapshot reports physical read failures
TEST_PASS acpi.root_sdt.snapshot rejects invalid signature length and checksum
TEST_PASS acpi.root_sdt.snapshot rejects invalid pointer array length
TEST_PASS acpi.root_sdt.snapshot result contains no managed references
```

## Failures

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo --no-dependencies` failed while issue #313 was in progress because `tests/OS.HostTests/QemuRunnerTests.cs` referenced missing `TestSmokeScriptDryRunAcpiPreset` and `TestRealModeMarkerPresetSummary` methods. This was outside the #310 ACPI write scope and was reported back to the #313 worker.

## Deferred Work

- The reader does not parse child table entries into table snapshots.
- No MCFG, MADT, HPET, FADT, APIC, PCI, timer, or interrupt setup is implemented.
- No live firmware RSDP pointer is dereferenced.
- No real physical memory is mapped, and no cacheability/MMIO/paging policy exists.
- The boundary is not wired into `EfiMain` or QEMU smoke.

## Evidence Boundary

This step is host-tested root-table snapshot composition over synthetic RSDP and physical-memory data. It is not live ACPI discovery, QEMU ACPI table walking, platform enumeration, PCI ECAM mapping, or cloud hardware evidence.
