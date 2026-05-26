# Step 361 - Virtio-net TX register-window completion reclaim composition

## Summary

Added a host-tested TX register-window reclaim composer that consumes a
successful register-window TX readiness result, validates one caller-owned
used-ring completion through the existing completion planner, and applies
descriptor ownership reclaim through the existing caller-owned state table.

## Why

Step 360 allowed TX completion planning to accept register-window notification
provenance. The next narrow boundary was to compose that completion evidence
with descriptor-state reclaim without introducing live MMIO, polling, interrupt
handling, DMA ownership, or a runtime descriptor allocator.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetTransmitRegisterWindowReclaimPlan.cs`
- `tests/OS.HostTests/VirtioNetTransmitRegisterWindowReclaimPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/drivers.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step361.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.tx\.reclaim\.windowexec|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS drivers.virtionet.tx.reclaim.windowexec composes readiness completion ownership
TEST_PASS drivers.virtionet.tx.reclaim.windowexec rejects failed readiness
TEST_PASS drivers.virtionet.tx.reclaim.windowexec rejects no completion
TEST_PASS drivers.virtionet.tx.reclaim.windowexec rejects ownership mismatch
TEST_PASS drivers.virtionet.tx.reclaim.windowexec preserves no-live-mmio boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Replace fake register-window notification and caller-owned used-ring snapshots
  with live BAR-backed notification and completion evidence only after BAR
  mapping, MMIO ownership, DMA ownership, and real ordering primitives exist.
- Keep reclaim limited to one validated TX used-ring entry until a later issue
  implements multi-completion draining, interrupt/poll integration, and runtime
  descriptor allocator recycling.
- Do not treat this as live virtio-net packet I/O, cloud readiness, hosted .NET
  10 readiness, ASP.NET Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only TX completion/reclaim composition over caller-owned queue
memory and caller-owned descriptor-state spans after successful register-window
readiness. It does not map BARs, perform volatile/live MMIO, execute a real
memory barrier, notify a live device, allocate or map DMA memory, process
interrupts, poll hardware, transfer packets, or prove cloud/hypervisor
readiness.
