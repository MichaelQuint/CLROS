# Step 306 - Baseline CI qcow2/libvirt Smoke Intent

## What Changed

- Wired qcow2 smoke intent into `scripts/run-ci-smoke.ps1` and `scripts/run-baseline-evidence.ps1`.
- Wired libvirt dry-run and deterministic missing-`virsh` evidence into the same baseline/CI evidence paths.
- Added artifact-plan metadata for qcow2 image paths, disk format, serial logs, summary logs, and libvirt domain XML files.
- Added host tests that validate the CI dry-run artifact plan and baseline script wiring without requiring QEMU, `qemu-img`, `virsh`, or a libvirt daemon.
- Updated the QEMU, libvirt, image-builder, and completion-gate docs to describe this as smoke intent unless the explicit qcow2 real-run switch is used.

## Why It Changed

Issue #306 needed the new qcow2 and libvirt wrapper evidence to show up in the routine baseline/CI evidence flow. The default path must remain host-tolerant, so it records command/marker/artifact intent instead of requiring optional virtualization tools.

## Files Changed

- `scripts/run-ci-smoke.ps1`
- `scripts/run-baseline-evidence.ps1`
- `tests/OS.HostTests/CiSmokeScriptTests.cs`
- `docs/qemu.md`
- `docs/libvirt.md`
- `docs/image-builder.md`
- `docs/completion-gates.md`
- `done/step306.md`

## Evidence

Build:

```powershell
dotnet build .\OS.slnx --nologo -m:1
```

Result: passed with 0 warnings and 0 errors.

Integrated host tests:

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

Result: emitted `TEST_PASS host.tests`.

Focused #306 host-test markers included:

```text
TEST_PASS ci.smoke.dryrun includes qcow2 smoke intent
TEST_PASS ci.smoke.dryrun includes libvirt smoke intent
TEST_PASS ci.smoke returns nonzero on failed command
TEST_PASS baseline evidence script wires qcow2 and libvirt intent
```

Safe CI dry-run command:

```powershell
.\scripts\run-ci-smoke.ps1 -DryRun -LogRoot artifacts\logs\ci-smoke-step306-dryrun
```

Observed markers:

```text
TEST_START ci.smoke.dryrun
WORKLOAD_PROFILE SingleMinimalApi
TEST_PASS workload.profile.tooling default single
TEST_PASS ci.smoke.dryrun writes artifact plan
TEST_PASS ci.smoke.dryrun includes serial and summary logs
TEST_PASS ci.smoke.dryrun includes qcow2 smoke intent
TEST_PASS ci.smoke.dryrun includes libvirt smoke intent
TEST_PASS ci.smoke.dryrun
```

The generated `artifacts\logs\ci-smoke-step306-dryrun\ci-smoke.artifacts.json` includes:

- `ci.smoke.qemu.qcow2.dryrun` using `scripts\run-qemu-qcow2-smoke.ps1 -DryRun`.
- `ci.smoke.libvirt.dryrun` using `scripts\run-libvirt-smoke.ps1 -DryRun`.
- `ci.smoke.libvirt.missingtool` using `scripts\run-libvirt-smoke.ps1 -Virsh <evidence-dir>\missing-virsh`.
- `qemu-qcow2-serial.log`, `qemu-qcow2.summary.log`, `libvirt-dryrun.xml`, and `libvirt-missing-tool.xml` artifact paths.

Whitespace check:

```powershell
git diff --check
```

Result: exited 0. It reported only existing LF-to-CRLF normalization warnings.

## Failures

- The first integrated host-test rerun exited during `ci.smoke returns nonzero on failed command`. Manual script reproduction completed correctly with `TEST_FAIL ci.smoke failures=2`, so the failure was isolated to the test helper.
- Fixed `tests/OS.HostTests/CiSmokeScriptTests.cs` to start asynchronous stdout/stderr drains before waiting on the process. The full host-test run then passed.

## Deferred Work

- This does not add repository CI workflow files.
- This does not run real libvirt domains, poll libvirt serial logs, or clean up transient libvirt domains.
- Default qcow2 coverage remains dry-run intent. Use `-RunQcow2RealSmoke` with valid `-Qemu`, `-QemuImg`, and `-OvmfCode` inputs to collect configured real qcow2 evidence.
- The libvirt missing-tool lane intentionally proves skip/missing-tool reporting only; it is not boot evidence.

## Evidence Boundary

This step improves baseline/CI evidence collection. It does not change the kernel boot path, image format, QEMU runner behavior, libvirt real-run behavior, cloud import readiness, hosted .NET 10, Minimal API, or Kestrel status.
