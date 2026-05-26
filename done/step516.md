# Step 516 - Virtio PCI binding plan default guard

## Summary

Implemented Gitea issue #501 by making `VirtioPciBindingPlanResult` reject
default/zero values as successful direct virtio PCI binding evidence.

## Why

`VirtioPciBindingPlanStatus.Success` is zero. The result struct previously
reported success for default values even though no PCI header parse, virtio
capability parse, device discovery, BAR decode, or aperture validation had run.
Direct binding evidence must only be successful after explicit planner
initialization.

## Files changed

- `src/OS.Kernel/Drivers/VirtioPciBindingPlan.cs`
- `tests/OS.HostTests/VirtioPciBindingPlanTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step516.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.virtio.pci.binding default result is not success` and
  `TEST_PASS host.tests`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START drivers.virtio.pci.binding default result is not success
TEST_PASS drivers.virtio.pci.binding default result is not success
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

- This is host-tested virtio PCI binding metadata evidence only. It does not
  perform live config reads, BAR sizing writes, MMIO mapping, feature
  negotiation, queue setup, driver attach, device I/O, ASP.NET Minimal
  API/Kestrel support, cloud readiness, or production readiness.

