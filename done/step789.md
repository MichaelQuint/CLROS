# Step 789 - PAL app log BufferOperations copy path

## Summary
- Migrated `OSHostAppLogTable` record write/read byte copies from direct span
  `CopyTo` calls to `OS.Std.Memory.BufferOperations.MemoryCopy`.
- Added a source-guard host-test marker for the app-log table copy paths:
  `TEST_PASS pal.applog table uses shared buffer operations`.

## Why
- Issue #746 required PAL app-log record copies to use the shared no-runtime
  buffer-copy helper while preserving bounded write/read behavior and status
  results.

## Files changed
- `src/OS.Kernel/Pal/OSHostAppLogTable.cs`
- `tests/OS.HostTests/PalAppLogTableTests.cs`
- `done/step789.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:BaseIntermediateOutputPath=..\..\artifacts\issue746-pal-applog-bufferops\obj\ -p:OutputPath=..\..\artifacts\issue746-pal-applog-bufferops\bin\`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo --artifacts-path .\artifacts\issue746-pal-applog-bufferops`
- `dotnet .\artifacts\issue746-pal-applog-bufferops\bin\OS.HostTests\debug\OS.HostTests.dll`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:OutputPath=..\..\artifacts\issue746-pal-applog-bufferops\bin\Debug\net10.0\`
- `dotnet .\artifacts\issue746-pal-applog-bufferops\bin\Debug\net10.0\OS.HostTests.dll`
- `git -c core.autocrlf=false diff --check -- .\src\OS.Kernel\Pal\OSHostAppLogTable.cs .\tests\OS.HostTests\PalAppLogTableTests.cs .\done\step789.md`
- Scoped trailing-whitespace scan over `OSHostAppLogTable.cs`,
  `PalAppLogTableTests.cs`, and `done/step789.md`.

## Evidence
- `artifacts/logs/issue746-build.log` captured the exact issue-command build
  attempt.
- `artifacts/logs/issue746-build-artifacts-path.log` captured the passing
  focused artifact-path build.
- `artifacts/logs/issue746-hosttests.log` captured the artifact-path host-test
  attempt.
- `artifacts/logs/issue746-build-default-layout.log` captured the passing
  default-layout host-test build.
- `artifacts/logs/issue746-hosttests-default-layout.log` captured the passing
  full host-test run with:
  - `TEST_PASS pal.applog table uses shared buffer operations`
  - `TEST_PASS host.tests`
- `artifacts/logs/issue746-build-artifact-standard-layout.log` captured the
  passing artifact-directory build for
  `artifacts/issue746-pal-applog-bufferops/bin/Debug/net10.0/OS.HostTests.dll`.
- `artifacts/logs/issue746-hosttests-artifact-standard-layout.log` captured the
  passing artifact-directory full host-test run with:
  - `TEST_PASS pal.app_log stdout write captured`
  - `TEST_PASS pal.app_log default results are not success`
  - `TEST_PASS pal.applog table uses shared buffer operations`
  - `TEST_PASS host.tests`
- `artifacts/logs/issue746-diff-check-scoped.log` captured the passing scoped
  `git diff --check`.
- `artifacts/logs/issue746-trailing-whitespace.log` captured the passing scoped
  trailing-whitespace scan.

## Failures
- The exact issue-command build using one shared
  `BaseIntermediateOutputPath=..\..\artifacts\issue746-pal-applog-bufferops\obj\`
  failed during NuGet restore with `Cannot create a file when that file already
  exists`.
- The artifact-path host-test run reached app-log evidence but failed later in
  image-builder tests because those tests derive spawned tool paths from
  `AppContext.BaseDirectory`; the artifact-path layout made them look under
  `tools/OS.ImageBuilder/bin/OS.HostTests/debug/OS.ImageBuilder.dll`.
- The default-layout and artifact-standard-layout full host-test runs passed.

## Deferred work
- No PAL docs were changed in this slice because `docs/pal.md` already had
  unrelated dirty-worktree edits. The existing PAL doc still accurately
  describes `OSHostAppLogTable` as a host-tested bounded app-log sink.

## Evidence boundary
- This is host-tested in-process PAL app-log table evidence only. It does not
  export PAL functions, connect hosted runtime stdout/stderr, write guest serial
  output, or prove hosted .NET/Kestrel behavior.

