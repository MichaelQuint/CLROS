# Step 211 - Virtio PCI INTx ISR Status Planning

## Summary

- Added a host-tested virtio PCI ISR status parser and read-plan contract for the INTx boundary.
- Modeled ISR reads as exactly one byte at relative offset `0` of the validated ISR aperture.
- Parsed bit `0` as virtqueue progress, bit `1` as device configuration change, and preserved bits `2..7` as reserved diagnostics.
- Documented that the INTx ISR read is destructive and clears device status.

## Why

Issue #233 needed the ISR boundary represented before live interrupt integration. This keeps interrupt handling host-testable and status-coded while avoiding live MMIO, MSI-X, APIC/IOAPIC setup, queue draining, and packet dispatch.

## Files changed

- `src/OS.Kernel/Drivers/VirtioPciIsrStatus.cs`
- `tests/OS.HostTests/VirtioPciIsrStatusTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `done/step211.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence

- Managed solution build passed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS host.tests`.
- New ISR markers emitted:
  - `TEST_PASS drivers.virtiopci.isr parses queue interrupt`
  - `TEST_PASS drivers.virtiopci.isr parses config change`
  - `TEST_PASS drivers.virtiopci.isr preserves reserved bits`
  - `TEST_PASS drivers.virtiopci.isr plans one byte read`

## Failures

None observed.

## Deferred work

- Live volatile MMIO register reads.
- APIC/IOAPIC or MSI-X configuration.
- Virtqueue draining after queue-progress status.
- Device-configuration reread stabilization after config-change status.
- Packet dispatch from completed virtio-net receive buffers.

## Evidence boundary

This is INTx ISR planning evidence only. It does not perform a live ISR read, handle a real interrupt, clear a real device status byte, configure MSI-X, configure APIC/IOAPIC, drain queues, dispatch packets, run QEMU, launch hosted .NET 10, run ASP.NET Minimal APIs, or prove Kestrel support.

