# Step 545 - Virtio-net queue bring-up default-result guard

## Summary

Added an explicit initialized-state guard to the virtio-net driver queue
bring-up result so a default value is not treated as success when the status
enum defaults to zero.

## Why

The queue bring-up result feeds the virtio-net queue MMIO setup planner. A
zero/default struct should not expose publish-ready queue metadata or satisfy
`Succeeded`, because that would let downstream code trust a result that was
never produced by the planner.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetDriverQueueBringUpPlan.cs`
- `tests/OS.HostTests/VirtioNetDriverQueueBringUpPlanTests.cs`
- `tests/OS.HostTests/VirtioNetQueueMmioSetupPlanTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step545.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to the queue bring-up and queue-MMIO rejection markers.
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```text
TEST_START drivers.virtionet.queue.bringup creates publish ready plan
TEST_PASS drivers.virtionet.queue.bringup creates publish ready plan
TEST_START drivers.virtionet.queue.bringup rejects non network descriptor
TEST_PASS drivers.virtionet.queue.bringup rejects non network descriptor
TEST_START drivers.virtionet.queue.bringup rejects invalid queue plan
TEST_PASS drivers.virtionet.queue.bringup rejects invalid queue plan
TEST_START drivers.virtionet.queue.bringup rejects split ring memory
TEST_PASS drivers.virtionet.queue.bringup rejects split ring memory
TEST_START drivers.virtionet.queue.bringup rejects buffer capacities
TEST_PASS drivers.virtionet.queue.bringup rejects buffer capacities
TEST_START drivers.virtionet.queue.bringup rejects byte count overflow
TEST_PASS drivers.virtionet.queue.bringup rejects byte count overflow
TEST_START drivers.virtionet.queue.bringup default result is not success
TEST_PASS drivers.virtionet.queue.bringup default result is not success
TEST_START drivers.virtionet.queue.mmio rejects failed bringup
TEST_PASS drivers.virtionet.queue.mmio rejects failed bringup
TEST_PASS host.tests
```

```text
dotnet build .\OS.slnx --nologo -m:1
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

```text
git diff --check
ExitCode: 0; existing LF-to-CRLF working-copy warnings only.
```

## Failures

- None.

## Deferred work

- This is host-tested planner/result safety only. It does not prove live MMIO,
  queue publication to a device, descriptor DMA ownership, packet I/O,
  interrupts, cloud readiness, Kestrel support, or production readiness.

