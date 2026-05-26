# Step 706 - Native App Argument Vector Diagnostic Formatter

## Summary

Added a caller-owned ASCII diagnostic formatter for native app argument-vector
planning results. The formatter emits the stable `APP_ARGS_VECTOR status=`
marker with bounded metadata only: planner status, initialized state, argument
count, maximum argument count, maximum argument length, first-argument
offset/length metadata, and explicit `appExecuted=false` /
`kestrelStarted=false` flags.

## Why

The native app model already had host-tested argument-vector planning, but it
did not have a compact diagnostic marker that future boot/app-launch gates can
log without copying argument bytes or implying app execution. This slice adds
that marker boundary while preserving the existing no-execution and no-Kestrel
claim limits.

## Files changed

- `src/OS.Kernel/Manifest/NativeAppArgumentVectorDiagnosticFormatter.cs`
- `tests/OS.HostTests/NativeAppArgumentVectorPlanTests.cs`
- `docs/app-model.md`
- `docs/manifest.md`
- `docs/completion-gates.md`
- `done/step706.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build -- "appmodel args vector formatter|host.tests|TEST_FAIL"`
- `git diff --check`

## Evidence

- Formatter marker shape:
  - `APP_ARGS_VECTOR status=Success initialized=true argumentCount=<count> maxArgumentCount=16 maxArgumentLength=128 firstArgumentPresent=<true|false> firstArgumentOffset=<offset|-1> firstArgumentLength=<length|0> appExecuted=false kestrelStarted=false`
- Added host-test markers:
  - `TEST_PASS appmodel args vector formatter formats success marker`
  - `TEST_PASS appmodel args vector formatter reports short destination`
  - `TEST_PASS appmodel args vector formatter rejects default result`
- After `dotnet build-server shutdown` released idle MSBuild node handles, the
  requested host-test project build passed with 0 warnings and 0 errors.
- `git diff --check` exited 0. It printed existing CRLF normalization warnings
  for many concurrently edited files, but no whitespace errors.

## Failures

- The first build attempt failed because another parallel `OS.HostTests`
  process held `tests/OS.HostTests/bin/Debug/net10.0/OS.Kernel.dll` and
  `OS.Std.NoRuntime.Shared.dll`.
- A second build attempt failed because an idle/concurrent MSBuild node held
  `tools/OS.QemuRunner/obj/Debug/net10.0/OS.QemuRunner.dll`; `dotnet
  build-server shutdown` cleared the handle.
- The requested focused host-test command did not reach the new
  argument-vector formatter markers. It failed first in the unrelated
  `runtime.nativeaot helper ledger structured columns` module initializer:
  the test expected the class-constructor coverage text to contain
  `preflight tests`, while the current ledger text says `NativeAOT cctor
  state/preflight/work-item tests cover prerequisite metadata; no cctor
  execution test exists.` Runtime/cctor files were intentionally left
  untouched for this issue.

## Deferred work

- The formatter does not pass arguments to app code, allocate argv strings,
  execute a native app, start hosted .NET, or start Kestrel.
- A future BootSequence/app-entry marker can consume this formatter once live
  package loading and native app invocation move beyond host-tested metadata.

