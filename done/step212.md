# Step 212 - Virtio PCI Modern Register Map Constants

## Summary
- Added a host-testable modern virtio PCI register-map contract for common configuration, device status, ISR status, no-notification-data notify width, and virtio-net device configuration.
- Tied the discovery common-configuration minimum length to the first live queue setup register range through `queue_device`.
- Added host markers for the register map and updated the virtio-net MMIO boundary status doc.

## Why
Issue #230 needs constants/validation only before a later transport-owned MMIO accessor can safely plan queue setup and notification. The previous common capability minimum was `0x34`, which did not cover `queue_device` at `0x30` with its 8-byte width.

## Files changed
- `src/OS.Kernel/Drivers/VirtioPciModernRegisterMap.cs`
- `src/OS.Kernel/Drivers/VirtioPciDeviceDiscovery.cs`
- `tests/OS.HostTests/VirtioPciModernRegisterMapTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `done/step212.md`

## Tests run
- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence
- Build succeeded with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS host.tests`.
- Naming guard emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited successfully; it reported existing LF-to-CRLF working-copy warnings only.
- New host markers included:
  - `TEST_PASS drivers.virtiopci.registermap common offsets`
  - `TEST_PASS drivers.virtiopci.registermap status bits`
  - `TEST_PASS drivers.virtiopci.registermap isr and notify bits`
  - `TEST_PASS drivers.virtiopci.registermap validates first live common length`
  - `TEST_PASS drivers.virtiopci.registermap net config offsets`

## Failures
- None.

## Deferred work
- No BAR mapping, volatile MMIO reads/writes, device notification, packet I/O, live QEMU, hosted .NET 10, Minimal API, or Kestrel evidence was added.
- Queue setup command planning, notify planning, and ISR read planning remain follow-up work under the parent integration.

