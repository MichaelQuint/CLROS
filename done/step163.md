# Step 163 - Virtio-net RX/TX Queue Planning Contract

## What Changed

- Added `OS.Drivers.VirtioNetQueuePlan` as a pure planning contract for future virtio-net RX/TX queues.
- Added host tests for RX/TX queue acceptance, descriptor access direction, insufficient queue rejection, and invalid frame buffer rejection.
- Updated networking, driver, and completion-gate docs to describe the contract and its evidence boundary.

## Why

Gitea issue #182 requires the next virtio-net slice before live packet I/O: model queue and buffer requirements without allocating DMA memory, publishing descriptors, notifying a device, or parsing virtio-net headers.

The type lives under `OS.Drivers` because it composes the generic `VirtqueueSplitRingPlan` and records device descriptor access direction. `OS.Net` remains focused on protocol parsers, packet encoders, and mock network state machines.

## Files Changed

- `src/OS.Kernel/Drivers/VirtioNetQueuePlan.cs`
- `tests/OS.HostTests/VirtioNetQueuePlanTests.cs`
- `docs/networking.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step163.md`

## Implemented Behavior

- Requires at least two available queues: receive queue index 0 and transmit queue index 1.
- Validates RX and TX split-ring queue sizes through `VirtqueueSplitRingPlan`.
- Bounds caller-owned frame buffers to a virtio-net header plus Ethernet frame without FCS.
- Records RX buffers as device-writable descriptors.
- Records TX virtio-net header and Ethernet frame buffers as device-readable descriptors.
- Computes required bytes for configured RX buffers and one TX frame plan.
- Exposes explicit booleans that this contract does not allocate buffers, publish descriptors, notify the device, or parse virtio-net headers.

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build *> .\artifacts\logs\host-tests-issue182.log
```

Build evidence:

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` passed with 0 warnings and 0 errors after a parallel host-test process released the shared test output.

Observed issue #182 markers:

```text
TEST_PASS drivers.virtionet queue plan accepts rx tx queues
TEST_PASS drivers.virtionet queue plan marks rx writable and tx readable
TEST_PASS drivers.virtionet queue plan rejects insufficient queues
TEST_PASS drivers.virtionet queue plan rejects invalid frame size
```

## Failures

- The first host-test build attempt failed because a parallel `OS.HostTests.exe` process held `tests/OS.HostTests/bin/Debug/net10.0/OS.Kernel.dll`. The same build passed after that process exited.
- A temporary isolated-output build attempt failed because the shared alternate intermediate path caused duplicate generated assembly attributes in `OS.QemuRunner`. This was not used as evidence.
- The full `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted all issue #182 markers, then exited nonzero later in an unrelated existing/parallel capability-parser test: `TEST_FAIL drivers.virtiopci.capability parser rejects truncated capability ArgumentOutOfRangeException`. No full `TEST_PASS host.tests` evidence was produced in this run.

## Deferred Work

- Map real virtio-net PCI capability metadata and live feature negotiation into this plan.
- Allocate DMA-safe RX/TX buffers only after memory ownership and page-permission rules are ready.
- Publish descriptors, notify the device, and handle interrupts in a later live virtio-net issue.
- Wire received/transmitted Ethernet frames into ARP, IPv4, DHCP, DNS, TCP, HTTP, and Kestrel prerequisites only after live packet I/O exists.

## Evidence Boundary

This step is host-tested planning evidence only. It does not prove live PCI enumeration, BAR mapping, virtio feature negotiation, DMA-safe memory allocation, descriptor publication, device notification, interrupt handling, Ethernet packet transfer, DHCP/DNS/TCP/HTTP behavior, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
