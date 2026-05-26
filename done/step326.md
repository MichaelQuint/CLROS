# Step 326 - Virtio-net Queue Notification Planner

## Summary

Added `OS.Drivers.VirtioNetQueueNotificationPlanner`, a host-tested coordinator that composes successful virtio-net queue MMIO setup metadata with successful RX/TX descriptor publication results and produces a future virtio PCI notify write plan.

## Why

The networking backlog needed a small bridge between queue setup/publish evidence and the later live-device notify path. This keeps notification readiness status-coded and testable without writing MMIO, executing memory barriers, processing interrupts, reclaiming descriptors, moving packets, or claiming live virtio-net support.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetQueueNotificationPlan.cs`
- `tests/OS.HostTests/VirtioNetQueueNotificationPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/networking.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step326.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`
- `rg --files src std apps | Where-Object { $_ -match '\.(c|cpp|h|asm|s)$' }`

## Evidence

- Host test project build passed with 0 warnings and 0 errors.
- Full host harness passed and emitted:

```text
TEST_PASS drivers.virtionet.queue.notify plans rx notification
TEST_PASS drivers.virtionet.queue.notify plans tx notification
TEST_PASS drivers.virtionet.queue.notify rejects failed setup
TEST_PASS drivers.virtionet.queue.notify rejects failed publish
TEST_PASS drivers.virtionet.queue.notify propagates notify failure
TEST_PASS drivers.virtionet.queue.notify preserves no-mmio boundary
TEST_PASS host.tests
```

- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` exited 0. Git reported existing LF-to-CRLF working-copy warnings only.
- Forbidden first-party source scan produced no `.c`, `.cpp`, `.h`, `.asm`, or `.s` paths under `src`, `std`, or `apps`.

## Failures

- The first host harness run failed because the test fixture used `VirtioNetQueuePlan.MaximumFrameBufferBytes` for RX publication, while `VirtioNetReceiveLifecycle` correctly accepts only no-mergeable-buffer RX frames up to `MaximumReceiveBufferBytes`. The fixture was corrected to use the receive lifecycle limit, and the host harness then passed.
- Gitea issue updates are still blocked by missing write credentials in the current task context; API write attempts return `token is required`.

## Deferred work

- Execute the notify plan through a fake MMIO accessor with explicit memory-barrier ordering.
- Add a live BAR-backed accessor and QEMU evidence before claiming real device notification.
- Add ISR/interrupt parsing, TX used-ring completion and descriptor reclaim, RX used-ring consumption, Ethernet frame delivery, and real packet I/O.
