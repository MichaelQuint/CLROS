# Step 284 - QEMU APP_EXIT Summary Parser

## What Changed

- Taught `SerialMarkerSummary` to treat exact `APP_EXIT` serial lines as observed marker lines.
- Added parsing for valid `APP_EXIT code=<n> status=<OSHOST_STATUS_*>` records.
- Added malformed app-exit reporting with status reasons such as `invalid_format`, `invalid_code`, and `invalid_status`.
- Added `APP_EXIT_RESULT` and `APP_EXIT_MALFORMED` fields to QEMU real-run summary artifacts.
- Kept required-marker and forbidden-marker pass/fail behavior independent from malformed `APP_EXIT` diagnostics.
- Added focused parser tests and a fake-QEMU process-level summary artifact test.
- Updated QEMU, app-model, and completion-gate documentation.

## Why It Changed

Future app launcher and hosted workload smoke paths need the host runner to
preserve app-exit marker evidence in summary artifacts. This step only teaches
the runner how to observe and summarize those serial lines; it does not change
runtime app lifecycle behavior.

## Files Changed

- `tools/OS.QemuRunner/SerialMarkerSummary.cs`
- `tools/OS.QemuRunner/Program.cs`
- `tests/OS.HostTests/SerialMarkerSummaryTests.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/qemu.md`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step284.md`

## Tests Run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
$output = dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build 2>&1; $exitCode = $LASTEXITCODE; $output | Select-String -Pattern 'qemu\.summary app exit|qemu\.runner app exit|qemu\.serial summary keeps required marker behavior unchanged|TEST_FAIL|host\.tests'; exit $exitCode
git diff --check -- tools/OS.QemuRunner/SerialMarkerSummary.cs tools/OS.QemuRunner/Program.cs tests/OS.HostTests/SerialMarkerSummaryTests.cs tests/OS.HostTests/QemuRunnerTests.cs docs/qemu.md docs/app-model.md docs/completion-gates.md done/step284.md
```

## Logs And Evidence

The focused host-test project build passed:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

The filtered host-test run exited 0 and emitted:

```text
TEST_PASS qemu.summary app exit marker parsed
TEST_PASS qemu.summary app exit absent reported
TEST_PASS qemu.summary app exit malformed reported
TEST_PASS qemu.runner app exit summary emitted
TEST_PASS qemu.serial summary keeps required marker behavior unchanged
TEST_PASS host.tests
```

No `TEST_FAIL` lines were emitted by the filtered run.

`git diff --check` passed for the issue-owned files. Git printed line-ending
normalization warnings only.

## Failures

No failures were observed in this slice.

## Evidence Boundary

This is host-runner parser and fake-QEMU process-level evidence only. It does
not prove a guest app ran, a live app exit was observed, app stdout/stderr was
routed to serial, runtime lifecycle changed, restart policy executed, hosted
.NET 10 launched, or Kestrel served traffic.

## Deferred Work

- Wire real app lifecycle exit capture only under a future runtime/app-launcher
  issue.
- Treat `APP_EXIT` as a required marker only in future smoke runs that actually
  own live app execution evidence.
