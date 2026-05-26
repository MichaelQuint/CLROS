# Step 082 - Controlled Fault Probe Readiness Marker

## What Changed

- Added `BootSequence.RunControlledFaultProbeReadiness`.
- Added stable markers `CONTROLLED_FAULT_PROBES_READY` and `CONTROLLED_FAULT_PROBES_NOT_READY status=<status>`.
- Added status-coded gating for early diagnostics failure and fault-probe readiness failure.

## Why It Changed

Issue #100 needed an explicit boot-order boundary before any destructive null-write or invalid-opcode probe can be wired into the boot path.

## Files Changed

- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/BootSequencePlanTests.cs`
- `docs/diagnostics.md`
- `docs/boot-order.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Host tests emitted:
  - `TEST_PASS boot.sequence controlled fault probes emit ready marker`
  - `TEST_PASS boot.sequence controlled fault probes reject early diagnostics failure`
  - `TEST_PASS boot.sequence controlled fault probes reject probe readiness failure`
  - `TEST_PASS boot.sequence controlled fault probes leave default markers unchanged`

## Failures

- No build or host-test failures after integration.

## Deferred Work

- No generated stubs execute.
- No CPU fault is triggered.
- The helper is not wired into the default live boot path.

## Evidence Boundary

This step proves a host-tested marker/readiness boundary only. It is not QEMU fault-probe evidence.
