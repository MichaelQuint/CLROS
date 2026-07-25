# Step 531 - Virtio-Net Queue Notification Default Result Guard

## Summary

Added explicit initialization metadata to virtio-net queue notification result
structs so default/zero values do not report success.

## Why

The queue notification status enums use `Success = 0`. Default result values
could therefore pass `Succeeded` before planner, register-window execution,
barrier-gated execution, fake-sink execution, or fake register-read paths had
actually initialized evidence.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetQueueNotificationPlan.cs`
- `tests/OS.HostTests/VirtioNetQueueNotificationPlanTests.cs`
- `tests/OS.HostTests/VirtioNetTransmitCompletionPlanTests.cs`
- `docs/drivers.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step531.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.queue\.notify rejects default results|TEST_PASS host\.tests|TEST_FAIL'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START drivers.virtionet.queue.notify rejects default results
TEST_PASS drivers.virtionet.queue.notify rejects default results
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
ExitCode: 0; existing LF-to-CRLF working-copy warnings only.
```

## Failures

- Initial host test run after the production change passed the new marker but
  failed two TX completion tests whose reflection helpers expected the older
  private notification result constructor shape. The helpers were updated to
  preserve `IsInitialized` in synthesized mismatch cases.
- Claude Code CLI still reports `Not logged in - Please run /login`; the Claude
  MCP agent registry was present but had no runnable default agent type. Built-in
  read-only explorer subagents were used instead for future-slice discovery.

## Deferred work

- This is host-tested virtio-net notification metadata only. It does not execute
  live MMIO, real memory barriers, BAR mapping, DMA allocation/mapping, packet
  I/O, interrupts, live device notification, ASP.NET Minimal API/Kestrel, cloud
  readiness, or production readiness.
