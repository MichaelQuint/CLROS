# Step 750 - Phase G Native App Launch Diagnostic Formatter

## Summary

Added a small host-tested app-model diagnostic formatter for native app launch
plans. The formatter emits caller-owned `APP_LAUNCH_PLAN status=` metadata for
already-produced `NativeAppLaunchPlanResult` values and keeps explicit
`appLoaded=false`, `appExecuted=false`, `hostedDotNetStarted=false`, and
`kestrelStarted=false` boundary flags.

## Why

Phase G needed one more narrow preflight/diagnostic contract between manifest
launch-plan selection and later package loading or invocation work. This slice
records the selected app's bounded metadata and capability requirements without
touching CI smoke scripts, QEMU docs, storage/std files, or live boot wiring.

## Files changed

- `src/OS.Kernel/Manifest/NativeAppLaunchPlanDiagnosticFormatter.cs`
- `tests/OS.HostTests/NativeAppLaunchPlanDiagnosticFormatterTests.cs`
- `docs/app-model.md`
- `docs/manifest.md`
- `done/step750.md`

## Tests run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

- Build log: `artifacts/logs/issue-app-model-slice-hosttests-build.log`
- Host-test log: `artifacts/logs/issue-app-model-slice-hosttests.log`
- New host markers:
  - `TEST_PASS appmodel launch diagnostic formats selected app`
  - `TEST_PASS appmodel launch diagnostic reports short destination`
  - `TEST_PASS appmodel launch diagnostic formats missing runtime`
  - `TEST_PASS appmodel launch diagnostic rejects default result`
- Full host harness emitted `TEST_PASS host.tests`.

## Failures

None in the targeted build or host-test run.

## Deferred work

- Wire launch diagnostics into a future boot/app-selection marker only after
  live storage can supply manifest/package inputs.
- Keep native app execution, hosted .NET 10, Minimal API, and Kestrel claims
  blocked until the completion gates have matching live evidence.

