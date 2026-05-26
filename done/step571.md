# Step 571 - Native app exit-marker default-result guard

## Summary

Added explicit initialized-state gating to `NativeAppExitMarkerResult` so a
default exit-marker format/write result no longer satisfies `Succeeded`.

## Why

The exit-marker result records future native app exit marker formatting and app
log write metadata. Because the success enum and app-log OK status are both
zero-valued, a default result could otherwise look like a successful marker
format/write before validation, formatting, or app-log writing had run.

## Files changed

- `src/OS.Kernel/Runtime/NativeAppExitStatus.cs`
- `tests/OS.HostTests/NativeAppExitStatusTests.cs`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step571.md`

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
TEST_START appmodel.exit status marker default result is not success
TEST_PASS appmodel.exit status marker default result is not success
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

- This is host-tested exit-marker metadata only. It does not observe a live app
  exit, start a supervisor, route live stdout/stderr, enforce restart policy,
  expose a host runner result, execute native app code, host .NET 10, support
  Kestrel, prove cloud readiness, or prove production readiness.
