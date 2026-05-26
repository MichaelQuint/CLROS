# Step 785 - Phase F/G storage-app staging BufferOperations copy path

## Summary

Migrated the package-file staging, native app executable payload staging, and
package parser path-copy byte writes onto the shared
`OS.Std.Memory.BufferOperations.MemoryCopy` helper.

## Why

Issue #742 asks the Phase F/G storage-app staging paths to use the shared
no-runtime buffer-copy primitive instead of direct span `CopyTo` calls, while
preserving existing status-coded destination failures.

## Files changed

- `src/OS.Kernel/Storage/OsPackageFileStagingPlan.cs`
- `src/OS.Kernel/Runtime/NativeAppPayloadStagingPlan.cs`
- `src/OS.Kernel/Storage/OsPackageParser.cs`
- `tests/OS.HostTests/AppStorageBufferOperationsSourceGuardTests.cs`
- `tests/OS.HostTests/StoragePackageFileStagingPlanTests.cs`
- `tests/OS.HostTests/NativeAppPayloadStagingPlanTests.cs`
- `tests/OS.HostTests/StoragePackageParserTests.cs`
- `docs/cstorage.md`
- `docs/app-model.md`
- `done/step785.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue742-storage-app-staging-bufferops\bin\Debug\net10.0`
- `dotnet .\artifacts\issue742-storage-app-staging-bufferops\bin\Debug\net10.0\OS.HostTests.dll`
- Scoped `git diff --check` for the files changed by issue #742.
- Scoped trailing-whitespace `rg -n "[ \t]$"` for the files changed by issue
  #742.

## Evidence

- `artifacts/logs/issue742-host-build-20260525.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- `artifacts/logs/issue742-hosttests-20260525.log`
  - `TEST_PASS storage.package staging uses shared buffer operations`
  - `TEST_PASS storage.package parser path copy uses shared buffer operations`
  - `TEST_PASS appmodel payload staging uses shared buffer operations`
  - `TEST_PASS host.tests`
- `artifacts/logs/issue742-git-diff-check-scoped-20260525.log`
  - `TEST_PASS issue742.git_diff_check_scoped`
  - Git reported line-ending conversion warnings for existing Windows checkout
    settings, but no `diff --check` whitespace errors.
- `artifacts/logs/issue742-trailing-whitespace-20260525.log`
  - `TEST_PASS issue742.scoped_trailing_whitespace`
- No Gitea API was used.

## Failures

- None in the final verification run.

## Deferred work

- No live storage, executable mapping, app invocation, hosted .NET, or Kestrel
  behavior was changed or verified.
- The evidence remains host-test and source-guard evidence for caller-owned
  buffer-copy staging paths only.
