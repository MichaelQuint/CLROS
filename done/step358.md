# Step 358 - Virtio-net RX readiness register-window fake execution

## Summary

Added a host-tested RX readiness composition layer that combines queue setup
register-window fake execution, caller-owned RX descriptor publication, explicit
barrier-readiness metadata, RX notification planning, and barrier-gated notify
register-window fake execution.

## Why

The RX path needed the same small composition boundary as TX readiness, but on
the register-window fake-execution path. This gives the future live BAR accessor
a single status-coded RX sequence to replace while keeping the current evidence
strictly caller-owned and non-live.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetReceiveReadinessPlan.cs`
- `tests/OS.HostTests/VirtioNetReceiveReadinessPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/drivers.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step358.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.rx\.readiness|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS drivers.virtionet.rx.readiness composes setup publish notify windowexec
TEST_PASS drivers.virtionet.rx.readiness rejects failed setup window execution
TEST_PASS drivers.virtionet.rx.readiness rejects failed publish
TEST_PASS drivers.virtionet.rx.readiness rejects missing barrier
TEST_PASS drivers.virtionet.rx.readiness propagates notification plan failure
TEST_PASS drivers.virtionet.rx.readiness propagates notification window failure
TEST_PASS drivers.virtionet.rx.readiness preserves no-live-mmio boundary
TEST_PASS host.tests
```

Full solution build passed with 0 warnings and 0 errors. `git diff --check`
reported no whitespace errors; it only emitted LF-to-CRLF working-copy warnings
on the Windows host.

## Failures

None in the focused build, marker scan, solution build, or diff check.

## Deferred work

- Replace fake byte-span register-window setup/notification with a live
  BAR-backed volatile accessor only after BAR mapping, MMIO ownership, DMA
  ownership, and real ordering primitives are implemented and tested.
- Add live RX completion and packet-I/O evidence only after the live queue
  ownership boundary exists.
- Do not treat this as live virtio-net packet I/O, cloud readiness, hosted .NET
  10 readiness, ASP.NET Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only RX readiness composition over caller-owned queue memory,
caller-owned common BAR-window bytes, caller-owned notify BAR-window bytes, and
explicit barrier-readiness metadata. It does not map BARs, perform volatile/live
MMIO, execute a real memory barrier, notify a live device, allocate or map DMA
memory, process interrupts, poll hardware, complete receives, transfer packets,
or prove cloud/hypervisor readiness.
