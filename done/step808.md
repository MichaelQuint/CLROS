# Step 808 - Allocation-free probe catalog

## Summary

Added a caller-buffer probe catalog over the existing `Probes.cs` compile-time
probe definitions and host tests for full enumeration and destination-capacity
failure reporting.

## Why

Diagnostics tooling needs a single value-only way to enumerate probe metadata
without allocating, duplicating each probe getter, or enabling destructive
probes. This keeps the boot/probe surface machine-readable while preserving
disabled-by-default behavior for panic, storage, and network probes.

## Files changed

- `src/OS.Kernel/Diagnostics/Probes.cs`
- `tests/OS.HostTests/DiagnosticsTests.cs`
- `docs/diagnostics.md`
- `docs/completion-gates.md`
- `done/step808.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Result: pass, 0 warnings, 0 errors.
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll`
  - Result: pass, emitted `TEST_PASS host.tests`.
- Scoped `git diff --check` plus trailing-whitespace scan for the changed
  files.
  - Result: pass; Git reported LF/CRLF conversion warnings only.

## Evidence

- `artifacts/logs/issue766-host-build.log` records the successful host build.
- `artifacts/logs/issue766-host-tests.log` records `TEST_PASS host.tests`.
- `artifacts/logs/issues762-766-diff-check.log` records
  `git-diff-check-exit=0`.
- `artifacts/logs/issues762-766-whitespace.log` records
  `TEST_PASS issues762_766.trailing_whitespace`.
- Verified markers:
  - `TEST_PASS probes enumerate all definitions`
  - `TEST_PASS probes catalog reports destination capacity`

## Failures

- None recorded yet.

## Deferred work

- Wire the catalog into any future serial/QEMU probe-reporting path once live
  IDT/fault diagnostics and post-firmware serial output are implemented.
- This is host-tested metadata evidence only. It does not execute destructive
  probes, prove null-pointer or invalid-opcode panic handling, access virtio
  devices, or advance cloud-readiness gates beyond documented host contracts.
