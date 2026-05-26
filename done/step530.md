# Step 530 - ENA PCI Resource Default Result Guard

## Summary

Added explicit initialization metadata to the PCI MSI-X parse result and ENA
PCI resource plan result so default/zero values do not report success.

## Why

`PciMsixCapabilityParseStatus.Success` and `EnaPciResourcePlanStatus.Success`
are zero enum values. ENA PCI resource evidence should only be successful after
explicit MSI-X capability parsing or resource planning validates caller-provided
metadata.

## Files changed

- `src/OS.Kernel/Drivers/EnaPciResourcePlan.cs`
- `tests/OS.HostTests/EnaPciResourcePlanTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step530.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.ena\.pci\.resources rejects default results|TEST_PASS host\.tests|TEST_FAIL'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START drivers.ena.pci.resources rejects default results
TEST_PASS drivers.ena.pci.resources rejects default results
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

- Claude Code delegation retry still reports `Not logged in - Please run /login`,
  so no Claude subagent wrote issue updates for this slice.

## Deferred work

- This is host-tested ENA PCI MSI-X/resource metadata only. It does not walk
  live PCI capabilities, size or map BARs, configure MSI-X, allocate DMA
  memory, create admin queues, move packets, host .NET, prove Kestrel, or prove
  cloud/production readiness.

