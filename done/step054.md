# Step 054 - QEMU Forbidden Marker Assertions

## What Changed

- Added `--forbid-marker <marker>` support to `OS.QemuRunner`.
- Added `-ForbidMarker` support to `scripts/run-qemu-smoke.ps1`.
- Added forbidden-marker reporting to dry-run output.
- Added `FORBIDDEN_MARKERS` and `FORBIDDEN_MARKER_HIT` fields to real-run summary logs.
- Added forbidden-marker failure output:
  - `OS_SMOKE_FAIL forbidden-marker=<marker>`
  - `TEST_FAIL qemu.smoke forbidden_marker marker=<marker>`
- Moved successful QEMU/OVMF discovery diagnostics to stdout and hardened the PowerShell wrapper around native command stderr/exit-code handling.

## Why It Changed

Issue #74 needed negative marker assertions so normal QEMU smoke can prove disabled firmware probes do not accidentally run. This is especially important for disabled-by-default probes such as the fresh-memory-map `ExitBootServices` probe.

## Files Changed

- `tools/OS.QemuRunner/Program.cs`
- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/qemu.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\run-qemu-smoke.ps1 -DryRun -ForbidMarker UEFI_EBS_FRESH_MAP_PROBE_START,UEFI_EBS_CALL_SKIPPED
.\scripts\run-qemu-smoke.ps1 -SerialLog artifacts/logs/qemu-smoke-forbidden.log -SummaryLog artifacts/logs/qemu-smoke-forbidden.summary.log -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -ForbidMarker UEFI_EBS_FRESH_MAP_PROBE_START,UEFI_EBS_CALL_SKIPPED
```

## Logs And Evidence

- Build passed with 0 warnings and 0 errors.
- Host tests emitted:
  - `TEST_PASS qemu runner accepts forbidden markers`
  - `TEST_PASS qemu runner fails when forbidden marker appears`
  - `TEST_PASS qemu runner keeps required marker failure distinct`
  - `TEST_PASS host.tests`
- Dry-run emitted `FORBIDDEN_MARKERS UEFI_EBS_FRESH_MAP_PROBE_START;UEFI_EBS_CALL_SKIPPED`.
- Real QEMU smoke emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke-forbidden.summary.log` reported:
  - `MISSING_MARKERS none`
  - `FORBIDDEN_MARKER_HIT none`
  - `FAIL_REASON none`

## Failures

- While verifying #70, `run-qemu-smoke.ps1 -BuildEfi` exposed wrapper issues around in-process script `exit` and native stderr handling. This step keeps the native exit-code handling explicit and prevents successful discovery diagnostics from surfacing as PowerShell error-looking records.

## Deferred Work

- Forbidden markers are host-tooling assertions only. They do not replace probe defaults or kernel-side guard checks.
- CI still needs to run this smoke path and store summary artifacts automatically.

## Evidence Boundary

This step proves QEMU smoke can reject forbidden serial markers and can prove disabled firmware probes are absent on the normal boot path. It does not alter kernel boot behavior and does not prove real `ExitBootServices`.
