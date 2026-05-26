# Step 220 - NVMe Register Map Constants

## Summary

Added host-tested NVMe controller, admin queue, status, and doorbell register
constants for issue #240. This is the first AWS Nitro storage track code slice
and deliberately stops at register metadata.

## Why

AWS Nitro exposes EBS and instance-store volumes as NVMe devices. Before any
admin queue or block I/O work can be implemented, the OS needs a deterministic
register-map contract for controller enable/status, admin queue registers, entry
sizes, and CAP-derived doorbell stride.

## Files changed

- `src/OS.Kernel/Drivers/NvmeRegisterMap.cs`
- `tests/OS.HostTests/NvmeRegisterMapTests.cs`
- `docs/aws-nitro-device-plan.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\check-naming.ps1`
- `.\scripts\run-qemu-smoke.ps1 -DryRun -RequireNativeAppMarkers`
- `git diff --check`

## Evidence

- Host test markers:
  - `TEST_PASS drivers.nvme.registermap controller offsets`
  - `TEST_PASS drivers.nvme.registermap admin queue offsets`
  - `TEST_PASS drivers.nvme.registermap status bits`
  - `TEST_PASS drivers.nvme.registermap validates admin queue aperture length`
  - `TEST_PASS drivers.nvme.registermap calculates doorbell stride`
- Checkpoint logs:
  - `artifacts/logs/checkpoint-build-20260522-120158.log`
  - `artifacts/logs/checkpoint-host-tests-20260522-120158.log`
  - `artifacts/logs/checkpoint-guards-20260522-120158.log`

## Failures

None in the final checkpoint run. `git diff --check` emitted only line-ending
normalization warnings and returned exit code 0.

## Deferred work

- No NVMe PCI candidate parser is implemented in this step.
- No NVMe controller enable sequence, admin queue allocation, identify command,
  namespace discovery, PRP/SGL handling, block read, interrupt, or timeout path
  is implemented.
- No AWS EC2/Nitro boot evidence is claimed.

