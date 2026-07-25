# Step 219 - Virtio PCI Queue Notification Planner

## Summary

Added a host-tested virtio PCI queue notification address/value planner for
issue #232. The planner computes the notify BAR relative offset, physical
address, write width, and queue write value for a selected queue after
descriptors have been published by a higher layer.

## Why

Virtio queue notification must be represented separately from descriptor
publication and interrupt completion. This keeps the current driver evidence
bounded to deterministic metadata and prevents future code from treating a plan
as proof that a live device was notified.

## Files changed

- `src/OS.Kernel/Drivers/VirtioPciQueueNotificationPlan.cs`
- `tests/OS.HostTests/VirtioPciQueueNotificationPlanTests.cs`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\check-naming.ps1`
- `.\scripts\run-qemu-smoke.ps1 -DryRun -RequireNativeAppMarkers`
- `git diff --check`

## Evidence

- Host test markers:
  - `TEST_PASS drivers.virtiopci.queue.notify creates no data write plan`
  - `TEST_PASS drivers.virtiopci.queue.notify rejects queue preconditions`
  - `TEST_PASS drivers.virtiopci.queue.notify rejects notification data features`
  - `TEST_PASS drivers.virtiopci.queue.notify rejects notify offset overflow`
  - `TEST_PASS drivers.virtiopci.queue.notify rejects notify range outside aperture`
  - `TEST_PASS drivers.virtiopci.queue.notify rejects invalid notify aperture`
- Checkpoint logs:
  - `artifacts/logs/checkpoint-build-20260522-120158.log`
  - `artifacts/logs/checkpoint-host-tests-20260522-120158.log`
  - `artifacts/logs/checkpoint-guards-20260522-120158.log`

## Failures

None in the final checkpoint run. `git diff --check` emitted only line-ending
normalization warnings and returned exit code 0.

## Deferred work

- No live notify BAR write is executed.
- Notification-data and notification-config-data feature paths remain
  unsupported.
- No interrupt, used-ring completion, retry, timeout, or packet I/O behavior is
  implemented.

