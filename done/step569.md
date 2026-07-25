# Step 569 - Native app lifecycle default-result guard

## Summary

Added explicit initialized-state gating to `NativeAppLifecyclePlanResult` so a
default lifecycle result no longer satisfies `Succeeded`.

## Why

The lifecycle planner aggregates invocation metadata and app-log readiness for
future supervisor work. Because the success enum value is zero, a default
result could otherwise look like completed lifecycle readiness before invocation
or log-sink validation had run.

## Files changed

- `src/OS.Kernel/Runtime/NativeAppLifecyclePlan.cs`
- `tests/OS.HostTests/NativeAppLifecyclePlanTests.cs`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step569.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to `appmodel.lifecycle`, `TEST_PASS host.tests`, and `TEST_FAIL`.
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
TEST_START appmodel.lifecycle accepts invocation and log sink
TEST_PASS appmodel.lifecycle accepts invocation and log sink
TEST_START appmodel.lifecycle rejects invocation failure
TEST_PASS appmodel.lifecycle rejects invocation failure
TEST_START appmodel.lifecycle rejects missing log sink
TEST_PASS appmodel.lifecycle rejects missing log sink
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

- This is host-tested lifecycle metadata planning only. It does not start a
  supervisor, route live logs, observe app exits, enforce restart policy,
  isolate apps, execute native app code, host .NET 10, support Kestrel, prove
  cloud readiness, or prove production readiness.

