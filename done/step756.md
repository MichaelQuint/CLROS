# Step 756 - Native App Exit Marker Shared Formatting

## Summary
Migrated `NativeAppExitStatusFormatter` marker output onto the shared
no-runtime ASCII formatting helpers. The `APP_EXIT code=<n>
status=<OSHOST_STATUS_*>` path now uses `AsciiDecimalFormatter` for exit-code
byte counts and `AsciiTextBuilder` for marker fragments plus decimal exit-code
bytes instead of app-model-local decimal writing loops.

## Why
The native-tier app supervisor boundary needs stable machine-readable exit
markers, but app-model code should not carry one-off integer-to-ASCII
implementations when the shared `std/no-runtime/shared` formatter exists. This
keeps future launcher, serial, PAL, and QEMU summary paths aligned on the same
caller-owned ASCII formatting contract.

## Files changed
- `src/OS.Kernel/Runtime/NativeAppExitStatus.cs` now uses
  `OS.Std.Text.AsciiDecimalFormatter` and `OS.Std.Text.AsciiTextBuilder` for
  `APP_EXIT` marker formatting and removes the local decimal digit/write
  helpers.
- `tests/OS.HostTests/NativeAppExitStatusTests.cs` adds
  `TEST_PASS appmodel.exit status formatter uses shared ascii text builder` to
  guard the source dependency and preserve the existing marker bytes.
- `docs/app-model.md` documents the shared formatter usage and focused marker.
- `docs/nativeaot-nostdlib-limits.md` records the app-exit formatter guard
  under shared text-builder evidence.
- `done/step756.md` records this evidence.

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue714-app-exit-status\bin\Debug\net10.0`
- `dotnet .\artifacts\issue714-app-exit-status\bin\Debug\net10.0\OS.HostTests.dll`
- `git diff --check`

## Evidence
- Build log:
  `artifacts/logs/issue714-host-build-20260525.log`
- Host-test log:
  `artifacts/logs/issue714-hosttests-20260525.log`
- Diff-check log:
  `artifacts/logs/issue714-git-diff-check-20260525.log`
- Focused markers:
  - `TEST_PASS appmodel.exit status formats success marker`
  - `TEST_PASS appmodel.exit status formats launcher failure`
  - `TEST_PASS appmodel.exit status parser parses formatted marker`
  - `TEST_PASS appmodel.exit status formatter uses shared ascii text builder`
  - `TEST_PASS appmodel.exit status writes marker to app log`
  - `TEST_PASS host.tests`

## Failures
None in the focused build, full host-test harness, or `git diff --check`.
`git diff --check` emitted only existing LF/CRLF working-copy warnings.

## Deferred work
- Wire `APP_EXIT` marker production to a real native app supervisor once app
  payload mapping, stacks, ABI entry, log routing, and app exit capture exist.
- Keep parser and QEMU summary behavior aligned with the marker if future
  restart-policy or launcher-failure metadata becomes explicit.
- Keep hosted .NET 10, ASP.NET Minimal API, Kestrel, live package execution, and
  cloud-readiness claims blocked until the corresponding gates have evidence.

## Evidence boundary
This is host-tested marker formatting and parsing evidence only. It does not run
a native app, ingest live serial logs, map executable payloads, allocate stacks,
enter an app ABI, capture a live exit, start hosted .NET, start Kestrel, or
prove cloud-provider behavior.

