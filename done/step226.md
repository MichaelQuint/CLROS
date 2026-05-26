# Step 226 - ENA Device Attributes Parser

## Summary

Added `EnaDeviceAttributesParser`, a host-tested parser for caller-provided ENA
`GET_FEATURE DEVICE_ATTRIBUTES` response bytes.

## Why

Issue #246 needed the MAC/config discovery boundary before live ENA admin queue
submission. The parser proves the byte layout and validation contract for the
device-attributes response without allocating DMA memory, writing BARs, ringing
doorbells, polling completions, or claiming hardware MAC retrieval.

## Files changed

- `src/OS.Kernel/Drivers/EnaDeviceAttributes.cs`
- `tests/OS.HostTests/EnaDeviceAttributesTests.cs`
- `docs/drivers.md`
- `docs/ena-driver-plan.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence

- `TEST_PASS drivers.ena.deviceattr parses mac and mtu`
- `TEST_PASS drivers.ena.deviceattr preserves address widths`
- `TEST_PASS drivers.ena.deviceattr rejects zero mac`
- `TEST_PASS drivers.ena.deviceattr rejects broadcast mac`
- `TEST_PASS drivers.ena.deviceattr reports completion failure`
- `TEST_PASS drivers.ena.deviceattr rejects malformed responses`
- `TEST_PASS host.tests`

## Failures

A first focused verification attempt ran host tests in parallel with `dotnet
build` and reproduced a file-lock copy failure on `OS.Kernel.dll`. Verification
was rerun sequentially and passed.

## Deferred work

- No live admin command is submitted.
- No ENA BAR is mapped or accessed.
- No DMA memory is allocated.
- No admin queue doorbell is rung.
- No completion is polled from hardware.
- No MAC address is retrieved from a live ENA device.
- No TX/RX queue, packet I/O, DHCP, TCP, hosted .NET, Minimal API, or Kestrel
  behavior is claimed.
