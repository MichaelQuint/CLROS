# Step 574 - Native app launch-plan default-result guard

## Summary

Added explicit initialized-state gating to `NativeAppLaunchPlanResult` so a
default native app launch-plan result no longer satisfies `Succeeded`.

## Why

The native app launch plan records manifest-derived app metadata, boot
capability requirements, runtime capability requirements, and listen endpoint
metadata before future app launch. Because the launch-plan success status is
zero-valued, a default result could otherwise look like a valid launch plan
before manifest requirements had been validated.

## Files changed

- `src/OS.Kernel/Manifest/NativeAppLaunchPlan.cs`
- `tests/OS.HostTests/NativeAppLaunchPlanTests.cs`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step574.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to `appmodel launch plan`, `boot.sequence app package`,
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
TEST_START boot.sequence app package readiness emits ready marker
TEST_PASS boot.sequence app package readiness emits ready marker
TEST_START boot.sequence app package readiness rejects manifest failure
TEST_PASS boot.sequence app package readiness rejects manifest failure
TEST_START boot.sequence app package readiness rejects package failure
TEST_PASS boot.sequence app package readiness rejects package failure
TEST_START boot.sequence app package readiness rejects launch-plan failure
TEST_PASS boot.sequence app package readiness rejects launch-plan failure
TEST_START appmodel launch plan accepts manifest
TEST_PASS appmodel launch plan accepts manifest
TEST_START appmodel launch plan accepts packaged native app
TEST_PASS appmodel launch plan accepts packaged native app
TEST_START appmodel launch plan carries environment fields
TEST_PASS appmodel launch plan carries environment fields
TEST_START appmodel launch plan records listen endpoint
TEST_PASS appmodel launch plan records listen endpoint
TEST_START appmodel launch plan requires network for listen endpoint
TEST_PASS appmodel launch plan requires network for listen endpoint
TEST_START appmodel launch plan rejects missing capabilities
TEST_PASS appmodel launch plan rejects missing capabilities
TEST_START appmodel launch plan rejects malformed requirements
TEST_PASS appmodel launch plan rejects malformed requirements
TEST_START appmodel launch plan default result is not success
TEST_PASS appmodel launch plan default result is not success
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

- This is host-tested launch-plan metadata only. It does not prove boot-time
  package selection, app launch, executable mapping, native app execution,
  hosted .NET 10, Minimal API, Kestrel, cloud readiness, or production
  readiness.
