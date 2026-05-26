# Step 600 - Native App Payload Staging Boundary

## Summary

Added a host-tested native app payload staging contract. The new planner copies
only the selected executable bytes from already-loaded package bytes into a
caller-owned staging span after payload range planning, preserves nested mapping
status metadata, and rejects failed mapping plans, overflowed package ranges,
out-of-bounds package ranges, short destinations, and default/zero results.

## Why

The app model could already select the manifest-declared package app, validate
the `OSAPPv0` header, and compute executable range metadata. A future launcher
also needs a deterministic copy boundary before executable page mapping. This
slice proves that boundary without reading live storage, mapping pages, changing
permissions, relocating code, or executing the app.

## Files changed

- `src/OS.Kernel/Runtime/NativeAppPayloadStagingPlan.cs`
- `tests/OS.HostTests/NativeAppPayloadStagingPlanTests.cs`
- `src/OS.Kernel/Net/TcpEstablishedStream.cs`
  - Added a `scoped` annotation to `ReceiveSegment` to unblock current C# ref
    safety compilation. This is a build-blocker annotation only and does not
    change networking behavior.
- `docs/app-model.md`
- `docs/cstorage.md`
- `done/step600.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'TEST_FAIL|Unhandled|Exception|appmodel payload staging|host\.tests'`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo /p:OSWorkloadProfile=SingleMinimalApi /nodeReuse:false`

## Evidence

- Initial host-test build exposed an unrelated networking ref-safety compile
  blocker in `TcpEstablishedStream.ReceiveSegment`; the `scoped` parameter
  annotation fixed that blocker.
- Host-test project build then passed with 0 warnings and 0 errors.
- Focused host-test output included:
  - `TEST_PASS appmodel payload staging copies executable bytes`
  - `TEST_PASS appmodel payload staging rejects short destination`
  - `TEST_PASS appmodel payload staging rejects package range`
  - `TEST_PASS appmodel payload staging rejects mapping failure`
  - `TEST_PASS appmodel payload staging preserves no-execute boundary`
  - `TEST_PASS appmodel payload staging default result is not success`
- Rebuilding with `/p:OSWorkloadProfile=SingleMinimalApi /nodeReuse:false`
  passed with 0 warnings and 0 errors after shutting down stale MSBuild server
  processes.

## Failures

- A full host harness run without an explicit workload profile failed in
  pre-existing image-builder Kestrel package tests because the tool output
  reported `WORKLOAD_PROFILE MultiMinimalApi`; this is unrelated to the payload
  staging contract.
- A later full host harness run with `SingleMinimalApi` was interrupted by
  unrelated CI smoke/image-builder work after existing image-builder tests; it
  did not provide a clean `TEST_PASS host.tests` marker for this slice.

## Deferred work

- No live virtio-blk reads are performed.
- The staging planner consumes already-loaded package bytes only.
- No executable pages are allocated, mapped, protected, relocated, or executed.
- No app ABI, stack setup, scheduler integration, stdout/stderr routing, exit
  capture, hosted .NET, or Kestrel behavior is implemented.
