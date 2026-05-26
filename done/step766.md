# Step 766 - App launch-plan formatter shared ASCII helpers

## Summary
Migrated `NativeAppLaunchPlanDiagnosticFormatter` to the shared
`OS.Std.Text.AsciiDecimalFormatter` and `OS.Std.Text.AsciiTextBuilder`
helpers. The existing `APP_LAUNCH_PLAN status=` marker shape and
short-destination/default-result behavior remain unchanged while removing the
formatter-local decimal byte-count helpers, decimal append helpers, and direct
digit writes.

## Why
The launch-plan diagnostic is Phase G app-model host evidence over
already-produced manifest launch metadata. It should use the common
no-runtime-compatible ASCII formatting surface so integer formatting behavior
stays centralized and source-guarded before later app-model diagnostics reuse
the same helper path.

## Files changed
- `src/OS.Kernel/Manifest/NativeAppLaunchPlanDiagnosticFormatter.cs`
- `tests/OS.HostTests/NativeAppLaunchPlanDiagnosticFormatterTests.cs`
- `docs/app-model.md`
- `done/step766.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue723-launch-plan-formatter\bin\Debug\net10.0 *> .\artifacts\logs\issue723-host-build-20260525.log`
- `dotnet .\artifacts\issue723-launch-plan-formatter\bin\Debug\net10.0\OS.HostTests.dll *> .\artifacts\logs\issue723-hosttests-20260525.log`
- `git diff --check *> .\artifacts\logs\issue723-git-diff-check-20260525.log`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue723-launch-plan-formatter-rerun\bin\Debug\net10.0 *>&1 | Tee-Object -FilePath .\artifacts\logs\issue723-host-build-rerun-20260525.log`
- `dotnet .\artifacts\issue723-launch-plan-formatter-rerun\bin\Debug\net10.0\OS.HostTests.dll *>&1 | Tee-Object -FilePath .\artifacts\logs\issue723-hosttests-rerun-20260525.log`
- `git diff --check *>&1 | Tee-Object -FilePath .\artifacts\logs\issue723-git-diff-check-rerun-20260525.log`

## Evidence
- Build log: `artifacts/logs/issue723-host-build-20260525.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- Host test log: `artifacts/logs/issue723-hosttests-20260525.log`
  - `TEST_PASS appmodel launch diagnostic formats selected app`
  - `TEST_PASS appmodel launch diagnostic reports short destination`
  - `TEST_PASS appmodel launch diagnostic formats missing runtime`
  - `TEST_PASS appmodel launch diagnostic rejects default result`
  - `TEST_PASS appmodel launch plan formatter uses shared ascii text builder`
- Clean rerun host test log:
  `artifacts/logs/issue723-hosttests-rerun-20260525.log`
  - `TEST_PASS appmodel launch plan formatter uses shared ascii text builder`
  - `TEST_PASS host.tests`
- Clean rerun source check:
  `artifacts/logs/issue723-git-diff-check-rerun-20260525.log`
  exited 0 with only existing LF-to-CRLF working-copy warnings.
- Source guard evidence:
  - `NativeAppLaunchPlanDiagnosticFormatter.cs` uses
    `AsciiDecimalFormatter.CountInt32Digits(...)`.
  - `NativeAppLaunchPlanDiagnosticFormatter.cs` uses
    `AsciiTextBuilder.AppendBytes(...)` and
    `AsciiTextBuilder.AppendInt64Decimal(...)`.
  - The guard rejects formatter-local `GetInt32DecimalByteCount`,
    `GetUInt32DecimalByteCount`, `AppendInt32`, `AppendUInt32`, and direct
    digit/sign writes.

## Failures
- None in the clean rerun.
- The original full host-test DLL run exited nonzero because an unrelated QEMU
  script test failed in the shared dirty worktree:
  - `TEST_FAIL qemu smoke script dry-run propagates qcow2 disk format`
  - `TEST_FAIL qemu.runner.tests failures=1`
- The clean rerun of the full host-test DLL passed, including
  `TEST_PASS host.tests`, so the original QEMU dry-run failure is recorded as a
  transient/concurrent harness artifact rather than #723 evidence.
- The original redirected `git diff --check` command exited nonzero while the
  saved log contained only Git LF-to-CRLF working-copy warnings across the dirty
  tree. The clean rerun exited 0 with the same class of line-ending warnings and
  no issue-specific whitespace error.
- No QEMU smoke was run because this change only updates host-tested
  app-model diagnostic formatting over already-produced launch-plan metadata.

## Deferred work
- Keep migrating app-model diagnostics that still carry local integer
  formatting loops onto the shared ASCII helper surface.
- Wire launch-plan diagnostics into live boot/app-launch evidence only after
  live package reads, executable mapping, app ABI, stdout/stderr routing, and
  app execution have separate evidence.

## Evidence boundary
This step proves caller-owned byte-buffer formatting for already-produced
native app launch-plan results and source-guards use of the shared ASCII text
builder and decimal formatter. It does not read packages from live storage,
map executable pages, pass arguments to app code, start a native app, observe
stdout/stderr, capture an app exit, start hosted .NET, start Kestrel, or provide
QEMU/cloud-provider evidence.

