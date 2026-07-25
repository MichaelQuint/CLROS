# Step 364 - Virtio-net RX register-window network dispatch

## Summary

Added a host-tested RX register-window dispatch composer that consumes the
caller-owned slab-completion path, selects the completed receive buffer, and
feeds the selected buffer into `NetworkReceiveDispatcher` for ARP/IPv4 metadata.

## Why

RX slab completion proved descriptor-to-buffer selection and receive-frame
classification, but the future live receive path also needs a narrow handoff
into network payload dispatch. This slice keeps that handoff explicit and
status-coded without claiming live MMIO, interrupts, packet I/O, cloud
readiness, hosted .NET 10 readiness, ASP.NET Minimal API readiness, or Kestrel
readiness.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetReceiveRegisterWindowDispatchPlan.cs`
- `tests/OS.HostTests/VirtioNetReceiveRegisterWindowDispatchPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/drivers.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step364.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.rx\.dispatch\.windowexec|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS drivers.virtionet.rx.dispatch.windowexec dispatches arp slab completion
TEST_PASS drivers.virtionet.rx.dispatch.windowexec dispatches ipv4 slab completion
TEST_PASS drivers.virtionet.rx.dispatch.windowexec rejects failed slab completion
TEST_PASS drivers.virtionet.rx.dispatch.windowexec propagates dispatcher failure
TEST_PASS drivers.virtionet.rx.dispatch.windowexec preserves no-live-mmio boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Replace caller-owned used-ring and receive-buffer-slab snapshots with live
  completion/recycling evidence only after BAR mapping, MMIO ownership, DMA
  ownership, and real ordering primitives exist.
- Keep network dispatch as metadata only until live virtio-net RX, interrupt or
  polling integration, packet delivery, and protocol state ownership are proven.
- Do not treat this as live virtio-net packet I/O, networking readiness, cloud
  readiness, hosted .NET 10 readiness, ASP.NET Minimal API readiness, or Kestrel
  evidence.

## Evidence boundary

This proves only RX register-window completion/slab-selection/classification
composition into network receive dispatch metadata over caller-owned queue
memory and a caller-owned receive-buffer slab after successful RX
register-window readiness. It does not map BARs, perform volatile/live MMIO,
execute a real memory barrier, notify a live device, allocate or map DMA memory,
process interrupts, poll hardware, transfer packets, mutate network state, or
prove networking, cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel
readiness.
