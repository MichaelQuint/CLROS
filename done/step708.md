# Step 708 - Native App Argument Vector BootSequence Marker

## Summary
Added a host-tested `BootSequence` readiness boundary for native app argument
vector planning. The new boundary emits `APP_ARGS_VECTOR_READY` or
`APP_ARGS_VECTOR_NOT_READY` from an already-produced
`NativeAppArgumentVectorPlanResult` without allocating process strings,
defining the final argv ABI, executing app code, or starting Kestrel.

## Why
The native-tier launch chain already had manifest argument-vector planning and
diagnostic formatting, but the boot-order source of truth did not expose an
ordered marker for that metadata. This step makes the future boot handoff
observable while keeping the evidence boundary honest and non-executing.

## Files changed
- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/NativeAppArgumentVectorPlanTests.cs`
- `docs/app-model.md`
- `docs/boot.md`
- `docs/completion-gates.md`
- `done/step708.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build -- "appmodel args vector|boot.sequence app args|host.tests|TEST_FAIL"`

## Evidence
- Host-test build passed with 0 warnings and 0 errors.
- Focused host-test output emitted:
  - `TEST_PASS appmodel args vector splits manifest args`
  - `TEST_PASS appmodel args vector preserves quoted spaces`
  - `TEST_PASS appmodel args vector accepts empty args`
  - `TEST_PASS appmodel args vector rejects too many args`
  - `TEST_PASS appmodel args vector rejects malformed quotes`
  - `TEST_PASS appmodel args vector rejects failed launch plan`
  - `TEST_PASS appmodel args vector default result is not success`
  - `TEST_PASS boot.sequence app args vector ready`
  - `TEST_PASS boot.sequence app args vector rejects failed result`
  - `TEST_PASS boot.sequence app args vector rejects default result`
  - `TEST_PASS boot.sequence app args vector preserves default markers`
  - `TEST_PASS host.tests`
- Ready marker shape preserves readiness status, initialized state, planner
  status, launch status, argument count, max argument count, max argument
  length, error offset, and explicit `executesApp=false` /
  `startsKestrel=false` flags.

## Failures
- None.

## Deferred work
- This is host-tested boot marker aggregation only. It does not wire argument
  vectors into the default boot path or live EFI artifact, allocate process
  strings, define the final native app ABI, pass argc/argv to app code, execute
  a native app, start hosted .NET, or start Kestrel.

