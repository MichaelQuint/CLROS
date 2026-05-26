# Step 501 - PCI BAR decode default guard

## Summary

Implemented Gitea issue #486 by making `PciBarDecodeResult` reject
default/zero values as successful PCI BAR decode evidence.

## Why

`PciBarDecodeStatus.Success` is zero. The result struct previously reported
success for default values even though no type-0 header BAR values had been
decoded, no descriptor set had been initialized, and no failing-index metadata
had been written. BAR decode evidence must only be successful after explicit
decoding.

## Files changed

- `src/OS.Kernel/Drivers/PciBarDecoder.cs`
- `tests/OS.HostTests/PciBarDecoderTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step501.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.pci.bar default result is not success` and
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

TEST_START drivers.pci.bar default result is not success
TEST_PASS drivers.pci.bar default result is not success
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

- This is host-tested caller-provided PCI BAR metadata decoding only. It does
  not access live ECAM, execute BAR sizing writes, map BARs, perform MMIO or
  DMA, bind drivers, prove cloud-provider hardware support, support ASP.NET
  Minimal API/Kestrel, prove cloud readiness, or prove production readiness.
