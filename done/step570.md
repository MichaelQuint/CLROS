# Step 570 - Native app exit-status create default-result guard

## Summary

Added explicit initialized-state gating to
`NativeAppExitStatusCreateResult` so a default exit-status create result no
longer satisfies `Succeeded`.

## Why

The create result records future native app exit-code and launcher-failure
metadata before marker formatting. Because the success enum value is zero, a
default result could otherwise look like a valid exit-status record before exit
code and PAL status validation had run.

## Files changed

- `src/OS.Kernel/Runtime/NativeAppExitStatus.cs`
- `tests/OS.HostTests/NativeAppExitStatusTests.cs`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step570.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to `appmodel.exit status`, `TEST_PASS host.tests`, and `TEST_FAIL`.
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
TEST_START appmodel.exit status formats success marker
TEST_PASS appmodel.exit status formats success marker
TEST_START appmodel.exit status formats nonzero marker
TEST_PASS appmodel.exit status formats nonzero marker
TEST_START appmodel.exit status formats launcher failure
TEST_PASS appmodel.exit status formats launcher failure
TEST_START appmodel.exit status rejects invalid input
TEST_PASS appmodel.exit status rejects invalid input
TEST_START appmodel.exit status create default result is not success
TEST_PASS appmodel.exit status create default result is not success
TEST_START appmodel.exit status writes marker to app log
TEST_PASS appmodel.exit status writes marker to app log
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

- None.

## Deferred work

- This is host-tested exit-status metadata creation only. It does not observe a
  live app exit, start a supervisor, enforce restart policy, expose a host
  runner result, execute native app code, host .NET 10, support Kestrel, prove
  cloud readiness, or prove production readiness.

