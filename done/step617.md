# Step 617 - Baseline Evidence Workload Profile Propagation

## Summary

Patched the baseline evidence script so its libvirt dry-run and missing-tool
intent lanes receive the selected workload profile, matching the CI smoke
script behavior already used by QEMU and qcow2 lanes.

## Why

The cloud-tooling evidence path should not silently fall back to the default
profile when a caller runs baseline evidence for `MultiMinimalApi` or another
validated profile. This remains host-tooling intent evidence only; it does not
start QEMU or libvirt.

## Files changed

- `scripts/run-baseline-evidence.ps1`
- `tests/OS.HostTests/CiSmokeScriptTests.cs`
- `docs/qemu.md`
- `docs/libvirt.md`
- `docs/image-builder.md`
- `docs/completion-gates.md`
- `done/step617.md`

## Tests run

- PowerShell parser check for `scripts/run-baseline-evidence.ps1`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 -c Step617 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `.\tests\OS.HostTests\bin\Step617\net10.0\OS.HostTests.exe *> .\artifacts\logs\host-tests-step617.log`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

The first default-configuration host-test build attempt was blocked by an
out-of-scope file lock in `tools/OS.QemuRunner\obj\Debug\net10.0`, so validation
used an isolated `Step617` build configuration to avoid disturbing other
workers. The isolated host-test build passed with 0 warnings and 0 errors.

The parser check emitted:

```text
TEST_PASS baseline.evidence.script parses
```

`artifacts/logs/host-tests-step617.log` emitted:

```text
TEST_PASS ci.smoke.dryrun includes qcow2 smoke intent
TEST_PASS ci.smoke.dryrun includes libvirt smoke intent
TEST_PASS ci.smoke.dryrun records default workload profile
TEST_PASS ci.smoke.dryrun propagates multi workload profile
TEST_PASS baseline evidence script wires qcow2 and libvirt intent
TEST_PASS host.tests
```

`scripts/check-naming.ps1` emitted:

```text
TEST_PASS naming.guard
TEST_PASS source.firewall
```

`git diff --check` passed with line-ending normalization warnings only.

## Failures

The default `Debug` host-test build did not complete because a pre-existing
MSBuild/dotnet process held
`tools/OS.QemuRunner\obj\Debug\net10.0\OS.QemuRunner.dll`. This was treated as a
local verification blocker, not a code failure, and was bypassed with the
isolated `Step617` configuration.

## Deferred work

- Real libvirt lifecycle execution remains unimplemented.
- Baseline evidence still performs build/test/QEMU dry-run and intent capture;
  there is no dedicated no-build dry-run mode for `run-baseline-evidence.ps1`.
- This step does not add cloud-provider validation, ISO output, or live boot
  proof.

## Evidence Boundary

This is host-tooling command propagation and documentation evidence only. Dry-run
and intent artifacts are not QEMU boot proof, not libvirt boot proof, and do not
satisfy the CloudReady image or hosted-workload completion gates.
