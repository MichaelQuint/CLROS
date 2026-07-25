# Step 018 - QEMU Runner Argument Builder Tests

## What Changed

- Extracted QEMU argument and dry-run command-line construction from `Program.cs` into `QemuCommandBuilder`.
- Kept the existing QEMU command shape stable:
  - `q35,accel=tcg`
  - serial-file logging
  - optional OVMF pflash drive
  - AHCI-backed ESP raw image
  - user-mode virtio-net device
- Expanded QEMU runner host tests into focused checks for base dry-run construction, serial log arguments, OVMF path arguments, required marker reporting, supported virtio network arguments, and missing-image failure.
- Updated QEMU documentation to describe the expanded host-test coverage.

## Why It Changed

Issue #26 required the QEMU runner command contract to become host-testable before adding more launch modes. Extracting command construction gives future QEMU image, qcow2, storage, and networking work a single place to extend without weakening the current dry-run and smoke evidence contract.

## Files Changed

- `tools/OS.QemuRunner/Program.cs`
- `tools/OS.QemuRunner/QemuCommandBuilder.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/qemu.md`
- `done/step018.md`

## Tests Run

```powershell
dotnet build .\tools\OS.QemuRunner\OS.QemuRunner.csproj --nologo
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
dotnet run --project .\tools\OS.QemuRunner\OS.QemuRunner.csproj --no-build -- --dry-run --qemu qemu-test --image artifacts\images\os.raw --serial-log artifacts\logs\qemu-smoke.log --ovmf-code artifacts\ovmf\OVMF_CODE.fd --require-marker OS_SMOKE_PASS --require-marker UEFI_MEMORY_MAP_DESCRIPTOR_COUNT
```

## Logs and Evidence

- `OS.QemuRunner` build passed with 0 warnings and 0 errors.
- `OS.HostTests` build passed with 0 warnings and 0 errors.
- Full solution build passed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS host.tests`.
- Direct QEMU runner dry-run emitted `TEST_PASS qemu.runner.dryrun` and `REQUIRED_MARKERS OS_SMOKE_PASS;UEFI_MEMORY_MAP_DESCRIPTOR_COUNT`.
- New QEMU runner markers passed:
  - `TEST_PASS qemu dry-run preserves command construction`
  - `TEST_PASS qemu dry-run emits serial log arguments`
  - `TEST_PASS qemu dry-run emits OVMF path arguments`
  - `TEST_PASS qemu dry-run emits marker requirements`
  - `TEST_PASS qemu dry-run emits virtio network arguments`
  - `TEST_PASS qemu real mode rejects missing image before launch`

## Failures

No failures remain for this slice.

An initial host-test run used the previously built binary because it was run with `--no-build` before rebuilding `OS.HostTests`. The tests were rebuilt and rerun successfully.

## Deferred Work

- Add qcow2 launch arguments after image conversion support exists.
- Add virtio-blk launch support when the kernel has a matching driver probe.
- Add TAP/bridge networking launch modes after the network smoke path is ready.
- Add real libvirt/QEMU parity checks once libvirt boot is available.

## Evidence Boundary

This step proves host-side QEMU argument construction and dry-run output. It does not add a new boot mode, qcow2 launch path, virtio-blk boot path, or additional in-guest hardware evidence.
