# Step 679 - Native APP_EXIT Marker Parser

## Summary

Added a kernel/app-model parser for bounded native app supervisor exit marker
records:

`APP_EXIT code=<0..255> status=OSHOST_STATUS_<NAME>`

The parser returns status-coded value results, rejects default/zero parse
results as success evidence, and preserves the no-live-launch boundary for the
current app-model slice.

## Why

Future supervisor log ingestion needs a kernel-owned parser for the same
machine-readable marker shape already produced by
`NativeAppExitStatusFormatter`. The existing QEMU runner parser remains a
host-tool behavior reference only; no host-tool parser code was moved into the
kernel.

## Files changed

- `src/OS.Kernel/Runtime/NativeAppExitStatus.cs`
  - Added `NativeAppExitParseStatus` and `NativeAppExitParseResult`.
  - Added byte-span parsing for the existing `APP_EXIT` formatter line shape.
  - Rejects malformed prefix/field order, malformed/out-of-range code, invalid
    status token, extra fields, truncated fields, and uninitialized parse
    results.
  - Infers launcher-failure-shaped formatter output only from the current
    `code=255` plus non-OK status convention because the marker has no explicit
    launcher-failure field.
- `tests/OS.HostTests/NativeAppExitStatusTests.cs`
  - Added round-trip coverage for existing formatter output.
  - Added malformed marker rejection coverage.
  - Added default parse-result rejection and no-managed-reference layout
    coverage.
- `docs/app-model.md`
  - Documented the APP_EXIT parser contract, launcher-failure inference
    boundary, host-only evidence, markers, and deferred supervisor ingestion.
- `docs/completion-gates.md`
  - Updated the native app exit marker evidence and limitation text.
- `done/step679.md`
  - Recorded this slice.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'appmodel\.exit status'`
- `git diff --check -- .\src\OS.Kernel\Runtime\NativeAppExitStatus.cs .\tests\OS.HostTests\NativeAppExitStatusTests.cs .\docs\app-model.md .\docs\completion-gates.md .\done`

## Evidence

- Focused host-test build passed after clearing stale host-test process locks.
- Full host-test run exited 0 and emitted `TEST_PASS host.tests`.
- Filtered host-test rerun observed:
  - `TEST_PASS appmodel.exit status formats success marker`
  - `TEST_PASS appmodel.exit status formats nonzero marker`
  - `TEST_PASS appmodel.exit status formats launcher failure`
  - `TEST_PASS appmodel.exit status rejects invalid input`
  - `TEST_PASS appmodel.exit status parser parses formatted marker`
  - `TEST_PASS appmodel.exit status parser rejects malformed marker`
  - `TEST_PASS appmodel.exit status create default result is not success`
  - `TEST_PASS appmodel.exit status marker default result is not success`
  - `TEST_PASS appmodel.exit status parser default result is not success`
  - `TEST_PASS appmodel.exit status writes marker to app log`
- Scoped `git diff --check` exited 0.

## Failures

- The first focused host-test build attempt failed with MSB3027/MSB3021 copy
  errors because stale `OS.HostTests.exe` processes held files under
  `tests/OS.HostTests/bin/Debug/net10.0`. After stopping those lock-holders, the
  build passed. One rerun reported transient copy retry warnings while another
  stale host-test process was cleared; final result was build success with
  zero errors.

## Deferred work

- Connect marker parsing to the future supervisor log ingestion path.
- Observe and parse a live app exit after the app launcher, executable mapping,
  app ABI, log routing, and supervisor state exist.
- Keep QEMU runner APP_EXIT summaries as host-side serial parsing evidence
  only until guest app execution is proven.

## Evidence boundary

This is host-tested parser/formatter evidence only. It does not prove a live
native app launch, live stdout/stderr routing, supervisor policy, restart
handling, executable mapping, hosted .NET 10, ASP.NET Minimal API, Kestrel, or
cloud readiness.
