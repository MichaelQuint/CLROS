# Step 034 - QEMU qcow2 Launch Mode

## What Changed

- Added `--disk-format raw|qcow2` to `tools/OS.QemuRunner`.
- Kept `raw` as the default disk format.
- Routed the selected disk format into the QEMU ESP drive argument.
- Added host tests for default raw mode, explicit qcow2 mode, and invalid disk-format validation.
- Updated QEMU and image-builder docs to describe qcow2 launch dry-run coverage without claiming real qcow2 boot proof.

## Why It Changed

The image builder already has a qcow2 conversion wrapper. The runner also needs a deterministic command-line contract for launching qcow2 artifacts so cloud-image tooling can be tested before real qcow2 boot evidence is captured.

## Files Changed

- `tools/OS.QemuRunner/Program.cs`
- `tools/OS.QemuRunner/QemuCommandBuilder.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/qemu.md`
- `docs/image-builder.md`
- `done/step034.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
git diff --check
```

## Evidence

- Host test marker: `TEST_PASS qemu dry-run uses raw disk format by default`
- Host test marker: `TEST_PASS qemu dry-run uses qcow2 disk format`
- Host test marker: `TEST_PASS qemu rejects invalid disk format`
- Full host test marker: `TEST_PASS host.tests`

## Failures

None for this runner slice.

## Deferred Work

- Convert `artifacts/images/os.raw` to qcow2 on a host with `qemu-img`.
- Run a real QEMU boot against the qcow2 artifact.
- Add libvirt qcow2 boot coverage.
- Add cloud provider import validation.

## Evidence Boundary

This step proves QEMU runner command construction and validation for qcow2 mode. It does not prove a real qcow2 boot, libvirt boot, provider import, storage driver behavior, networking, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
