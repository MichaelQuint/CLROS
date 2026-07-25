# Step 496 - Panic serial reporter default guard

## Summary

Implemented Gitea issue #481 by making `PanicSerialReporterResult` reject
default/zero values as successful panic-serial evidence.

## Why

`PanicSerialReporterStatus.Ok` is zero. The result struct previously reported
success for default values even though no caller-owned panic text had been
formatted and no bytes had been flushed to the serial sink. Panic diagnostics
must only trust explicit reporter outcomes.

## Files changed

- `src/OS.Kernel/Diagnostics/PanicSerialReporter.cs`
- `tests/OS.HostTests/PanicSerialReporterTests.cs`
- `docs/diagnostics.md`
- `docs/completion-gates.md`
- `done/step496.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `panic serial reporter default result is not success` and
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

TEST_START panic serial reporter default result is not success
TEST_PASS panic serial reporter default result is not success
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

- This is host-tested panic reporter result metadata only. It does not perform
  CPU port I/O, install an IDT, capture live fault registers, initialize COM1
  on hardware, prove live owned serial output after real `ExitBootServices`,
  support ASP.NET Minimal API/Kestrel, prove cloud readiness, or prove
  production readiness.
