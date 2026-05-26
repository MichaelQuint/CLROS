# Step 268 - Post-Firmware Diagnostics Readiness Gate

## What Changed

- Added `BootSequence.RunPostFirmwareDiagnosticsReadiness`.
- Added stable markers:
  - `POST_FIRMWARE_DIAGNOSTICS_READY`
  - `POST_FIRMWARE_DIAGNOSTICS_BLOCKED status=<SerialDiagnosticsGuardStatus>`
- Added host tests for the owned-serial-ready path, firmware-console-only block,
  firmware-services-unavailable block, and default boot marker preservation.
- Updated boot, boot-order, diagnostics, and completion-gate docs.

## Why It Changed

Real `ExitBootServices` must not advance past firmware-backed output until the
OS can diagnose failures through an owned path. This slice adds the ordered
`BootSequence` boundary for that requirement without configuring COM1, invoking
port-I/O stubs, marking firmware exited, or calling firmware.

## Files Changed

- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/BootSequencePlanTests.cs`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/diagnostics.md`
- `docs/completion-gates.md`
- `done/step268.md`

## Tests Run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
```

## Logs And Evidence

The host-test project build passed with no errors. It emitted transient copy
retry warnings because another `OS.HostTests` process held test output DLLs
during the build, then completed successfully:

```text
Build succeeded.
21 Warning(s)
0 Error(s)
```

The new tests are:

```text
TEST_PASS boot.sequence post-firmware diagnostics readiness emits ready marker
TEST_PASS boot.sequence post-firmware diagnostics readiness blocks firmware-console-only path
TEST_PASS boot.sequence post-firmware diagnostics readiness blocks firmware-services-unavailable path
TEST_PASS boot.sequence post-firmware diagnostics readiness leaves default markers unchanged
```

## Failures

No compile failures. Full solution/test/hygiene verification still needs to run
after the parallel worker slices stop holding test binaries.

## Evidence Boundary

This is managed host-test marker evidence only. It does not configure COM1,
invoke generated port-I/O stubs, mark firmware services exited, call
`ExitBootServices`, or prove live post-firmware output.

## Deferred Work

- Implement and test the owned COM1 output path.
- Feed the owned serial readiness result into the post-firmware diagnostics gate
  before a real firmware shutdown attempt.
- Wire the gate into the live boot path only after post-firmware output can be
  proven in QEMU.
