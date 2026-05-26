# Step 758 - Native app exit observer diagnostics use shared ASCII formatting

## Summary
Migrated the native app exit observer diagnostic formatter onto the shared
`OS.Std.Text.AsciiDecimalFormatter` and `OS.Std.Text.AsciiTextBuilder` helpers.
The formatter still emits the same `APP_EXIT_OBSERVER status=` marker and the
same status-coded short-destination/default-result behavior, but its decimal
byte counts and writes no longer carry app-model-local integer formatting loops.

## Why
CloudReady diagnostic formatting should converge on the no-runtime shared
ASCII/text surface before app-model and supervisor code grows larger. Keeping
the observer marker on the shared path reduces duplicate unsafe-friendly byte
formatting code and gives future supervisor/log ingestion work one tested
numeric formatting contract.

## Files changed
- `src/OS.Kernel/Runtime/NativeAppExitLogObserverDiagnosticFormatter.cs`
  now uses `AsciiDecimalFormatter` for required decimal byte counts and
  `AsciiTextBuilder` for byte, signed decimal, and unsigned decimal writes.
- `tests/OS.HostTests/NativeAppExitLogObserverTests.cs` adds
  `TEST_PASS appmodel.exit observer formatter uses shared ascii text builder`
  as a source guard against reintroducing app-model-local decimal loops.
- `docs/app-model.md` documents the shared formatter path and the host-only
  evidence boundary.
- `docs/nativeaot-nostdlib-limits.md` records the new guard marker in the
  shared no-runtime text-helper evidence.
- `done/step758.md` records the implementation and verification evidence.

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue716-app-exit-observer\bin\Debug\net10.0`
- `dotnet .\artifacts\issue716-app-exit-observer\bin\Debug\net10.0\OS.HostTests.dll`
- `git diff --check`

## Evidence
- Build log:
  `artifacts/logs/issue716-host-build-20260525.log`
- Host test log:
  `artifacts/logs/issue716-hosttests-20260525.log`
- Focused markers from the host test log:
  - `TEST_PASS appmodel.exit observer formatter formats success marker`
  - `TEST_PASS appmodel.exit observer formatter reports short destination`
  - `TEST_PASS appmodel.exit observer formatter rejects default result`
  - `TEST_PASS appmodel.exit observer formatter uses shared ascii text builder`
  - `TEST_PASS host.tests`
- `git diff --check` returned exit code 0. It printed existing CRLF conversion
  warnings only; no whitespace errors were reported.

## Failures
- None in the focused host-tested slice.
- No QEMU smoke was run for this step because the change is a host-tested
  diagnostic formatter migration and does not alter the live EFI boot artifact.

## Deferred work
- Wire `NativeAppExitLogObserver` into a real app supervisor/log ingestion path.
- Observe a live guest app exit and expose that status through the QEMU/libvirt
  runner evidence path.
- Continue migrating remaining diagnostics-local integer formatting loops onto
  shared no-runtime text helpers.

## Evidence boundary
This step proves only caller-owned byte-buffer diagnostic formatting for
already-produced native app exit observer metadata. It does not run an app,
ingest live serial logs, start a supervisor, execute hosted .NET, start Kestrel,
or provide cloud-provider evidence.

