# Step 492 - COM1 writer default guard

## Summary

Implemented Gitea issue #477 by making `Com1SerialWriterResult` reject
default/zero values as successful COM1 writer evidence.

## Why

`Com1SerialWriterStatus.Success` is zero. The writer result previously reported
success for `default(Com1SerialWriterResult)` even though no initialization,
ASCII write, byte count, emitted port-write count, or line-status poll metadata
had been explicitly produced. Future owned serial transport code must only
trust initialized writer results.

## Files changed

- `src/OS.Kernel/Diagnostics/Com1SerialWriter.cs`
- `tests/OS.HostTests/Com1SerialWriterTests.cs`
- `docs/diagnostics.md`
- `docs/completion-gates.md`
- `done/step492.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `com1 writer default result is not success` and
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

TEST_START com1 writer default result is not success
TEST_PASS com1 writer default result is not success
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

- This is host-tested fake-backend serial metadata only. It does not perform
  CPU port I/O, initialize COM1 on hardware, install an owned serial backend,
  prove post-`ExitBootServices` output, support ASP.NET Minimal API/Kestrel,
  prove cloud readiness, or prove production readiness.
