# Step 566 - Native app payload mapping default-result guard

## Summary

Added explicit initialized-state gating to
`NativeAppPayloadMappingPlanResult` so a default payload mapping result no
longer satisfies `Succeeded`.

## Why

The payload mapping planner turns a selected app descriptor into future
executable and entrypoint ranges. Because the success enum value is zero, a
default result could otherwise look like a completed range plan before payload
mapping had run.

## Files changed

- `src/OS.Kernel/Runtime/NativeAppPayloadMappingPlan.cs`
- `tests/OS.HostTests/NativeAppPayloadMappingPlanTests.cs`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step566.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to `appmodel payload mapping`, `TEST_PASS host.tests`, and
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
TEST_START appmodel payload mapping plans executable range
TEST_PASS appmodel payload mapping plans executable range
TEST_START appmodel payload mapping rejects descriptor failure
TEST_PASS appmodel payload mapping rejects descriptor failure
TEST_START appmodel payload mapping rejects zero executable bytes
TEST_PASS appmodel payload mapping rejects zero executable bytes
TEST_START appmodel payload mapping rejects invalid entry range
TEST_PASS appmodel payload mapping rejects invalid entry range
TEST_START appmodel payload mapping default result is not success
TEST_PASS appmodel payload mapping default result is not success
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

- This is host-tested payload range-planning metadata safety only. It does not
  allocate executable memory, copy executable bytes, change page permissions,
  relocate code, invoke native app code, host .NET 10, support Kestrel, prove
  cloud readiness, or prove production readiness.

