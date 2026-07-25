# Step 274 - ExitBootServices Preflight Diagnostics Gate

## What Changed

- Added `BootSequence.RunExitBootServicesPreflight`.
- Added stable markers:
  - `UEFI_EBS_PREFLIGHT_READY`
  - `UEFI_EBS_PREFLIGHT_BLOCKED diagnostics=<status> plan=<status>`
- Added host tests for:
  - ready preflight;
  - missing post-firmware diagnostics;
  - non-success transition plan;
  - unchanged default boot markers.
- Updated boot, boot-order, diagnostics, and completion-gate docs.

## Why It Changed

The future real `ExitBootServices` path must require both OS-owned
post-firmware diagnostics and a successful transition plan before firmware
services can be considered unavailable. This step joins those existing
host-tested contracts at the ordered boot boundary without calling firmware.

## Files Changed

- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/BootSequencePlanTests.cs`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/diagnostics.md`
- `docs/completion-gates.md`
- `done/step274.md`

## Tests Run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll
```

## Logs And Evidence

`dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1`
passed:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

The focused host-test marker run exited `0` and included:

```text
TEST_PASS boot.sequence exitboot preflight emits ready marker
TEST_PASS boot.sequence exitboot preflight blocks missing post-firmware diagnostics
TEST_PASS boot.sequence exitboot preflight blocks non-success transition plan
TEST_PASS boot.sequence exitboot preflight leaves default markers unchanged
TEST_PASS host.tests
```

## Failures

No failures in this focused slice.

## Evidence Boundary

This is managed host-test marker evidence only. It does not call real firmware,
configure COM1, invoke generated port-I/O stubs, execute after firmware
shutdown, or prove QEMU post-firmware behavior.

## Deferred Work

- Derive owned serial readiness from concrete COM1 backend prerequisites.
- Wire preflight into the live EFI path only after owned serial output is QEMU
  proven.
- Implement the actual fresh-map retry loop and firmware
  `ExitBootServices` call in a later issue.
