# Step 534 - PCI Bus Scan Default Result Guard

## Summary
- Added explicit initialization metadata to `PciBusScanResult`.
- Updated `PciBusScanResult.Succeeded` so default/zero scan results no longer
  report success before `PciBusScanner` creates a success or failure result.
- Added host marker coverage and updated the driver and completion-gate docs.

## Why
`PciBusScanStatus.Success` is the zero enum value. Without an initialization
guard, `default(PciBusScanResult)` can be mistaken for successful PCI scan
evidence before any injected-reader scan actually ran.

## Files changed
- `src/OS.Kernel/Drivers/PciBusScanner.cs`
- `tests/OS.HostTests/PciBusScannerTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step534.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.pci.scan default result is not success`,
  `TEST_PASS host.tests`, and failures.
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence
```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```text
TEST_START drivers.pci.scan default result is not success
TEST_PASS drivers.pci.scan default result is not success
TEST_PASS host.tests
```

```text
dotnet build .\OS.slnx --nologo -m:1

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

```text
git diff --check
ExitCode: 0; existing LF-to-CRLF working-copy warnings only.
```

## Failures
- None for this slice.

## Deferred work
- This is host-tested injected-reader PCI bus scanner contract evidence only.
- No live ECAM mapping, live PCI enumeration, BAR mapping, interrupt
  configuration, driver binding, cloud readiness, or production readiness is
  claimed.

