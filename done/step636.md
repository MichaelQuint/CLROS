# Step 636 - Diagnostics Readiness Chain

## Summary

Added a host-tested diagnostics-chain readiness aggregate for Issue #2. The
new chain composes the current COM1 backend, descriptor-table install,
controlled fault-probe, CPU fault panic, and diagnostic stub materialization
contracts into one status-coded result and one boot-sequence marker helper.

## Why

The diagnostics backlog had individual readiness contracts but no single
ordered gate that proves the prerequisites are coherently chained before a
future live IDT/GDT/TSS/IST install and controlled fault probe. This step keeps
the work host-testable and explicit without claiming live CPU fault handling.

## Files changed

- `src/OS.Kernel/Diagnostics/DiagnosticsChainReadiness.cs`
- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/DiagnosticsChainReadinessTests.cs`
- `tools/OS.QemuRunner/SmokeMarkerPreset.cs`
- `tools/OS.QemuRunner/Program.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `scripts/run-qemu-smoke.ps1`
- `docs/diagnostics.md`
- `docs/qemu.md`
- `docs/completion-gates.md`
- `done/step636.md`

## Tests run

- `dotnet build .\tools\OS.QemuRunner\OS.QemuRunner.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset diagnostics`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check -- src/OS.Kernel/Diagnostics/DiagnosticsChainReadiness.cs src/OS.Kernel/Boot/BootSequence.cs tools/OS.QemuRunner/SmokeMarkerPreset.cs tools/OS.QemuRunner/Program.cs scripts/run-qemu-smoke.ps1 tests/OS.HostTests/DiagnosticsChainReadinessTests.cs tests/OS.HostTests/QemuRunnerTests.cs docs/diagnostics.md docs/qemu.md docs/completion-gates.md done/step636.md`

## Evidence

- `artifacts/logs/host-tests-step636-final.log` contains:
  - `TEST_PASS diagnostics.chain readiness accepts complete chain`
  - `TEST_PASS diagnostics.chain readiness rejects serial backend failure`
  - `TEST_PASS diagnostics.chain readiness rejects descriptor failure`
  - `TEST_PASS diagnostics.chain readiness rejects fault probe failure`
  - `TEST_PASS diagnostics.chain readiness rejects panic failure`
  - `TEST_PASS diagnostics.chain readiness rejects stub materialization failure`
  - `TEST_PASS diagnostics.chain readiness default result is not success`
  - `TEST_PASS diagnostics.chain readiness result contains no managed references`
  - `TEST_PASS boot.sequence diagnostics chain emits ready marker`
  - `TEST_PASS boot.sequence diagnostics chain emits blocked marker`
  - `TEST_PASS boot.sequence diagnostics chain leaves default markers unchanged`
  - `TEST_PASS qemu.smoke.preset diagnostics adds readiness chain markers`
  - `TEST_PASS host.tests`
- `artifacts/logs/qemu-diagnostics-preset-step636.log` contains:
  - `QEMU_MARKER_PRESETS diagnostics`
  - `ORDERED_MARKERS FAULT_INFRA_READY;EARLY_DIAGNOSTICS_READY;DIAGNOSTIC_STUBS_MATERIALIZED;CONTROLLED_FAULT_PROBES_READY;CPU_FAULT_PANIC_READY;DIAGNOSTICS_CHAIN_READY`
  - `TEST_PASS qemu.runner.dryrun`

## Failures

- A parallel build attempt failed with `CS2012` because `OS.QemuRunner.dll` was
  locked by a simultaneous runner build. The same host-test build was rerun
  sequentially and passed with zero warnings and zero errors.
- `git diff --check` emitted only existing LF-to-CRLF warnings for touched text
  files.

## Deferred work

- The diagnostics chain is host-tested metadata and marker evidence only. It
  does not load descriptor tables, execute generated stubs, perform COM1 port
  I/O, trigger a null-write or invalid-opcode fault, capture live registers, or
  prove QEMU fault handling.
- Next Issue #2 work should move from this aggregate gate to live-owned COM1
  stub invocation or descriptor-table install preconditions, still behind
  disabled-by-default probes.
