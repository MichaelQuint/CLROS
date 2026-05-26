# Step 577 - Native app lifecycle log-sink default-readiness guard

## Summary

Added explicit initialized-state and capacity gating to
`NativeAppLogSinkReadiness` so default log-sink readiness no longer allows
native app lifecycle planning to succeed.

## Why

The native app lifecycle plan is the final host-tested readiness aggregation
before future supervisor/launcher work. A default log-sink readiness value had
zero-valued `OSHostStatus.Ok` and could let lifecycle planning report success
with no remaining app-log capacity and no record size for app exit/status
markers.

## Files changed

- `src/OS.Kernel/Runtime/NativeAppLifecyclePlan.cs`
- `tests/OS.HostTests/NativeAppLifecyclePlanTests.cs`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step577.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to `appmodel.lifecycle`, `boot.sequence app lifecycle`,
  `TEST_PASS host.tests`, and `TEST_FAIL`.
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
TEST_START boot.sequence app lifecycle ready
TEST_PASS boot.sequence app lifecycle ready
TEST_START boot.sequence app lifecycle rejects dependency failure
TEST_PASS boot.sequence app lifecycle rejects dependency failure
TEST_START boot.sequence app lifecycle preserves default markers
TEST_PASS boot.sequence app lifecycle preserves default markers
TEST_START appmodel.lifecycle accepts invocation and log sink
TEST_PASS appmodel.lifecycle accepts invocation and log sink
TEST_START appmodel.lifecycle rejects invocation failure
TEST_PASS appmodel.lifecycle rejects invocation failure
TEST_START appmodel.lifecycle rejects missing log sink
TEST_PASS appmodel.lifecycle rejects missing log sink
TEST_START appmodel.lifecycle log sink default readiness is not success
TEST_PASS appmodel.lifecycle log sink default readiness is not success
TEST_START appmodel.lifecycle preserves app identity
TEST_PASS appmodel.lifecycle preserves app identity
TEST_START appmodel.lifecycle default result is not success
TEST_PASS appmodel.lifecycle default result is not success
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

- This is host-tested lifecycle/log-sink readiness only. It does not start a
  supervisor, route live logs, observe app exits, execute native apps, prove
  hosted .NET 10, Minimal API, Kestrel, cloud readiness, or production
  readiness.
