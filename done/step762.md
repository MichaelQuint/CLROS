# Step 762 - Package staging readiness formatter shared ASCII helpers

## Summary
Migrated `NativeAppPackageStagingReadinessFormatter` to the shared
`OS.Std.Text.AsciiDecimalFormatter` and `OS.Std.Text.AsciiTextBuilder` helpers.
The package-staging not-ready marker remains byte-compatible with the existing
`APP_PACKAGE_STAGING_NOT_READY ...` shape while removing the formatter-local
unsigned decimal count/write loops.

## Why
The native app staging path is part of the CloudReady app-model handoff. Its
diagnostic formatting should use the same no-runtime ASCII helper surface as
the rest of the kernel-facing diagnostics so decimal formatting behavior is
centralized and host-guarded.

## Files changed
- `src/OS.Kernel/Runtime/NativeAppPackageStagingReadinessFormatter.cs`
  now uses `AsciiDecimalFormatter.CountUInt64Digits(...)` for byte-count
  planning and checked `AsciiTextBuilder` append calls for marker writes.
- `tests/OS.HostTests/NativeAppPackageStagingFormatterTests.cs` adds
  `TEST_PASS appmodel package staging formatter uses shared ascii text builder`
  as a source guard against reintroducing local decimal loops.
- `docs/app-model.md` documents the shared helper dependency and preserves the
  no-live-storage/no-app-launch boundary.
- `docs/nativeaot-nostdlib-limits.md` records the new shared-builder guard
  marker under the no-runtime text helper evidence.
- `done/step762.md` records this implementation and evidence.

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue718-package-staging-formatter\bin\Debug\net10.0`
- `dotnet .\artifacts\issue718-package-staging-formatter\bin\Debug\net10.0\OS.HostTests.dll`
- `git diff --check`

## Evidence
- Build log:
  `artifacts/logs/issue718-host-build-20260525.log`
- Host test log:
  `artifacts/logs/issue718-hosttests-20260525.log`
- Diff-check log:
  `artifacts/logs/issue718-git-diff-check-20260525.log`
- Focused markers:
  - `TEST_PASS appmodel package staging formatter formats not-ready marker`
  - `TEST_PASS appmodel package staging formatter reports short destination`
  - `TEST_PASS appmodel package staging formatter formats default readiness`
  - `TEST_PASS appmodel package staging formatter rejects invalid status`
  - `TEST_PASS appmodel package staging formatter default result is not success`
  - `TEST_PASS appmodel package staging formatter uses shared ascii text builder`
  - `TEST_PASS host.tests`
- `git diff --check` exited 0. It emitted existing LF-to-CRLF working-copy
  warnings only; no whitespace errors were reported.

## Failures
- None in this focused host-tested slice.
- No QEMU smoke was run because this changes only host-tested diagnostic
  formatting over already-produced app staging readiness metadata.

## Deferred work
- Connect the package staging readiness path to live boot-owned package reads.
- Allocate boot-owned staging memory, map executable pages, perform W^X/NX
  transitions, relocate/call app code, and observe real app exit/log output.
- Continue migrating remaining diagnostic formatters that still carry local
  integer formatting loops.

## Evidence boundary
This step proves caller-owned byte-buffer formatting for already-produced
native app package-staging readiness results. It does not perform live storage
I/O, allocate staging memory, map pages, change permissions, relocate code,
launch an app, start hosted .NET, start Kestrel, or provide cloud-provider
evidence.

