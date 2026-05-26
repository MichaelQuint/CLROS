# Step 623 - QEMU Summary Serial Tail

## Summary

Added a bounded serial-log tail to `OS.QemuRunner` real-run summary artifacts.
The summary now records the last eight non-empty serial lines, with each line
trimmed to 240 characters, alongside the existing missing/forbidden marker
diagnostics.

## Why

Failed QEMU smoke runs already reported required, missing, out-of-order, and
forbidden marker status, but triage still required opening the full serial log
to see the last firmware-visible lines. A small bounded tail keeps the summary
artifact useful for failures without changing the pass/fail contract or claiming
new boot evidence.

## Files changed

- `tools/OS.QemuRunner/Program.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/qemu.md`
- `done/step623.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `.\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe *> .\artifacts\logs\host-tests-step623.log`
- `.\scripts\check-naming.ps1`
- `git diff --check`
- `.\scripts\run-qemu-smoke.ps1 -DryRun`

## Evidence

- Host-test build completed with 0 warnings and 0 errors.
- Full host-test log: `artifacts/logs/host-tests-step623.log`.
- Machine-readable markers:
  - `TEST_PASS qemu runner fails when forbidden marker appears`
  - `TEST_PASS qemu real mode writes missing marker summary artifact`
  - `TEST_PASS qemu summary includes bounded serial tail`
  - `TEST_PASS host.tests`
  - `TEST_PASS naming.guard`
  - `TEST_PASS source.firewall`
  - `TEST_PASS qemu.runner.dryrun`
- `git diff --check` exited 0 with LF-to-CRLF normalization warnings only.

## Failures

- No compile, host-test, naming, source-firewall, dry-run, or whitespace
  failures were observed for this slice.

## Deferred work

- Keep real QEMU/libvirt/cloud boot claims tied to captured serial logs and
  required markers, not to this diagnostic summary tail.
- Extend libvirt real-run lifecycle only after the existing serial summary
  parser contract is wired into a real domain startup/cleanup path.

## Evidence boundary

This is host-tested QEMU runner summary behavior only. It does not add new real
QEMU boot proof, libvirt boot proof, cloud-provider import validation, hosted
.NET 10, Kestrel, storage, networking, or CloudReady gate completion.
