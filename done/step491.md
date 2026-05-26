# Step 491 - Serial configuration default guards

## Summary

Implemented Gitea issue #476 by making the serial-port configuration result
structs reject default/zero values as successful COM1 configuration evidence.

## Why

`SerialPortConfigurationStatus.Success` is zero. `SerialPortConfigurationResult`,
`SerialPortDivisorLatchResult`, and `SerialPortInitializationPlanResult`
previously reported success for default values even though no COM1
configuration, divisor latch, or initialization write sequence had been
produced. Future owned serial readiness must only trust explicitly constructed
serial configuration metadata.

## Files changed

- `src/OS.Kernel/Diagnostics/SerialPortConfiguration.cs`
- `tests/OS.HostTests/SerialPortConfigurationTests.cs`
- `docs/diagnostics.md`
- `docs/completion-gates.md`
- `done/step491.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `serial port config default results are not success` and
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

TEST_START serial port config default results are not success
TEST_PASS serial port config default results are not success
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

- This is host-tested serial configuration metadata only. It does not perform
  CPU port I/O, initialize COM1 on hardware, install an owned serial backend,
  prove post-`ExitBootServices` output, support ASP.NET Minimal API/Kestrel,
  prove cloud readiness, or prove production readiness.
