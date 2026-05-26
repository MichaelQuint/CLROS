# Step 495 - Owned serial diagnostics default guards

## Summary

Implemented Gitea issue #480 by making `OwnedSerialMarkerFormatResult` and
`OwnedSerialDiagnosticWriteResult` reject default/zero values as successful
owned-serial diagnostics evidence.

## Why

`OwnedSerialMarkerFormatStatus.Success` and
`OwnedSerialDiagnosticWriteStatus.Success` are zero. Their result structs
previously reported success for default values even though no marker bytes,
line terminator, serial transport status, or write byte counts had been
initialized. The owned-serial diagnostics path must only trust explicitly
formatted marker lines and explicit platform serial write results.

## Files changed

- `src/OS.Kernel/Diagnostics/OwnedSerialDiagnostics.cs`
- `tests/OS.HostTests/OwnedSerialDiagnosticsTests.cs`
- `docs/diagnostics.md`
- `docs/completion-gates.md`
- `done/step495.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `diagnostics.owned_serial default results are not success` and
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

TEST_START diagnostics.owned_serial default results are not success
TEST_PASS diagnostics.owned_serial default results are not success
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

- This is host-tested formatter/write metadata only. It does not perform CPU
  port I/O, initialize COM1 on hardware, prove live owned serial output after
  real `ExitBootServices`, support ASP.NET Minimal API/Kestrel, prove cloud
  readiness, or prove production readiness.
