# Step 203 - Virtio-net Driver Queue Bring-up Plan

## What Changed

- Added `OS.Drivers.VirtioNetDriverQueueBringUpPlan`, a host-testable metadata boundary between the virtio device descriptor catalog and future live virtio-net queue setup.
- Added status-coded validation for descriptor binding, network descriptor kind, network MAC/status configuration, initialization profile, selected feature mask, RX/TX queue sizes, RX/TX split-ring memory capacity, RX/TX buffer capacity, and byte-count overflow.
- Added `VirtioNetDriverQueueBringUpMetadata` so later driver code can carry MAC address, queue indexes, queue sizes, required descriptor counts, required memory bytes, available memory bytes, selected features, and publish-readiness flags without touching hardware.
- Added host tests with `drivers.virtionet.queue.bringup` markers.
- Updated `docs/drivers.md` with the new bring-up boundary, markers, limitations, and next integration step.

## Why It Changed

Virtio-net already has separate host-tested slices for descriptor/catalog metadata, RX/TX queue planning, TX publication, and RX buffer publication/completion. The missing boundary was the driver-level readiness check that consumes a selected network descriptor and verifies that caller-owned ring and frame buffers are large enough before live queue MMIO work begins.

## Files Changed

- `src/OS.Kernel/Drivers/VirtioNetDriverQueueBringUpPlan.cs`
- `tests/OS.HostTests/VirtioNetDriverQueueBringUpPlanTests.cs`
- `docs/drivers.md`
- `done/step203.md`

## Tests Run

```text
dotnet build .\OS.slnx --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo --no-restore -m:1 /p:UseAppHost=false -o .\artifacts\tmp\issue221-hosttests
dotnet .\artifacts\tmp\issue221-hosttests\OS.HostTests.dll *> .\artifacts\tmp\issue221-hosttests\host-tests.log
dotnet run --project .\artifacts\tmp\issue221-focus\Issue221Focus.csproj --nologo
```

## Evidence

The normal solution build reached project compilation but failed when copying the host-test apphost because another `OS.HostTests.exe` process held the normal output file. The isolated no-apphost host-test build succeeded:

```text
OS.HostTests -> C:\Projects\OS\artifacts\tmp\issue221-hosttests\OS.HostTests.dll
Build succeeded.
0 Warning(s)
0 Error(s)
```

The temporary focus runner compiled only the issue #221 host-test file against `OS.Kernel` and emitted all new markers:

```text
TEST_PASS drivers.virtionet.queue.bringup creates publish ready plan
TEST_PASS drivers.virtionet.queue.bringup rejects non network descriptor
TEST_PASS drivers.virtionet.queue.bringup rejects invalid queue plan
TEST_PASS drivers.virtionet.queue.bringup rejects split ring memory
TEST_PASS drivers.virtionet.queue.bringup rejects buffer capacities
TEST_PASS drivers.virtionet.queue.bringup rejects byte count overflow
```

## Failures

- `dotnet build .\OS.slnx --nologo -m:1` failed because `tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe` was locked by an existing `OS.HostTests` process. The isolated no-apphost host-test build succeeded.
- Running the full host-test assembly from the isolated output path exited nonzero in unrelated image-builder tests before the issue #221 module initializer ran. This appears tied to tests that expect the normal test binary layout and is not evidence against the queue bring-up planner.

## Deferred Work

- No MMIO common-configuration queue registers are written.
- No device queue selection or queue enabling is performed.
- No DMA-safe page allocation or physical mapping exists.
- No queue notification, interrupt handling, polling loop, RX recycling, or packet I/O is implemented.
- No QEMU, libvirt, or live virtio-net proof is claimed.

## Evidence Boundary

This step proves only host-testable metadata validation from a virtio-net descriptor/catalog entry to RX/TX queue publish readiness over caller-owned capacities. It is not evidence of live PCI BAR mapping, virtio-net MMIO setup, real packet transmission or receive, DHCP/DNS/TCP/HTTP traffic, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
