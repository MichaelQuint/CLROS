# Step 497 - Panic text buffer default guard

## Summary

Implemented Gitea issue #482 by making `PanicTextBuffer` reject default/zero
ref-struct values as successful panic text-buffer evidence.

## Why

`PanicTextBufferStatus.Ok` is zero. A default `PanicTextBuffer` previously
reported success even though the constructor had not run and no caller-owned
destination storage had been supplied. Panic formatting evidence must only come
from an explicitly initialized caller-owned buffer.

## Files changed

- `src/OS.Kernel/Diagnostics/PanicTextBuffer.cs`
- `tests/OS.HostTests/PanicTextBufferTests.cs`
- `docs/diagnostics.md`
- `docs/completion-gates.md`
- `done/step497.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `panic text buffer default buffer is not success` and
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

TEST_START panic text buffer default buffer is not success
TEST_PASS panic text buffer default buffer is not success
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

- This is host-tested panic text-buffer metadata only. It does not install an
  IDT, capture live CPU fault registers, initialize COM1 on hardware, perform
  CPU port I/O, prove live owned serial output after real `ExitBootServices`,
  support ASP.NET Minimal API/Kestrel, prove cloud readiness, or prove
  production readiness.
