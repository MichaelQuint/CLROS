# Step 181 - Virtio PCI Binding Metadata Composition

## Summary

Implemented issue #200 as a host-test-only virtio PCI binding metadata plan.

## Why

The platform path now has separate contracts for modern virtio PCI capability parsing, virtio identity discovery, PCI BAR decoding, and BAR aperture validation. The next driver-facing step needs one deterministic composition point that can turn a scanned PCI function plus caller-owned configuration bytes and BAR sizes into a binding record for future block/network attach logic.

## Files changed

- `src/OS.Kernel/Drivers/VirtioPciBindingPlan.cs`
- `tests/OS.HostTests/VirtioPciBindingPlanTests.cs`
- `docs/drivers.md`
- `done/step181.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence

Observed markers:

- `TEST_PASS drivers.virtio.pci.binding creates block binding plan`
- `TEST_PASS drivers.virtio.pci.binding creates network binding plan`
- `TEST_PASS drivers.virtio.pci.binding reports capability parse failure`
- `TEST_PASS drivers.virtio.pci.binding reports discovery failure`
- `TEST_PASS drivers.virtio.pci.binding reports bar decode failure`
- `TEST_PASS drivers.virtio.pci.binding reports aperture validation failure`
- `TEST_PASS host.tests`

The binding plan preserves:

- ECAM segment index, segment group, ECAM base, bus range, bus, device, and function.
- PCI vendor/device identity.
- Virtio device kind and virtio device ID.
- Nested capability parser, discovery, BAR decoder, and aperture validator results.

## Failures

No failures in this slice.

## Deferred work

- No live ECAM/config-space reads.
- No BAR sizing writes or MMIO mapping.
- No feature negotiation.
- No queue setup.
- No driver attach.
- No QEMU hardware binding evidence.
