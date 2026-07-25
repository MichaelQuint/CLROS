# Step 494 - Owned COM1 readiness default guard

## Summary

Implemented Gitea issue #479 by making
`OwnedCom1DiagnosticsReadinessResult` reject default/zero values as successful
aggregate owned COM1 diagnostics readiness evidence.

## Why

`OwnedCom1DiagnosticsReadinessStatus.Success` is zero. The aggregate result
previously reported success for `default(OwnedCom1DiagnosticsReadinessResult)`
even though no COM1 backend plan, backend contract, owned-serial readiness,
guard transition, or post-firmware marker evidence had been initialized.
Aggregate post-firmware diagnostics readiness must require all nested readiness
and marker evidence.

## Files changed

- `src/OS.Kernel/Diagnostics/OwnedCom1DiagnosticsReadiness.cs`
- `tests/OS.HostTests/OwnedCom1DiagnosticsReadinessTests.cs`
- `docs/diagnostics.md`
- `docs/completion-gates.md`
- `done/step494.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `diagnostics.owned com1 readiness default result is not
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

TEST_START diagnostics.owned com1 readiness default result is not success
TEST_PASS diagnostics.owned com1 readiness default result is not success
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

- This is host-tested aggregate readiness and marker-boundary metadata only. It
  does not configure COM1, invoke generated port-I/O stubs, call
  `ExitBootServices`, prove live post-firmware serial output, support ASP.NET
  Minimal API/Kestrel, prove cloud readiness, or prove production readiness.
