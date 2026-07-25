# Step 167 - PCI Bus Scanner Injected Reader

## What Changed

- Added `PciBusScanner`, a read-only PCI bus scanner that uses an injected first-64-byte configuration-space reader delegate.
- Added host tests for single-device discovery, absent-function skipping, multifunction scanning, single-function skip behavior, destination-capacity exhaustion, and reader failure propagation.
- Updated driver and completion-gate documentation with the scanner evidence and explicit limitations.

## Why

Gitea issue #186 requires a bounded PCI scan contract before any live ECAM reader exists. The scanner needs to coordinate bus/device/function traversal and header parsing without mapping ECAM, performing MMIO reads, walking capabilities, binding drivers, or configuring interrupts.

This step keeps the live hardware boundary outside the scanner. Future platform code can provide a safe ECAM-backed reader after physical-memory/MMIO ownership rules exist.

## Files Changed

- `src/OS.Kernel/Drivers/PciBusScanner.cs`
- `tests/OS.HostTests/PciBusScannerTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step167.md`

## Implemented Behavior

- `PciConfigurationSpaceHeaderReader` fills a caller-owned first-64-byte function buffer for a bus/device/function tuple.
- `PciBusScanner.Scan` walks only caller-specified bus/device bounds and a caller-bounded maximum function.
- Vendor ID `0xFFFF` functions are skipped as not present.
- Function 0 is always checked first for each device, and functions 1-7 are scanned only when the function-0 header reports the PCI multifunction bit.
- Present type-0 functions are recorded with bus/device/function, vendor/device identity, revision, class/subclass/programming-interface, raw header type, multifunction flag, capability-list presence, and capability pointer.
- Destination capacity, injected-reader failures, and header-parse failures return deterministic status metadata without throwing from the scanner path.

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- Full host tests emitted `TEST_PASS host.tests`.
- PCI scanner host-test markers emitted:
  - `TEST_PASS drivers.pci.scan records single device`
  - `TEST_PASS drivers.pci.scan skips absent functions`
  - `TEST_PASS drivers.pci.scan honors multifunction bit`
  - `TEST_PASS drivers.pci.scan skips single-function extras`
  - `TEST_PASS drivers.pci.scan reports capacity exhaustion`
  - `TEST_PASS drivers.pci.scan reports reader failure`

## Failures

- No build or host-test failures were observed for this slice.

## Deferred Work

- Implement a safe ECAM-backed first-64-byte configuration-space reader from parsed MCFG segments.
- Feed scanner snapshots through capability parsing and virtio PCI discovery only after live config-space read ownership is represented by tests.
- Add driver binding and interrupt configuration only after platform and diagnostics prerequisites are in place.

## Evidence Boundary

This is host-tested injected-reader scanner evidence only. It does not prove live ECAM MMIO reads, ACPI table discovery, physical-memory reads, BAR sizing or mapping, capability walking, driver binding, MSI/MSI-X or interrupt configuration, QEMU hardware enumeration, storage, networking, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
