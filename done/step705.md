# Step 705 - NativeAOT Class Constructor Work-Item Plan Metadata

## Summary
Added a host-tested class-constructor work-item planning slice for the
NativeAOT runtime helper ledger. The new planner consumes an already-successful
`NativeAotClassConstructorRunnerPreflightResult`, caller-owned state entries,
caller-supplied target metadata, and caller-owned work-item storage, then
records which uninitialized type entries would need future constructor
dispatch.

## Why
Class constructor execution is still deferred, but the next runtime-correctness
slice needed a deterministic metadata boundary before any actual cctor runner
can be wired into boot/runtime code. This step proves the pre-execution plan can
reject failed, pending, malformed, or undersized metadata without using
reflection, managed delegates, exception control flow, metadata discovery, or
hidden allocation.

## Files changed
- `src/OS.Kernel/Runtime/NativeAotClassConstructorWorkItemPlan.cs`
- `src/OS.Kernel/Runtime/NativeAotHelperLedger.cs`
- `tests/OS.HostTests/NativeAotClassConstructorWorkItemPlanTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/boot.md`
- `docs/completion-gates.md`
- `done/step705.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence
- Full host-test log: `artifacts\logs\hosttests-issue630-20260525-054250.log`
- The final full host-test run exited 0 and emitted `TEST_PASS host.tests`.
- Runtime/cctor work-item markers emitted:
  - `TEST_PASS runtime.cctor work item plans uninitialized entries`
  - `TEST_PASS runtime.cctor work item skips initialized entries`
  - `TEST_PASS runtime.cctor work item rejects missing target token`
  - `TEST_PASS runtime.cctor work item rejects missing target metadata`
  - `TEST_PASS runtime.cctor work item reports destination capacity`
  - `TEST_PASS runtime.cctor work item rejects failed preflight`
  - `TEST_PASS runtime.cctor work item rejects pending preflight`
  - `TEST_PASS runtime.cctor work item rejects default preflight`
  - `TEST_PASS runtime.cctor work item layouts contain no managed references`
- The helper ledger marker `TEST_PASS runtime.nativeaot helper ledger
  structured columns` passed after updating the class-constructor entry to
  mention state, preflight, and work-item metadata coverage.

## Failures
- A standard build attempt was blocked by a concurrently running
  `OS.HostTests.exe` that held the default `bin\Debug\net10.0` output files.
- A custom-output full host-test attempt was rejected as evidence because some
  image-builder tests derive tool paths from the standard build layout.
- The first standard full host-test run failed in
  `runtime.nativeaot helper ledger structured columns` because the ledger
  coverage text no longer contained the exact phrase `preflight tests`. The
  wording was corrected and the same standard build/full host-test path passed.

## Deferred work
- This step does not invoke class constructors, run module initializers,
  discover NativeAOT metadata, dispatch through delegates, support lazy static
  reference fields, wire cctor planning into `BootSequence.Run`, or emit a live
  QEMU cctor marker.
- Full class constructor execution, failure unwinding, cctor cycles, and
  integration with static reference field initialization remain deferred runtime
  correctness work.

