# Step 190 - Virtio Device Initialization Profiles

## What Changed

- Added `VirtioDeviceInitializationProfile` for issue #209.
- Added block and network profile constants over the generic `VirtioDeviceInitializationPlan`.
- Made the current zero-required-feature baseline explicit with `NoRequiredFeatureMask`.
- Set required queue counts to one queue for virtio-blk and two queues for virtio-net.
- Selected optional baseline feature masks only when the caller-provided device feature set advertises them.
- Preserved nested initialization-plan status for insufficient queues and `FEATURES_OK` rejection.
- Rejected unknown `VirtioPciDeviceKind` values before composing a generic initialization plan.
- Updated driver docs with the profile status, test markers, and evidence boundary.

## Why

The PCI binding path can now identify modern virtio block and network candidates and the generic virtio initialization planner can model status/feature negotiation. The next small boundary is a device-kind profile that says which queue count and baseline feature masks each supported device kind uses before real MMIO status writes, feature-register reads, queue setup, or driver attach are introduced.

## Files Changed

- `src/OS.Kernel/Drivers/VirtioDeviceInitializationProfile.cs`
- `tests/OS.HostTests/VirtioDeviceInitializationProfileTests.cs`
- `docs/drivers.md`
- `done/step190.md`

## Tests Run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Build passed with 0 warnings and 0 errors.
- Host tests passed with final `TEST_PASS host.tests`.
- Naming/source firewall passed:
  - `TEST_PASS naming.guard`
  - `TEST_PASS source.firewall`
- `git diff --check` exited 0 with CRLF normalization warnings only.

Issue #209 markers:

- `TEST_PASS drivers.virtio.profile creates block initialization plan`
- `TEST_PASS drivers.virtio.profile creates network initialization plan`
- `TEST_PASS drivers.virtio.profile rejects unknown device kind`
- `TEST_PASS drivers.virtio.profile reports insufficient queues`
- `TEST_PASS drivers.virtio.profile reports features-ok rejection`

## Failures

- No failures in this slice.

## Deferred Work

- No device reset is performed.
- No MMIO status writes are performed.
- No device feature registers are read.
- No live `FEATURES_OK` result is observed.
- No queue selection, DMA allocation, queue notification, driver attach, block I/O, network I/O, hosted .NET 10, Minimal API, or Kestrel behavior is proven by this step.

## Evidence Boundary

This is host-tested planning evidence over caller-provided device features, available queue count, and caller-provided `FEATURES_OK` result only. It proves deterministic block/network profile composition with the generic virtio initialization planner; it does not prove live virtio device initialization or QEMU hardware behavior.
