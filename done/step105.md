# Step 105 - Native App Launch Plan

## What Changed

- Added `NativeAppLaunchPlan` for host-tested manifest-to-launch readiness planning.
- Added status-coded checks for package-relative app path, network/log support, required boot capabilities, and required runtime capabilities.
- Added host tests for accepted manifests, missing capability failures, and malformed requirement failures.

## Why It Changed

Issue #123 adds a deterministic boundary between manifest parsing and future app launching without executing app code or inventing the app ABI prematurely.

## Files Changed

- `src/OS.Kernel/Manifest/NativeAppLaunchPlan.cs`
- `tests/OS.HostTests/NativeAppLaunchPlanTests.cs`
- `docs/app-model.md`
- `docs/manifest.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build > .\artifacts\logs\host-tests-current.log
```

## Logs And Evidence

- Build passed with 0 warnings and 0 errors.
- Host tests emitted:
  - `TEST_PASS appmodel launch plan accepts manifest`
  - `TEST_PASS appmodel launch plan rejects missing capabilities`
  - `TEST_PASS appmodel launch plan rejects malformed requirements`
  - `TEST_PASS host.tests`

## Failures

- No failures in the final coordinated verification pass.

## Deferred Work

- No app ABI.
- No package payload read/load.
- No app execution, stdout/stderr routing, exit capture, or restart policy.
- No hosted .NET 10 or Kestrel launch.

## Evidence Boundary

This step proves launch planning only. It is not native app execution, hosted .NET 10, Minimal API, or Kestrel evidence.
