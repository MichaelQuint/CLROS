# Step 515 - Virtio PCI queue notification default guard

## Summary

Implemented Gitea issue #500 by making
`VirtioPciQueueNotificationPlanResult` reject default/zero values as successful
queue notification planning evidence.

## Why

`VirtioPciQueueNotificationPlanStatus.Success` is zero. The result struct
previously reported success for default values even though no notify aperture
validation, queue precondition check, feature check, or notify-offset range
check had run. Notification planning evidence must only be successful after
explicit planner initialization.

## Files changed

- `src/OS.Kernel/Drivers/VirtioPciQueueNotificationPlan.cs`
- `tests/OS.HostTests/VirtioPciQueueNotificationPlanTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step515.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.virtiopci.queue.notify default result is not success`
  and `TEST_PASS host.tests`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START drivers.virtiopci.queue.notify default result is not success
TEST_PASS drivers.virtiopci.queue.notify default result is not success
TEST_PASS host.tests

dotnet build .\OS.slnx --nologo -m:1
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall

git diff --check
exit 0; existing LF-to-CRLF working-copy warnings only
```

## Failures

- None.

## Deferred work

- This is host-tested virtio PCI queue notification metadata evidence only. It
  does not publish descriptors, execute live MMIO writes, process interrupts,
  transfer packets, support ASP.NET Minimal API/Kestrel, prove cloud readiness,
  or prove production readiness.

