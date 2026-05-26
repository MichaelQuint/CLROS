# Step 027 - qcow2 Conversion Wrapper

## What Changed

- Added `convert-qcow2` to `tools/OS.ImageBuilder`.
- Added explicit and PATH-based `qemu-img` discovery.
- Added dry-run output that validates input/output paths and prints the future conversion command without requiring `qemu-img`.
- Added stable failure markers for missing/empty raw input, missing `qemu-img`, and failed `qemu-img` execution.
- Added host tests for qcow2 dry-run and failure contracts.
- Updated image-builder, QEMU, and cloud-import docs without claiming cloud provider compatibility.

## Why It Changed

Cloud-ready deployment needs raw and qcow2 artifacts. This wrapper gives the repository a deterministic local conversion contract while keeping `qemu-img` optional for normal builds and host tests.

## Files Changed

- `tools/OS.ImageBuilder/Program.cs`
- `tests/OS.HostTests/ImageBuilderTests.cs`
- `docs/image-builder.md`
- `docs/qemu.md`
- `docs/cloud-import.md`
- `done/step027.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` passed and emitted `TEST_PASS host.tests`.
- ImageBuilder host tests emitted:
  - `TEST_PASS imagebuilder emits dry-run qcow2 conversion without qemu-img`
  - `TEST_PASS imagebuilder rejects missing qcow2 input`
  - `TEST_PASS imagebuilder rejects empty qcow2 input`
  - `TEST_PASS imagebuilder rejects missing qemu-img in real qcow2 mode`

## Failures

No failures remain for this slice.

## Deferred Work

- Add QEMU/libvirt qcow2 boot paths.
- Add checksum/metadata output for cloud artifacts.
- Validate provider import flows separately before claiming AWS, GCE, Azure, or generic cloud readiness.

## Evidence Boundary

This step proves the host-side qcow2 conversion wrapper and deterministic dry-run/failure contracts. It does not prove qcow2 boot, libvirt boot, or cloud provider import compatibility.
