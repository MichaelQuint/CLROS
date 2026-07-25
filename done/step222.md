# Step 222 - ENA Admin Descriptor Helpers

## Summary

Added host-testable ENA admin descriptor constants and caller-buffer helpers for
issue #244. The slice can write a `GET_FEATURE DEVICE_ATTRIBUTES` admin command
into a 64-byte Admin Queue entry and parse a 64-byte Admin Completion Queue
entry header/status.

## Why

The ENA driver track needs deterministic descriptor layout code before any live
Admin Queue memory ownership, BAR programming, doorbell, polling, or interrupt
work. This step keeps the boundary to byte-layout helpers over caller-owned
spans.

## Files changed

- `src/OS.Kernel/Drivers/EnaAdminDescriptors.cs`
- `tests/OS.HostTests/EnaAdminDescriptorsTests.cs`
- `docs/ena-driver-plan.md`
- `docs/drivers.md`
- `done/step222.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- Build completed with `Build succeeded`, `0 Warning(s)`, and `0 Error(s)`.
- Host test markers:
  - `TEST_PASS drivers.ena.admin.desc exposes descriptor size constants`
  - `TEST_PASS drivers.ena.admin.desc writes get feature device attributes`
  - `TEST_PASS drivers.ena.admin.desc parses successful completion`
  - `TEST_PASS drivers.ena.admin.desc reports command id mismatch`
  - `TEST_PASS drivers.ena.admin.desc reports completion status`
  - `TEST_PASS drivers.ena.admin.desc rejects short buffers`
  - `TEST_PASS drivers.ena.admin.desc preserves phase status bits`
- Full host-test run ended with `TEST_PASS host.tests`.
- `git diff --check` completed successfully with line-ending normalization
  warnings only.

## Failures

None in the final verification run.

## Deferred work

- No ENA PCI discovery change is made in this step.
- No ENA admin queue memory planner, DMA allocation, BAR/MMIO write, doorbell,
  completion polling, interrupt, or live admin command execution is implemented.
- Device-attributes payload parsing beyond the ACQ common header remains a
  later slice.
- No ENA TX/RX queues, Ethernet frame I/O, DHCP, TCP, hosted .NET 10, or
  Kestrel evidence is claimed.
