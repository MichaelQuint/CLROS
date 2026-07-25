# Step 401 - Manifest Listen Endpoint PAL Config Projection

## Summary
Extended `OSHostConfigEnvironmentTable.FromNativeLaunchPlan` so a launch plan with a listen endpoint exposes host-only config keys for `kestrel.listen.0`, `kestrel.listen.0.host`, and `kestrel.listen.0.port`.

## Why
Future hosted workloads need a PAL config path for manifest-selected listener metadata. This slice keeps that path host-tested and explicit while exported PAL config functions, hosted .NET configuration, and Kestrel binding remain unimplemented.

## Files changed
- `src/OS.Kernel/Pal/OSHostConfigEnvironmentTable.cs`
- `tests/OS.HostTests/PalConfigEnvironmentTests.cs`
- `docs/manifest.md`
- `docs/pal.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `done/step401.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence
- Focused host-test build completed with 0 warnings and 0 errors.
- Host-test marker scan emitted:
  - `TEST_PASS pal.config table exposes manifest listen endpoint`
  - `TEST_PASS pal.config table exposes listen host and port`
  - `TEST_PASS pal.config facade returns manifest listen endpoint`
  - `TEST_PASS pal.config table omits listen endpoint when absent`
  - `TEST_PASS pal.config table preserves host-only boundary`
  - `TEST_PASS host.tests`
- Full solution build completed with 0 warnings and 0 errors.
- `git diff --check` exited 0 with only LF-to-CRLF working-copy normalization warnings.

## Failures
- None.

## Deferred work
- Export real `OSHost_GetConfigValue`, `OSHost_EnumerateConfig`, `OSHost_GetEnvironmentValue`, and `OSHost_EnumerateEnvironment` only after the PAL export/runtime boundary exists.
- Feed hosted .NET 10 configuration and ASP.NET configuration from the exported PAL path after package/file/config/runtime prerequisites exist.
- Keep ASP.NET Minimal API, Kestrel, cloud readiness, and production readiness claims blocked until in-guest runtime/socket/config evidence exists.

## Evidence boundary
- This is host-tested config-table/facade metadata projection over a source-backed native launch plan. It does not export PAL functions, read guest storage, feed hosted .NET configuration, bind Kestrel, start hosted .NET 10, prove cloud readiness, or prove production readiness.
