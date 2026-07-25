# Step 528 - ENA DMA Address Default Result Guard

## Summary

Added explicit initialization metadata to ENA DMA region-validation and admin
queue DMA memory plan result structs so default/zero values do not report
success.

## Why

`EnaDmaAddressPlanStatus.Success` is the zero enum value. DMA address evidence
should only be successful after explicit region validation or admin queue DMA
memory planning validates the caller-provided addresses and lengths.

## Files changed

- `src/OS.Kernel/Drivers/EnaDmaAddressPlan.cs`
- `tests/OS.HostTests/EnaDmaAddressPlanTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step528.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.ena\.dma rejects default results|TEST_PASS host\.tests|TEST_FAIL'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START drivers.ena.dma rejects default results
TEST_PASS drivers.ena.dma rejects default results
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

- This is host-tested ENA DMA address metadata only. It does not allocate DMA
  memory, map pages, program an IOMMU, write ENA BAR registers, ring doorbells,
  configure interrupts, move packets, host .NET, prove Kestrel, or prove
  cloud/production readiness.

