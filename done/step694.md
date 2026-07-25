# Step 694 - NativeAOT Cctor Runner Preflight BootSequence Marker

## Summary
- Added a managed `BootSequence` readiness boundary for the existing NativeAOT
  class-constructor runner preflight result.
- Added stable `NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_READY` and
  `NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_NOT_READY` marker prefixes with compact
  metadata for readiness, initialization, preflight status, type/target counts,
  first problem index/state/failure code, and deferred invocation flags.
- Added host tests for ready, failed/default result handling, and default boot
  marker stability.
- Updated boot, NativeAOT/no-stdlib, and completion-gate docs.

## Why
The Phase D runtime slice needs the boot-order source of truth to expose the
cctor runner preflight as an explicit readiness marker before any future
runtime work can consider constructor invocation. This preserves the current
boundary: no constructors are executed, no metadata is discovered, no delegates
are invoked, and no CloudReady/runtime-helper completion is claimed.

## Files changed
- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/NativeAotClassConstructorStateTableTests.cs`
- `docs/boot.md`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- `done/step694.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern "cctor runner preflight|boot.sequence cctor runner|host.tests|TEST_FAIL"`
- `git diff --check -- src/OS.Kernel/Boot/BootSequence.cs tests/OS.HostTests/NativeAotClassConstructorStateTableTests.cs docs/boot.md docs/nativeaot-nostdlib-limits.md docs/completion-gates.md done`

## Evidence
- Focused host-test build passed with 0 errors. MSBuild reported copy-retry
  warnings because an existing `OS.HostTests` process held output DLLs, but the
  build completed successfully.
- The filtered host-test command exited 0 and emitted:
  - `TEST_PASS runtime.cctor runner preflight ready`
  - `TEST_PASS runtime.cctor runner preflight rejects default table`
  - `TEST_PASS runtime.cctor runner preflight rejects failed state`
  - `TEST_PASS runtime.cctor runner preflight rejects pending state`
  - `TEST_PASS runtime.cctor runner preflight rejects missing metadata`
  - `TEST_PASS runtime.cctor runner preflight formatter reports short destination`
  - `TEST_PASS boot.sequence cctor runner preflight ready`
  - `TEST_PASS boot.sequence cctor runner preflight rejects failed result`
  - `TEST_PASS boot.sequence cctor runner preflight rejects default result`
  - `TEST_PASS boot.sequence cctor runner preflight preserves default markers`
  - `TEST_PASS host.tests`
- Targeted `git diff --check` passed. Git printed line-ending normalization
  warnings for modified docs/source files only.

## Failures
- None for this slice.

## Deferred work
- Wire the cctor runner preflight into the live boot path only after the
  runtime metadata source, constructor runner, failure diagnostics, and
  invocation policy have separate evidence.
- Do not claim constructor invocation, module initialization, hosted .NET,
  Kestrel, CloudReady, or production readiness from this marker.

