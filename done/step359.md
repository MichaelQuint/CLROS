# Step 359 - Virtio-net TX readiness register-window fake execution

## Summary

Added a host-tested TX readiness composition layer that combines queue setup
register-window fake execution, caller-owned TX descriptor publication and
state marking, explicit barrier-readiness metadata, TX notification planning,
and barrier-gated notify register-window fake execution.

## Why

TX readiness already had a fake register-accessor composition boundary. The
register-window path needed an equivalent status-coded composition boundary so a
future live BAR-backed accessor can replace one fake byte-span sequence without
changing TX publication or notification planning contracts.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetTransmitRegisterWindowReadinessPlan.cs`
- `tests/OS.HostTests/VirtioNetTransmitRegisterWindowReadinessPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/drivers.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step359.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.tx\.readiness\.windowexec|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS drivers.virtionet.tx.readiness.windowexec composes setup publish notify
TEST_PASS drivers.virtionet.tx.readiness.windowexec rejects failed setup window execution
TEST_PASS drivers.virtionet.tx.readiness.windowexec rejects failed publish
TEST_PASS drivers.virtionet.tx.readiness.windowexec rejects missing barrier
TEST_PASS drivers.virtionet.tx.readiness.windowexec propagates notification plan failure
TEST_PASS drivers.virtionet.tx.readiness.windowexec propagates notification window failure
TEST_PASS drivers.virtionet.tx.readiness.windowexec preserves no-live-mmio boundary
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
- Keep TX completion gated on the existing executed-notification provenance
  until a live/register-window completion provenance issue defines and tests the
  handoff.
- Do not treat this as live virtio-net packet I/O, cloud readiness, hosted .NET
  10 readiness, ASP.NET Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only TX readiness composition over caller-owned queue memory,
caller-owned TX header storage, caller-owned descriptor ownership state,
caller-owned common BAR-window bytes, caller-owned notify BAR-window bytes, and
explicit barrier-readiness metadata. It does not map BARs, perform volatile/live
MMIO, execute a real memory barrier, notify a live device, allocate or map DMA
memory, process interrupts, poll hardware, wait for completion, transfer
packets, or prove cloud/hypervisor readiness.
