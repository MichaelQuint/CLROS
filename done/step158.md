# Step 158 - ACPI MCFG Parser To PCI ECAM Segments

## What Changed

- Added `AcpiMcfgParser` under `src/OS.Kernel/Boot` as a pure ACPI table parser for caller-provided MCFG bytes.
- Converted MCFG allocation entries into `OS.Drivers.PciEcamSegment` values for the existing PCI ECAM address-planning contract.
- Added status-coded validation for signature, declared minimum length, source length, the 8-byte MCFG reserved area after the ACPI SDT header, 16-byte allocation-entry alignment, destination capacity, and invalid bus ranges.
- Added focused host tests for valid ECAM segment parsing and malformed MCFG rejection.
- Updated driver and completion-gate documentation with the parser-only evidence boundary.

## Why It Changed

Gitea issue #177 needs the next ACPI-to-PCI boundary before a read-only PCI ECAM scanner can consume real firmware table data. Keeping this parser in `OS.Boot` matches the existing RSDP byte validator and keeps firmware table decoding separate from future PCI MMIO scanning, while still returning the driver-facing `PciEcamSegment` records that `PciEcamConfigurationSpace` already consumes.

## Files Changed

- `src/OS.Kernel/Boot/AcpiMcfgParser.cs`
- `tests/OS.HostTests/AcpiMcfgParserTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step158.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 -o .\artifacts\issue177\hosttests
dotnet run --project .\artifacts\issue177\focused\Issue177FocusedTests.csproj --nologo *> .\artifacts\logs\host-tests-issue177-focused.log
```

## Logs And Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- The first standard host-test project build was temporarily blocked by an existing `OS.HostTests.exe` process holding `tests/OS.HostTests/bin/Debug/net10.0/OS.Kernel.dll`; after that process exited, the same command passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 -o .\artifacts\issue177\hosttests` passed with 0 warnings and 0 errors.
- `artifacts/logs/host-tests-issue177-focused.log` exited with `FOCUSED_TEST_EXIT 0` and emitted:
  - `TEST_PASS acpi.mcfg parses ecam segment`
  - `TEST_PASS acpi.mcfg rejects bad signature`
  - `TEST_PASS acpi.mcfg rejects truncated entry`
  - `TEST_PASS acpi.mcfg rejects invalid bus range`
  - `TEST_PASS issue177.focused`
- A full isolated host-test executable run also emitted the four MCFG markers before failing later in unrelated ImageBuilder tests due alternate-output path assumptions.

## Failures

- A transient lock from PID 12140 (`OS.HostTests.exe`) blocked the first normal host-test build attempt. The process exited and the normal host-test build then passed.
- The full isolated host-test run is not counted as full-suite pass evidence because unrelated ImageBuilder tests failed after the MCFG markers. The focused issue harness passed.

## Deferred Work

- No RSDP/RSDT/XSDT table walking is implemented.
- No physical-memory read contract is used to fetch MCFG from firmware tables.
- ACPI SDT checksum validation is not implemented in this parser slice.
- No ECAM MMIO mapping, PCI configuration-space scan, BAR decode, or driver binding is implemented.

## Evidence Boundary

This step is host-tested parser evidence only. It proves caller-provided MCFG table bytes can be validated and converted into PCI ECAM segment contracts. It does not prove live ACPI table discovery, physical-memory access, UEFI-to-owned-driver transition, PCI enumeration, MMIO access, virtio device binding, storage, networking, or QEMU hardware evidence.
