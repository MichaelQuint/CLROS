# Step 517 - Virtio PCI register-window default guards

## Summary

Implemented Gitea issue #502 by making the virtio PCI register-window binding,
access-plan, and fake access-execution results reject default/zero values as
successful evidence.

## Why

The register-window plan and fake-execution status enums use zero-valued
success members. The result structs previously reported success for default
values even though no BAR-window binding, range validation, access planning, or
fake byte-span execution had happened. Register-window evidence must only be
successful after explicit planner or executor initialization.

## Files changed

- `src/OS.Kernel/Drivers/VirtioPciBarRegisterWindowPlan.cs`
- `tests/OS.HostTests/VirtioPciBarRegisterWindowPlanTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step517.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.virtiopci.registerwindow default results are not success`
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

TEST_START drivers.virtiopci.registerwindow default results are not success
TEST_PASS drivers.virtiopci.registerwindow default results are not success
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

- This is host-tested virtio PCI register-window metadata and fake byte-span
  execution evidence only. It does not map BARs, perform live MMIO, dereference
  physical addresses, execute memory barriers, configure queues, process
  interrupts, transfer packets, support ASP.NET Minimal API/Kestrel, prove cloud
  readiness, or prove production readiness.

