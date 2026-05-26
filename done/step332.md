# Step 332 - Native App Selection Loader Default Result Guard

## Summary
Hardened the native app selection loader result contract so `default(NativeAppSelectionLoadResult)` cannot be interpreted as a successful app-selection load.

## Why
App-selection readiness aggregates several nested status objects before the future boot launcher can consume a selected app descriptor. A default struct previously had the enum default for `Success`, so this slice adds an explicit initialization guard and nested-success check before `Succeeded` can report true.

## Files changed
- `src/OS.Kernel/Manifest/NativeAppSelectionLoader.cs`
- `tests/OS.HostTests/NativeAppSelectionLoaderTests.cs`
- `docs/app-model.md`
- `docs/manifest.md`
- `docs/completion-gates.md`
- `done/step332.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence
- Focused host build completed with 0 warnings and 0 errors.
- Full host-test run emitted `TEST_PASS appmodel selection loader default result is not success`.
- Full host-test run ended with `TEST_PASS host.tests`.

## Failures
- An initial `dotnet run .\tests\OS.HostTests\OS.HostTests.csproj --no-build` command was rejected by the CLI because the project path was passed without `--project`; the corrected command passed.

## Deferred work
- The selection loader still consumes caller-provided buffers and an injected block device only.
- Live boot-time package reads, executable mapping, native app execution, hosted .NET 10, Minimal API, and Kestrel remain unevidenced.
- Gitea issue updates remain blocked in this environment because no API credentials are available.

