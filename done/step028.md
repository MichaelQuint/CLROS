# Step 028 - QEMU Smoke Summary Artifact

## What Changed

- Added optional `--summary-log <path>` to `tools/OS.QemuRunner`.
- Real-mode runs now write a concise summary artifact for pass and fail outcomes.
- Summary files use stable fields:
  - `QEMU_SMOKE_STATUS`
  - `IMAGE_ARTIFACT`
  - `SERIAL_LOG`
  - `REQUIRED_MARKERS`
  - `OBSERVED_MARKER`
  - `MISSING_MARKERS`
  - `FAIL_REASON`
- `OBSERVED_MARKER` is emitted once per observed marker-like serial line, or as `OBSERVED_MARKER none` when no marker-like serial lines were captured.
- Summary writes are best-effort. If writing fails, the runner emits `SUMMARY_LOG_WRITE_FAILED ...` and preserves the original smoke exit code.
- `scripts/run-qemu-smoke.ps1` now passes a default summary path: `artifacts/logs/qemu-smoke.summary.log`.
- Host tests cover dry-run behavior, pass summaries, missing-marker summaries, HTTP-probe rejection summaries, and summary write failure visibility without requiring real QEMU.

## Why It Changed

Serial logs are the authoritative boot evidence channel, but they can be long and noisy. A small summary artifact gives CI, agents, and humans a stable first place to inspect the outcome of a QEMU smoke run without replacing the full serial log.

## Files Changed

- `tools/OS.QemuRunner/Program.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `scripts/run-qemu-smoke.ps1`
- `docs/qemu.md`
- `done/step028.md`

## Tests Run

```powershell
dotnet build .\tools\OS.QemuRunner\OS.QemuRunner.csproj --nologo
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

- `dotnet build .\tools\OS.QemuRunner\OS.QemuRunner.csproj --nologo` passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo` passed with 0 warnings and 0 errors.
- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors after clearing a stale `OS.HostTests.exe` lock from a prior run.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` passed and emitted `TEST_PASS host.tests`.
- QEMU runner host tests emitted:
  - `TEST_PASS qemu dry-run does not write summary artifact`
  - `TEST_PASS qemu real mode rejects HTTP probe execution`
  - `TEST_PASS qemu real mode writes passing summary artifact`
  - `TEST_PASS qemu real mode writes missing marker summary artifact`
  - `TEST_PASS qemu summary write failure is visible`

## Failures

No #46 failures remain. A transient rerun used a stale `OS.HostTests` binary because `--no-build` was executed before rebuilding after a test-helper fix; the subsequent rebuild and host test run passed.

## Deferred Work

- Re-run real QEMU smoke on a host with QEMU/OVMF to produce a live `artifacts/logs/qemu-smoke.summary.log` next to the serial log.
- Add the summary artifact to CI upload once CI smoke packaging is wired.
- Extend the summary with HTTP probe result fields only after real host-side HTTP probing is implemented.

## Evidence Boundary

This step proves the runner summary contract, pass/fail summary generation, and write-failure handling with deterministic host tests. It does not add new guest boot behavior, QEMU firmware evidence, networking, or Kestrel execution.
