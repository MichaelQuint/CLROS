# Step 507 - Virtio PCI derived BAR-size binding default guard

## Summary

Implemented Gitea issue #492 by making
`VirtioPciBindingDerivedBarSizePlanResult` reject default/zero values as
successful fake-derived virtio PCI binding evidence.

## Why

`VirtioPciBindingDerivedBarSizePlanStatus.Success` is zero. The result struct
previously reported success for default values even though no PCI configuration
bytes, decoded BAR metadata, fake-derived BAR sizes, or virtio PCI binding plan
had been composed. Derived binding evidence must only be successful after
explicit planner creation.

## Files changed

- `src/OS.Kernel/Drivers/VirtioPciBindingDerivedBarSizePlan.cs`
- `tests/OS.HostTests/VirtioPciBindingDerivedBarSizePlanTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step507.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.virtio.pci.binding.derive default result is not success`
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

TEST_START drivers.virtio.pci.binding.derive default result is not success
TEST_PASS drivers.virtio.pci.binding.derive default result is not success
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

- This is host-tested fake-derived virtio PCI binding metadata only. It does not
  execute live config-space writes or reads, restore BARs on hardware, validate
  live device behavior, map ECAM or BARs, perform MMIO or DMA, bind live
  drivers, prove cloud-provider hardware support, support ASP.NET Minimal
  API/Kestrel, prove cloud readiness, or prove production readiness.
