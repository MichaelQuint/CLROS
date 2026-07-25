# Step 316 - Physical ACPI MCFG Discovery Boundary

## What Changed

- Extended `AcpiMcfgDiscovery` with `TryDiscoverFromRootSdtSnapshot`.
- Added `AcpiMcfgPhysicalDiscoveryResult` and `AcpiMcfgPhysicalDiscoveryStatus` as value-type result metadata for root-SDT-to-MCFG discovery through `IPlatformPhysicalMemory`.
- Added host tests for physical-memory-backed XSDT and RSDT MCFG selection, missing MCFG, child header read failure, full-table read failure, invalid checksum, MCFG parse failure, segment destination exhaustion, and result layout.
- Updated boot, driver, and completion-gate docs to distinguish this host-tested bridge from live EFI ACPI walking, ECAM mapping, and PCI enumeration.

## Why

Issue #311 needed the next platform-driver slice after the root SDT snapshot boundary. The repository already had caller-provided table snapshot composition and an MCFG parser, but it did not yet bridge a validated root RSDT/XSDT snapshot to child table reads through the platform physical-memory contract.

This step keeps that bridge caller-buffered and status-coded. It prepares the PCI ECAM reader work without claiming live hardware enumeration.

## Files Changed

- `src/OS.Kernel/Boot/AcpiMcfgDiscovery.cs`
- `tests/OS.HostTests/AcpiMcfgPhysicalDiscoveryTests.cs`
- `docs/boot.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step316.md`

## Evidence

Commands run:

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'acpi\.mcfg\.discovery|TEST_PASS host\.tests|TEST_FAIL'
dotnet build .\OS.slnx --nologo -m:1
```

Observed markers:

```text
TEST_PASS acpi.mcfg.discovery reads xsdt child mcfg from physical memory
TEST_PASS acpi.mcfg.discovery reads rsdt child mcfg from physical memory
TEST_PASS acpi.mcfg.discovery reports physical mcfg missing
TEST_PASS acpi.mcfg.discovery reports child header read failure
TEST_PASS acpi.mcfg.discovery reports child full read failure
TEST_PASS acpi.mcfg.discovery rejects invalid physical mcfg checksum
TEST_PASS acpi.mcfg.discovery reports physical mcfg parse failure
TEST_PASS acpi.mcfg.discovery reports physical segment destination exhaustion
TEST_PASS acpi.mcfg.discovery physical result contains no managed references
TEST_PASS host.tests
```

`dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.

## Failures

- The first focused build used `--no-dependencies`, so `OS.HostTests` compiled against the previous `OS.Kernel` output and could not see the new API. Re-running the build without `--no-dependencies` rebuilt dependencies and passed.

## Deferred Work

- No live EFI path dereferences the firmware-provided RSDP pointer.
- No QEMU/OVMF ACPI table walking evidence exists yet.
- No ECAM MMIO mapping, PCI configuration reads, BAR sizing, interrupts, or driver binding is implemented.
- MADT, HPET, FADT, APIC, and timer table discovery remain separate future slices.

## Evidence Boundary

This step proves host-tested physical-memory-backed MCFG child-table discovery over synthetic root table and injected physical-memory data. It is not live ACPI discovery, not cloud provider platform proof, and not PCI enumeration evidence.
