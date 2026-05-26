# Step 317 - Physical ECAM Header Reader Boundary

## What Changed

- Added `PciEcamPhysicalHeaderReader` for reading one PCI function's first 64 configuration bytes through `IPlatformPhysicalMemory`.
- Added `PciEcamPhysicalHeaderReadResult` and `PciEcamPhysicalHeaderReadStatus` as value-type status metadata for missing physical-memory sources, address-planning failures, destination-capacity failures, physical-read failures, short reads, and success.
- Added `PciEcamPhysicalConfigurationSpaceHeaderReader`, an adapter from the physical ECAM reader into the existing `PciConfigurationSpaceHeaderReader` delegate consumed by `PciBusScanner`.
- Added host tests for successful present-device reads, absent-vendor propagation through the scanner, invalid address tuples, destination capacity, physical-read failure, short reads, scanner adaptation, and no-managed-reference result layout.
- Updated driver and completion-gate docs to distinguish the injected physical-memory ECAM reader from live MMIO/PCI enumeration.

## Why

Issue #315 needed the next platform-driver slice after physical MCFG discovery. The repository could already parse MCFG entries and plan ECAM addresses, but the scanner still only consumed synthetic first-64-byte PCI configuration snapshots.

This step introduces the narrow boundary that turns an MCFG segment plus bus/device/function tuple into a status-coded physical-memory read. It keeps the read caller-buffered and fakeable so later live physical-memory/MMIO work can be added without changing the bus scanner contract.

## Files Changed

- `src/OS.Kernel/Drivers/PciEcamPhysicalHeaderReader.cs`
- `tests/OS.HostTests/PciEcamPhysicalHeaderReaderTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step317.md`

## Evidence

Commands run:

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'pci\.ecam\.physical_header|TEST_PASS host\.tests|TEST_FAIL'
dotnet build .\OS.slnx --nologo -m:1
git diff --check
```

Observed markers:

```text
TEST_PASS pci.ecam.physical_header reads present device
TEST_PASS pci.ecam.physical_header surfaces absent vendor through scanner
TEST_PASS pci.ecam.physical_header rejects invalid address tuple
TEST_PASS pci.ecam.physical_header reports destination capacity
TEST_PASS pci.ecam.physical_header reports physical read failure
TEST_PASS pci.ecam.physical_header reports short read
TEST_PASS pci.ecam.physical_header adapts into pci bus scanner
TEST_PASS pci.ecam.physical_header result contains no managed references
TEST_PASS host.tests
```

`dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.

`git diff --check` exited 0. Git reported LF-to-CRLF working-copy warnings for existing text files, but no whitespace errors.

## Failures

- None in this slice.

## Deferred Work

- No live EFI path maps physical memory or ECAM.
- No QEMU/OVMF PCI enumeration evidence exists for this reader.
- No PCI configuration writes, BAR sizing, BAR mapping, capability walking, MSI/MSI-X configuration, or driver binding is implemented.
- The physical-memory source remains injected and host-tested only.

## Evidence Boundary

This step proves a host-tested ECAM first-header reader over an injected `IPlatformPhysicalMemory` source. It is not live physical-memory access, not MMIO execution, not PCI enumeration evidence, and not cloud provider platform proof.
