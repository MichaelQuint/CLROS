# Step 360 - Virtio-net TX completion register-window notification provenance

## Summary

Extended the host-tested TX completion planner so it can accept successful
barrier-gated register-window TX notification execution provenance in addition
to the existing fake-sink notification provenance.

## Why

Step 359 added TX readiness composition over queue setup register-window fake
execution, caller-owned TX descriptor publication, explicit barrier metadata,
and notify register-window fake execution. Completion planning still needed a
matching provenance path so that register-window notification evidence can flow
into the used-ring reclaim boundary without claiming live MMIO or packet I/O.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetTransmitCompletionPlan.cs`
- `tests/OS.HostTests/VirtioNetTransmitCompletionPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/drivers.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step360.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.tx\.completion .*register-window|drivers\.virtionet\.tx\.completion preserves no-mmio|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS drivers.virtionet.tx.completion accepts register-window notification
TEST_PASS drivers.virtionet.tx.completion rejects failed register-window notification
TEST_PASS drivers.virtionet.tx.completion rejects wrong register-window notification kind
TEST_PASS drivers.virtionet.tx.completion rejects register-window notification queue mismatch
TEST_PASS drivers.virtionet.tx.completion rejects register-window notification index mismatch
TEST_PASS drivers.virtionet.tx.completion preserves register-window notification boundary
TEST_PASS drivers.virtionet.tx.completion preserves no-mmio boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Replace fake sink/register-window notification execution with a live
  BAR-backed volatile accessor only after BAR mapping, MMIO ownership, DMA
  ownership, and real ordering primitives are implemented and tested.
- Keep TX completion limited to one caller-owned used-ring entry until a later
  issue implements multi-completion draining, interrupt/poll integration, and
  runtime descriptor allocator recycling.
- Do not treat this as live virtio-net packet I/O, cloud readiness, hosted .NET
  10 readiness, ASP.NET Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only TX completion planning over caller-owned queue memory after
successful fake-sink or fake register-window notification provenance. It does
not map BARs, perform volatile/live MMIO, execute a real memory barrier, notify
a live device, allocate or map DMA memory, process interrupts, poll hardware,
transfer packets, or prove cloud/hypervisor readiness.
