# Step 349 - Virtio-net TX readiness fake-accessor composition

## Summary

Added a host-tested `VirtioNetTransmitReadinessPlanner` that composes the
current fake-accessor virtio-net TX path from queue setup execution through
descriptor publication, explicit barrier-readiness metadata, notification
planning, and fake register-accessor-backed notification execution.

## Why

Issue #334 needed one small readiness boundary before live BAR-backed MMIO work.
The boundary proves the current TX pieces can be sequenced without publishing
descriptors after failed setup, notifying after failed publication, or writing a
fake notification before barrier readiness is explicit.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetTransmitReadinessPlan.cs`
- `tests/OS.HostTests/VirtioNetTransmitDescriptorOwnershipPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/networking.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step349.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.tx\.readiness|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- `TEST_PASS drivers.virtionet.tx.readiness composes setup publish notify`
- `TEST_PASS drivers.virtionet.tx.readiness rejects failed setup execution`
- `TEST_PASS drivers.virtionet.tx.readiness rejects failed publish`
- `TEST_PASS drivers.virtionet.tx.readiness rejects missing barrier`
- `TEST_PASS drivers.virtionet.tx.readiness propagates notification failure`
- `TEST_PASS drivers.virtionet.tx.readiness preserves no-live-mmio boundary`
- `TEST_PASS host.tests`
- Host test build passed.
- Full solution build passed.
- `git diff --check` passed.

## Failures

- None.

## Deferred work

- Replace the fake register accessor with a live BAR-backed volatile accessor.
- Replace explicit barrier-readiness metadata with a real kernel-safe ordering
  primitive.
- Add DMA ownership/allocation rules before live virtio-net packet I/O.
- Prove one live notification/completion path under QEMU before claiming live
  virtio-net TX.

## Evidence boundary

This is host-tested fake-accessor TX readiness composition evidence only. It
composes successful queue setup execution, caller-owned TX descriptor
publication, explicit barrier-readiness metadata, notification planning, and
fake-accessor-backed notification execution. It does not map BARs, perform
volatile or live MMIO, execute real memory barriers, notify a live device,
allocate or map DMA memory, process interrupts, poll hardware, transfer packets,
or prove networking, cloud, hosted .NET 10, ASP.NET Minimal API, Kestrel, or
production readiness.
