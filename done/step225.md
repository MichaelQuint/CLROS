# Step 225 - ENA PCI Candidate Metadata

## Summary

Added the issue #243 ENA PCI candidate metadata contract. The new code
centralizes Amazon/ENA PCI IDs, validates the supported VF and LLQ VF class
shape from caller-provided ECAM scan records, and preserves source/Pci location
metadata in bounded candidate selection output.

## Why

AWS Nitro networking uses ENA, but OS still has no live PCI reader or network
driver. This step creates the first host-testable ENA selection boundary without
reading ECAM, sizing or mapping BARs, configuring MSI-X, allocating DMA memory,
creating admin queues, or moving packets.

## Files changed

- `src/OS.Kernel/Drivers/EnaPciCandidate.cs`
- `tests/OS.HostTests/EnaPciCandidateTests.cs`
- `docs/drivers.md`
- `docs/ena-driver-plan.md`
- `done/step225.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- Build completed with `Build succeeded`, `0 Error(s)`.
- Host test markers:
  - `TEST_PASS drivers.ena.pci.candidate recognizes ena vf devices`
  - `TEST_PASS drivers.ena.pci.candidate rejects unsupported vendor device`
  - `TEST_PASS drivers.ena.pci.candidate rejects invalid pci shape`
  - `TEST_PASS drivers.ena.pci.candidate preserves pci source metadata`
  - `TEST_PASS drivers.ena.pci.candidate reports destination exhaustion`
- Host test run ended with `TEST_PASS host.tests`.
- `git diff --check` returned exit code 0. It emitted only line-ending
  normalization warnings for existing modified docs files.

## Failures

None. The build emitted transient `MSB3026` copy retry warnings because another
`OS.HostTests` process had `OS.HostTests.exe` open, but the command completed
successfully with zero errors.

## Deferred work

- No live ECAM reads or PCI enumeration.
- No BAR sizing, BAR mapping, MSI-X capability walk, interrupt setup, or PCI
  command-register writes.
- No DMA allocation, ENA admin queue, MAC retrieval, TX/RX queues, packet I/O,
  DHCP, TCP, ASP.NET Minimal API, or Kestrel evidence.
