# Step 575 - Native app launch descriptor default-result guard

## Summary

Added explicit initialized-state gating to `NativeAppLaunchDescriptorResult` so
a default launch descriptor result no longer satisfies `Succeeded`.

## Why

The selected native app launch descriptor is the app-model boundary between a
validated launch plan, package file lookup, and native payload-header metadata.
Because `NativeAppLaunchDescriptorStatus.Success` is zero-valued, a default
result could otherwise look ready before the package file and payload header had
been validated.

## Files changed

- `src/OS.Kernel/Manifest/NativeAppLaunchDescriptor.cs`
- `tests/OS.HostTests/NativeAppLaunchDescriptorTests.cs`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step575.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to `appmodel launch descriptor`,
  `boot.sequence app launch descriptor`, `boot.sequence app payload`,
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
TEST_START boot.sequence app launch descriptor ready
TEST_PASS boot.sequence app launch descriptor ready
TEST_START boot.sequence app launch descriptor rejects missing binary
TEST_PASS boot.sequence app launch descriptor rejects missing binary
TEST_START boot.sequence app launch descriptor rejects launch-plan failure
TEST_PASS boot.sequence app launch descriptor rejects launch-plan failure
TEST_START boot.sequence app payload ready
TEST_PASS boot.sequence app payload ready
TEST_START boot.sequence app payload rejects descriptor failure
TEST_PASS boot.sequence app payload rejects descriptor failure
TEST_START boot.sequence app payload rejects invalid header
TEST_PASS boot.sequence app payload rejects invalid header
TEST_START boot.sequence app payload leaves default markers unchanged
TEST_PASS boot.sequence app payload leaves default markers unchanged
TEST_START appmodel launch descriptor accepts package payload header
TEST_PASS appmodel launch descriptor accepts package payload header
TEST_START appmodel launch descriptor rejects invalid payload header
TEST_PASS appmodel launch descriptor rejects invalid payload header
TEST_START appmodel launch descriptor default result is not success
TEST_PASS appmodel launch descriptor default result is not success
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

- This is host-tested launch-descriptor metadata only. It does not prove live
  package reads, payload mapping, native app execution, hosted .NET 10, Minimal
  API, Kestrel, cloud readiness, or production readiness.
