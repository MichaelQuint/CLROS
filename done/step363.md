# Step 363 - Virtio-net RX register-window slab selection

## Summary

Added a host-tested RX register-window slab-completion composer that consumes a
successful RX register-window readiness result, reads one caller-owned used-ring
completion, selects the completed buffer from a caller-owned receive-buffer
slab, and then delegates to the existing receive completion/classification path.

## Why

The previous RX completion slice could classify a caller-provided receive
buffer, but a future live receive path needs an explicit boundary that selects
the completed buffer from the caller-owned RX slab before classification. This
keeps descriptor-to-buffer ownership visible without claiming live MMIO,
interrupts, packet I/O, cloud readiness, hosted .NET 10 readiness, ASP.NET
Minimal API readiness, or Kestrel readiness.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetReceiveRegisterWindowSlabCompletionPlan.cs`
- `tests/OS.HostTests/VirtioNetReceiveRegisterWindowSlabCompletionPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/drivers.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step363.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.rx\.completion\.slab\.windowexec|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS drivers.virtionet.rx.completion.slab.windowexec selects completed buffer
TEST_PASS drivers.virtionet.rx.completion.slab.windowexec rejects failed readiness
TEST_PASS drivers.virtionet.rx.completion.slab.windowexec rejects no completion
TEST_PASS drivers.virtionet.rx.completion.slab.windowexec rejects short slab
TEST_PASS drivers.virtionet.rx.completion.slab.windowexec propagates classifier failure
TEST_PASS drivers.virtionet.rx.completion.slab.windowexec preserves no-live-mmio boundary
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
- Keep RX completion limited to one caller-owned used-ring entry until a later
  issue implements multi-completion draining, interrupt/poll integration, and
  runtime receive-buffer recycling.
- Do not treat this as live virtio-net packet I/O, networking readiness, cloud
  readiness, hosted .NET 10 readiness, ASP.NET Minimal API readiness, or Kestrel
  evidence.

## Evidence boundary

This proves only RX register-window completion/slab-selection/classification
composition over caller-owned queue memory and a caller-owned receive-buffer
slab after successful RX register-window readiness. It does not map BARs,
perform volatile/live MMIO, execute a real memory barrier, notify a live device,
allocate or map DMA memory, process interrupts, poll hardware, transfer
packets, or prove networking, cloud, hosted .NET 10, ASP.NET Minimal API, or
Kestrel readiness.
