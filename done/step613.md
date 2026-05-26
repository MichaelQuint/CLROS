# Step 613 - Native App Package Staging Readiness Composition

## Summary

Added a host-tested native app package staging readiness composer. The new
runtime contract aggregates already-produced manifest/package/launch/descriptor
results, caller-owned package-file staging, payload mapping, payload staging,
executable-memory planning, and invocation planning into one initialized
readiness value.

## Why

Issue #5 needs boot-time package/app loading, but live virtio-blk and app
execution are still blocked. This slice tightens the storage/app-model boundary
by proving that staged package-file and staged executable-byte evidence can be
checked together before a future launcher trusts it.

## Files changed

- `src/OS.Kernel/Runtime/NativeAppPackageStagingReadinessPlan.cs`
  - Added staging-readiness status/result values and a composition planner.
  - Preserves nested status, byte, stack, and entry-offset metadata.
  - Rejects dependency failures, byte mismatches, entry mismatches, and
    default/zero readiness values.
- `tests/OS.HostTests/NativeAppPackageStagingReadinessPlanTests.cs`
  - Added focused host tests with stable `appmodel package staging readiness`
    markers.
- `docs/cstorage.md`
  - Documented the storage-facing evidence and marker boundary.
- `docs/app-model.md`
  - Documented the app-model readiness composition and non-launch boundary.
- `docs/completion-gates.md`
  - Recorded the host-only evidence without changing gate status.
- `done/step613.md`
  - Recorded this step.

## Tests run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /nodeReuse:false
dotnet run --project .\artifacts\verify\step609\focused\NativeAppPackageStagingReadinessFocused.csproj --nologo /p:OSWorkloadProfile=SingleMinimalApi /nodeReuse:false
.\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe | Select-String -Pattern 'TEST_FAIL|Unhandled|Exception|appmodel package staging readiness|host\.tests'
```

## Evidence

- Host-test project build completed with 0 warnings and 0 errors.
- Focused verifier emitted:
  - `TEST_PASS appmodel package staging readiness composes`
  - `TEST_PASS appmodel package staging readiness rejects selection failures`
  - `TEST_PASS appmodel package staging readiness rejects staging failures`
  - `TEST_PASS appmodel package staging readiness rejects memory invocation failures`
  - `TEST_PASS appmodel package staging readiness rejects byte mismatches`
  - `TEST_PASS appmodel package staging readiness preserves no-launch boundary`
  - `TEST_PASS appmodel package staging readiness default result is not success`
  - `TEST_PASS appmodel package staging readiness focused`

## Failures

- The full host-test executable is currently blocked by unrelated CI-smoke and
  libvirt smoke failures in the shared dirty worktree. The filtered run emitted
  `TEST_FAIL ci.smoke returns nonzero on failed command`, several nested
  `ci.smoke.*`/`libvirt.smoke.generate_xml` failures, and then threw
  `InvalidOperationException: CI smoke script tests failed: 1` before a global
  `TEST_PASS host.tests`.

## Deferred work

- No live virtio-blk read, boot-time package load, boot-owned staging
  allocation, executable page mapping, page-permission change, relocation,
  stack allocation, app ABI dispatch, native app execution, hosted .NET, or
  Kestrel behavior is implemented by this slice.
