# Step 568 - Native app invocation default-result guard

## Summary

Added explicit initialized-state gating to `NativeAppInvocationPlanResult` so a
default invocation result no longer satisfies `Succeeded`.

## Why

The invocation plan records future native app stack, entrypoint, executable-map,
and manifest metadata prerequisites. Because the success enum value is zero, a
default result could otherwise look like a completed invocation plan before any
executable-memory or launch-plan validation had run.

## Files changed

- `src/OS.Kernel/Runtime/NativeAppInvocationPlan.cs`
- `tests/OS.HostTests/NativeAppInvocationPlanTests.cs`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step568.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to `appmodel native invocation plan`, `TEST_PASS host.tests`, and
  `TEST_FAIL`.
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
TEST_START appmodel native invocation plan accepts executable memory
TEST_PASS appmodel native invocation plan accepts executable memory
TEST_START appmodel native invocation plan rejects memory failure
TEST_PASS appmodel native invocation plan rejects memory failure
TEST_START appmodel native invocation plan rejects invalid stack
TEST_PASS appmodel native invocation plan rejects invalid stack
TEST_START appmodel native invocation plan preserves manifest metadata counts
TEST_PASS appmodel native invocation plan preserves manifest metadata counts
TEST_START appmodel native invocation plan rejects invalid entry metadata
TEST_PASS appmodel native invocation plan rejects invalid entry metadata
TEST_START appmodel native invocation plan default result is not success
TEST_PASS appmodel native invocation plan default result is not success
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

- This is host-tested invocation metadata planning only. It does not allocate
  stacks, switch stacks, create threads, map pages, define a final app ABI,
  invoke native app code, host .NET 10, support Kestrel, prove cloud readiness,
  or prove production readiness.

