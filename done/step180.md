# Step 180 - PCI Virtio Candidate Selector

## Summary

Added a host-tested selector that filters ECAM-scanned PCI functions into caller-owned modern virtio block/network candidate records.

## Why

The platform path now has ACPI MCFG parsing, ECAM segment scan coordination, PCI type-0 identity metadata, BAR decoding, and virtio capability metadata contracts. A narrow candidate selector gives later capability walking and BAR validation work a deterministic bridge from scanned PCI functions without performing MMIO, binding, or device initialization.

## Files changed

- `src/OS.Kernel/Drivers/VirtioPciCandidateSelector.cs`
- `tests/OS.HostTests/VirtioPciCandidateSelectorTests.cs`
- `docs/drivers.md`
- `done/step180.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- `dotnet build .\OS.slnx --nologo -m:1` completed with 0 warnings and 0 errors.
- Host tests emitted the required #199 markers:
  - `TEST_PASS drivers.virtio.pci.candidate selects block and network devices`
  - `TEST_PASS drivers.virtio.pci.candidate preserves pci location`
  - `TEST_PASS drivers.virtio.pci.candidate skips unsupported vendor`
  - `TEST_PASS drivers.virtio.pci.candidate skips unsupported virtio device id`
  - `TEST_PASS drivers.virtio.pci.candidate requires capability pointer`
  - `TEST_PASS drivers.virtio.pci.candidate reports destination exhaustion`
- Host tests ended with `TEST_PASS host.tests`.
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited 0 with CRLF normalization warnings only.

## Failures

- No failures in this slice.

## Deferred work

- PCI capability walking from live configuration-space bytes.
- Virtio capability-set discovery from candidate records.
- BAR aperture validation wiring from selector output.
- ECAM MMIO reads, BAR mapping, MSI/MSI-X, feature negotiation, queue setup, interrupts, and driver binding.
