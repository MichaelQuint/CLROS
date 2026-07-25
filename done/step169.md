# Step 169 - PCI BAR Metadata Decoder

## What Changed

- Added `PciBarDecoder` for read-only type-0 PCI BAR metadata decoding.
- Added host tests for absent, I/O, 32-bit MMIO, 64-bit MMIO, reserved, and malformed BAR cases.
- Updated driver and completion-gate docs.

## Why

Gitea issue #185 needed a small contract between raw PCI configuration-header parsing and future BAR sizing/mapping. The decoder records BAR metadata without writing PCI config space or mapping MMIO.

## Files Changed

- `src/OS.Kernel/Drivers/PciBarDecoder.cs`
- `tests/OS.HostTests/PciBarDecoderTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`

## Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed in the worker run.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build *> .\artifacts\logs\host-tests-issue185.log` emitted `TEST_PASS host.tests`.
- Observed markers:
  - `TEST_PASS drivers.pci.bar decodes 32bit mmio`
  - `TEST_PASS drivers.pci.bar decodes 64bit mmio pair`
  - `TEST_PASS drivers.pci.bar decodes io aperture`
  - `TEST_PASS drivers.pci.bar records absent zero`
  - `TEST_PASS drivers.pci.bar rejects reserved memory type`
  - `TEST_PASS drivers.pci.bar rejects 64bit at index 5`
- `scripts/check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited 0 with CRLF normalization warnings only.

## Evidence Boundary

This is host-tested BAR metadata decoding only. It does not size BARs, write all-ones, write PCI config space, map MMIO, allocate pages, configure MSI/MSI-X, bind drivers, or prove live PCI enumeration.
