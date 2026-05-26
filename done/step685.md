# Step 685 - NativeAOT Cctor State Diagnostic Formatter

## Summary

Added caller-owned diagnostic formatting for
`NativeAotClassConstructorStateTableResult` values. The formatter produces the
stable `NATIVEAOT_CCTOR_STATE status=` marker text, reports required byte
counts, rejects undersized destinations without writing partial output, and
rejects default/uninitialized table results.

## Why

The class-constructor state table can now preserve value-type transition state,
but future boot/runtime diagnostics need a deterministic byte-buffer formatter
before a real cctor runner exists. This keeps the next runtime slice
allocation-free and status-coded while still making cctor state transitions easy
to report over serial or host logs.

## Files changed

- `src/OS.Kernel/Runtime/NativeAotClassConstructorStateDiagnosticFormatter.cs`
  - Added `NativeAotClassConstructorStateDiagnosticFormatter`,
    `NativeAotClassConstructorStateDiagnosticFormatStatus`, and
    `NativeAotClassConstructorStateDiagnosticFormatResult`.
  - Formats status, module/type IDs, previous/current state, failure code, slot
    indexes, occupied count, and capacity into caller-owned ASCII buffers.
- `tests/OS.HostTests/NativeAotClassConstructorStateTableTests.cs`
  - Added focused formatter coverage for success, short destination, and
    default-result rejection.
- `docs/nativeaot-nostdlib-limits.md`
  - Documented the formatter as host-tested cctor diagnostic evidence below the
    full cctor-runner boundary.
- `docs/completion-gates.md`
  - Updated Gate 4 evidence to include cctor state diagnostic formatting while
    keeping the gate failed.
- `done/step685.md`
  - Recorded this evidence boundary.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern "runtime\.cctor state formatter|host\.tests|TEST_FAIL"`

## Evidence

- Host-test build passed with 0 warnings and 0 errors.
- Filtered host-test run exited 0, reached `TEST_PASS host.tests`, and emitted:
  - `TEST_PASS runtime.cctor state formatter formats success marker`
  - `TEST_PASS runtime.cctor state formatter reports short destination`
  - `TEST_PASS runtime.cctor state formatter rejects default result`

## Failures

- None observed in the #607 validation commands.

## Deferred work

- The formatter does not run constructors, discover NativeAOT metadata, invoke
  delegates, manage lazy static reference fields, unwind exceptions, or export
  runtime helper symbols.
- Gate 4 remains failed until allocation helpers, GC/root scanning, interface
  dispatch, a real cctor runner, exceptions, thread statics, and runtime-helper
  probes are implemented and proven.

## Evidence boundary

This is host-tested diagnostic-formatting evidence over already-produced cctor
state-table results. It does not prove a full NativeAOT class-constructor
runner, module initialization, QEMU runtime behavior, hosted .NET 10, Kestrel,
or cloud readiness.

