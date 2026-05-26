# Step 500 - PCI configuration header default guard

## Summary

Implemented Gitea issue #485 by making `PciConfigurationHeaderResult`
reject default/zero values as successful PCI type-0 header parse evidence.

## Why

`PciConfigurationHeaderStatus.Success` is zero. The result struct previously
reported success for default values even though no caller-provided 64-byte
configuration header had been parsed and no vendor/device, class, BAR, or
capability-pointer metadata had been initialized. PCI header parse evidence
must only be successful after explicit parsing.

## Files changed

- `src/OS.Kernel/Drivers/PciConfigurationHeader.cs`
- `tests/OS.HostTests/PciConfigurationHeaderTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step500.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.pci.header default result is not success` and
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

TEST_START drivers.pci.header default result is not success
TEST_PASS drivers.pci.header default result is not success
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

- This is host-tested caller-provided PCI header byte parsing only. It does not
  access live ECAM, perform MMIO reads or writes, enumerate PCI, walk capability
  structures, size or map BARs, bind drivers, prove cloud-provider hardware
  support, support ASP.NET Minimal API/Kestrel, prove cloud readiness, or prove
  production readiness.
