# Step 156 - PCI ECAM Configuration Address Planning Contract

## What Changed

- Added `PciEcamConfigurationSpace` as a pure address-planning contract for PCIe ECAM configuration-space accesses.
- Added validation for bus range, device number, function number, register alignment, register bounds, and address overflow.
- Added focused host tests for deterministic address calculation and invalid-input rejection.
- Preserved the issue #171 evidence after a concurrent done-step number collision.

## Why It Changed

Gitea issue #171 needs a small PCI foundation before virtio-blk and virtio-net can bind through cloud-first PCI discovery. This contract keeps ECAM math deterministic and host-testable before live MCFG parsing, MMIO mapping, BAR decoding, or device reads are implemented.

## Files Changed

- `src/OS.Kernel/Drivers/PciEcamConfigurationSpace.cs`
- `tests/OS.HostTests/PciEcamConfigurationSpaceTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step156.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build *> .\artifacts\logs\host-tests-integrated-171-176.log
.\scripts\check-naming.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun -RequireNativeAppMarkers
git diff --check
```

## Logs And Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `artifacts/logs/host-tests-integrated-171-176.log` exited with `HOST_TEST_EXIT 0` and emitted:
  - `TEST_PASS drivers.pci.ecam calculates config address`
  - `TEST_PASS drivers.pci.ecam rejects bus outside segment`
  - `TEST_PASS drivers.pci.ecam rejects invalid device function register`
  - `TEST_PASS drivers.pci.ecam rejects address overflow`
  - `TEST_PASS host.tests`
- `scripts/check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `scripts/run-qemu-smoke.ps1 -DryRun -RequireNativeAppMarkers` emitted `TEST_PASS qemu.runner.dryrun` and listed the native app marker requirements.
- `git diff --check` exited 0 with only existing LF-to-CRLF normalization warnings.

## Failures

- No implementation failures remain for this planning contract.
- The integrated host suite is slower than focused issue runs because it launches host tools through nested `dotnet run` calls.

## Deferred Work

- No MCFG parser integration is implemented in this slice.
- No live ECAM MMIO mapping or PCI configuration read/write path is implemented.
- No BAR decode, MSI/MSI-X, or virtio driver binding is implemented here.

## Evidence Boundary

This step is host-tested PCI ECAM address planning only. It proves deterministic ECAM address math and input validation. It does not prove live ACPI MCFG discovery, MMIO access, PCI enumeration, BAR decoding, virtio device binding, storage, networking, or QEMU hardware evidence.
