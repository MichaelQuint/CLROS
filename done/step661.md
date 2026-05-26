# Step 661 - QEMU smoke wrapper prerequisite preflight (#579)

## Summary

Added script-level prerequisite checks to `scripts/run-qemu-smoke.ps1` for real
QEMU smoke mode. Dry-run mode remains host-tool-free. Real mode now resolves
QEMU and OVMF before optional EFI/image build, validates the selected image and
disk format before launching `OS.QemuRunner`, and emits deterministic
machine-readable preflight markers and failure reasons.

## Why

Issue #579: missing QEMU, missing OVMF, missing/empty images, and raw/qcow2
mismatches should fail in the wrapper with a concise prerequisite reason before
the runner starts. That makes host setup failures distinct from OS boot, image,
or serial-marker failures.

## Files changed

- `scripts/run-qemu-smoke.ps1`
  - Added QEMU executable discovery for explicit paths, PATH entries, and common
    install locations.
  - Added OVMF code discovery for explicit paths, environment variables,
    QEMU-relative directories, and common install locations.
  - Added `TEST_START qemu.smoke.preflight`,
    `QEMU_SCRIPT_PREFLIGHT_QEMU`, `QEMU_SCRIPT_PREFLIGHT_OVMF`, and
    `TEST_PASS qemu.smoke.preflight`.
  - Added early failures for `qemu_not_found` and `ovmf_not_found` before the
    runner is invoked.
  - Added image artifact checks for missing files, zero-byte files, and
    raw/qcow2 format mismatches with `QEMU_SCRIPT_PREFLIGHT_IMAGE` and
    `QEMU_SCRIPT_PREFLIGHT_DISK_FORMAT`.
- `tests/OS.HostTests/QemuRunnerTests.cs`
  - Added script-level tests that use fake paths/tools to prove real mode fails
    before the runner for missing QEMU, missing OVMF, and missing image
    artifacts.
- `docs/qemu.md`
  - Documented the wrapper preflight behavior, markers, failure reasons, tests,
    and evidence boundary.
- `docs/host-requirements.md`
  - Documented real-smoke prerequisite failures and added
    `TEST_PASS qemu.smoke.preflight` to expected real-smoke success markers.
- `scripts/AGENTS.override.md`
  - Added QEMU wrapper rules for dry-run preservation, real-mode preflight
    markers, concise failures, and no environment/secret dumps.
- `done/step661.md`
  - This record.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\run-qemu-smoke.ps1 -DryRun`
- `git diff --check`

## Evidence

- Focused host build passed with 0 warnings and 0 errors.
- Host tests passed and emitted:
  - `TEST_PASS qemu smoke script real mode reports missing qemu prerequisite`
  - `TEST_PASS qemu smoke script real mode reports missing ovmf prerequisite`
  - `TEST_PASS qemu smoke script real mode reports missing image before runner`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `.\scripts\run-qemu-smoke.ps1 -DryRun` passed and emitted
  `TEST_PASS qemu.runner.dryrun`, confirming dry-run still does not require
  QEMU, OVMF, or an image.
- `git diff --check` reported no whitespace errors; Git only printed existing
  LF-to-CRLF normalization warnings for edited files.
- Manual missing-QEMU probe during development emitted
  `TEST_START qemu.smoke.preflight`,
  `QEMU_SCRIPT_PREFLIGHT_QEMU ... result=not_found`,
  `TEST_FAIL qemu.smoke.preflight qemu_not_found`, and
  `TEST_FAIL qemu.smoke qemu_not_found`, then exited before
  `TEST_START qemu.runner`.

## Failures

- One development host-test run exposed a PowerShell null-to-empty-string
  fallthrough for missing tool paths; the wrapper reached the runner instead of
  failing in preflight. The preflight now uses
  `[string]::IsNullOrWhiteSpace(...)` for missing path detection, and the final
  host test run passed.

## Deferred work

- Add baseline evidence `qemu-img` discovery diagnostics (#580) so qcow2
  conversion intent reports host prerequisites before conversion attempts.
- Run a real QEMU/OVMF smoke on a host with QEMU installed to capture
  `TEST_PASS qemu.smoke.preflight`, `TEST_PASS qemu.smoke`, and serial marker
  evidence. Dry-run remains non-boot proof.

## Evidence boundary

This slice proves wrapper-level prerequisite diagnostics only. It does not
prove QEMU boot, generated image correctness beyond basic file shape,
`ExitBootServices`, live storage, live networking, hosted .NET, Minimal API, or
Kestrel.
