# Step 493 - Owned serial readiness default guard

## Summary

Implemented Gitea issue #478 by making
`OwnedSerialDiagnosticsReadinessResult` reject default/zero values as
successful owned-serial readiness evidence.

## Why

`OwnedSerialDiagnosticsReadinessStatus.Success` is zero. The readiness result
previously reported success for `default(OwnedSerialDiagnosticsReadinessResult)`
even though no COM1 backend plan, backend contract, stub bundle, COM1 base port,
or resolved backend stub addresses had been initialized. Post-firmware
diagnostics readiness must only trust explicit COM1 readiness composition.

## Files changed

- `src/OS.Kernel/Diagnostics/OwnedSerialDiagnosticsReadiness.cs`
- `tests/OS.HostTests/OwnedSerialDiagnosticsReadinessTests.cs`
- `docs/diagnostics.md`
- `docs/completion-gates.md`
- `done/step493.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `diagnostics owned serial readiness default result is not
  success` and `TEST_PASS host.tests`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START diagnostics owned serial readiness default result is not success
TEST_PASS diagnostics owned serial readiness default result is not success
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

- This is host-tested readiness metadata only. It does not perform CPU port
  I/O, install an owned serial backend, prove post-`ExitBootServices` serial
  output, support ASP.NET Minimal API/Kestrel, prove cloud readiness, or prove
  production readiness.
