# Step 664 - Windows CI agent host prerequisites (#582)

## Summary

Documented the canonical Windows CI agent prerequisite checklist and non-boot
bring-up commands in `docs/host-requirements.md`. The new section records the
required Windows, .NET 10, Visual Studio Build Tools, Windows SDK, QEMU,
`qemu-img`, and OVMF inputs, then points CI agents at the existing marker
producing validation scripts.

## Why

Issue #582: after adding EFI/QEMU/qemu-img prerequisite markers, the repo needed
a concise source-of-truth section for CI agent setup. Gate 12 must stay honest:
the repository has local CI smoke scripts and documented agent prerequisites,
but no repository CI workflow yet.

## Files changed

- `docs/host-requirements.md`
  - Added `Windows CI Agent Bring-Up` with required agent capabilities,
    explicit tool variables, environment-variable fallback boundaries,
    non-boot validation commands, expected marker families, and optional real
    smoke commands.
  - Updated next steps so the remaining CI task is workflow creation and
    artifact upload.
- `docs/qemu.md`
  - Linked local CI smoke usage back to the canonical host requirements doc.
- `docs/completion-gates.md`
  - Updated Gate 12 evidence to mention documented Windows CI prerequisites
    while leaving the gate status at `Fail`.
- `done/step664.md`
  - This record.

## Tests run

- `.\scripts\build-efi-shell.ps1 -PreflightOnly`
- `.\scripts\run-ci-smoke.ps1 -DryRun`
- `.\scripts\run-baseline-evidence.ps1 -PlanOnly`
- `git diff --check`

## Evidence

- EFI preflight emitted:
  - `TEST_START efi.shell.publish`
  - `EFI_PREFLIGHT_START`
  - `EFI_PREFLIGHT_MSVC_LINKER C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Tools\MSVC\14.44.35207\bin\Hostx64\x64\link.exe`
  - `EFI_PREFLIGHT_WINDOWS_SDK C:\Program Files (x86)\Windows Kits\10\Lib\10.0.26100.0\um\x64\kernel32.Lib`
  - `EFI_PREFLIGHT_OK`
- CI smoke dry-run emitted:
  - `TEST_START ci.smoke.qemu_img.discovery`
  - `CI_SMOKE_QEMU_IMG_DISCOVERY requested=auto source=well-known available=true resolved=C:\Program Files\qemu\qemu-img.exe`
  - `CI_SMOKE_QCOW2_CONVERSION_INTENT mode=dry-run status=planned qemuImgRequired=false qemuImgAvailable=true`
  - `TEST_PASS ci.smoke.qemu_img.discovery`
  - `TEST_PASS ci.smoke.dryrun`
- Baseline plan-only emitted:
  - `TEST_START baseline.qemu_img.discovery`
  - `BASELINE_QEMU_IMG_DISCOVERY requested=auto source=well-known available=true resolved=C:\Program Files\qemu\qemu-img.exe`
  - `BASELINE_QCOW2_CONVERSION_INTENT mode=dry-run status=planned qemuImgRequired=false qemuImgAvailable=true`
  - `TEST_PASS baseline.qemu_img.discovery`
  - `TEST_PASS baseline.artifact_summary`
- `git diff --check` reported no whitespace errors; Git only printed existing
  LF-to-CRLF normalization warnings for edited files.

## Failures

- None.

## Deferred work

- Add repository CI workflow files that run the documented validation commands
  on a compatible Windows agent and upload logs/artifacts.
- This slice does not change scripts or run real QEMU/qcow2 smoke.

## Evidence boundary

This is documentation and host-prerequisite validation evidence only. It does
not prove repository CI, QEMU boot, qcow2 conversion, libvirt real boot,
cloud-provider import, hosted .NET, Minimal API, Kestrel, or cloud readiness.
