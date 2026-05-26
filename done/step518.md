# Step 518 - Virtio-net queue MMIO default guards

## Summary

Implemented Gitea issue #503 by making the virtio-net queue MMIO setup planner,
fake accessor executor, register-window planner, and register-window fake
executor result structs reject default/zero values as successful evidence.

## Why

The queue setup status enums use zero-valued success members. The result structs
previously reported success for default values even though no setup plan,
register access, register-window access planning, or fake byte-span execution had
happened. Queue setup evidence must only become successful after explicit
planner or executor initialization.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetQueueMmioSetupPlan.cs`
- `tests/OS.HostTests/VirtioNetQueueMmioSetupPlanTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step518.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.virtionet.queue.mmio default results are not success`
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

TEST_START drivers.virtionet.queue.mmio default results are not success
TEST_PASS drivers.virtionet.queue.mmio default results are not success
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

- This is host-tested virtio-net queue setup metadata, fake accessor execution,
  register-window access planning, and fake byte-span execution evidence only.
  It does not map BARs, perform live MMIO, dereference physical addresses,
  execute real memory barriers, configure a live queue, process interrupts,
  transfer packets, support ASP.NET Minimal API/Kestrel, prove cloud readiness,
  or prove production readiness.

