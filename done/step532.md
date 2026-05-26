# Step 532 - PCI ECAM Physical Header Default Result Guard

## Summary

Added explicit initialization metadata to the PCI ECAM physical-header read
result so default/zero values do not report success.

## Why

`PciEcamPhysicalHeaderReadStatus.Success` is the zero enum value. Default
physical-header read results and the adapter's pre-read `LastResult` should not
look like successful ECAM reads before an injected physical-memory read path has
actually run.

## Files changed

- `src/OS.Kernel/Drivers/PciEcamPhysicalHeaderReader.cs`
- `tests/OS.HostTests/PciEcamPhysicalHeaderReaderTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step532.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'pci\.ecam\.physical_header default result is not success|TEST_PASS host\.tests|TEST_FAIL'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START pci.ecam.physical_header default result is not success
TEST_PASS pci.ecam.physical_header default result is not success
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

- Claude Code CLI still reports `Not logged in - Please run /login`; the Claude
  MCP agent registry was present but had no runnable default agent type. Built-in
  read-only explorer subagents were used instead for future-slice discovery.

## Deferred work

- This is host-tested injected-physical-memory metadata only. It does not map
  ECAM, execute live PCI config reads, enumerate QEMU/cloud hardware, size/map
  BARs, walk capabilities, configure interrupts, bind drivers, prove ASP.NET
  Minimal API/Kestrel, or prove cloud/production readiness.
