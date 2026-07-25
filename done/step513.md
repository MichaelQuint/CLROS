# Step 513 - PCI virtio descriptor catalog planner default guard

## Summary

Implemented Gitea issue #498 by making
`PciVirtioDescriptorCatalogPlanResult` reject default/zero values as successful
PCI-to-virtio descriptor catalog planning evidence.

## Why

`PciVirtioDescriptorCatalogPlanStatus.Success` is zero. The result struct
previously reported success for default values even though no ECAM scan,
candidate selection, fake-derived BAR-size binding catalog, feature-source
composition, or descriptor cataloging had run. Planner evidence must only be
successful after an explicit factory-created result.

## Files changed

- `src/OS.Kernel/Drivers/PciVirtioDescriptorCatalogPlanner.cs`
- `tests/OS.HostTests/PciVirtioDescriptorCatalogPlannerTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step513.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.pci virtio catalog default result is not success` and
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

TEST_START drivers.pci virtio catalog default result is not success
TEST_PASS drivers.pci virtio catalog default result is not success
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

- This is host-tested PCI/virtio metadata evidence only. It does not enumerate
  PCI live, access configuration space live, map ECAM or BARs, write MMIO,
  configure queues, allocate or map DMA, transfer packets, read sectors,
  support ASP.NET Minimal API/Kestrel, prove cloud readiness, or prove
  production readiness.

