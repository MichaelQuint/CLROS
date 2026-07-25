# Step 002 - Discovery, Tooling Guardrails, and First Host-Tested Slices

## What Changed

- Corrected first-party docs and defaults to use `OS` naming and the current `os.raw` image default; SharpOS remains reference-only.
- Added `scripts/check-naming.ps1` to fail on stale first-party `SharpOS` project wording, legacy smoke marker spelling, legacy raw image filenames, and old boot/probe paths.
- Added `OS.ManifestTool validate --manifest <path>` for manifest v0 host validation.
- Added deterministic `OS.LibvirtRunner generate-xml` / `--dry-run` domain XML generation.
- Added `OS.Std.Ascii` helper methods and host tests.
- Updated docs to make the first-class workload target explicit: .NET 10 apps, ASP.NET Minimal API as the primary cloud scenario, and mandatory Kestrel support.
- Created small Gitea child issues for boot, runtime, PAL, tooling, storage, networking, and hosted .NET 10/Kestrel prerequisites.
- Created and applied Gitea labels for phases, components, work type, and agent readiness.

## Why It Changed

The repository needed guardrails before broader reference adaptation. These slices keep the Phase A baseline honest while adding testable host tooling and documenting that the CloudReady target is not just a native demo: it must support .NET 10 Minimal API workloads on Kestrel.

## Files Changed

- `README.md`
- `docs/*`
- `docs/issue-labels.md`
- `done/step002.md`
- `scripts/check-naming.ps1`
- `std/no-runtime/shared/Ascii.cs`
- `tests/OS.HostTests/*`
- `tools/OS.ManifestTool/Program.cs`
- `tools/OS.LibvirtRunner/Program.cs`

## Tests

Passed:

```powershell
.\scripts\check-naming.ps1
dotnet build .\OS.slnx
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\run-qemu-smoke.ps1 -DryRun
dotnet run --project .\tools\OS.ManifestTool\OS.ManifestTool.csproj --no-build -- validate --manifest <valid-manifest>
dotnet run --project .\tools\OS.LibvirtRunner\OS.LibvirtRunner.csproj --no-build -- generate-xml --network none
```

Evidence:

- Naming guard emitted `TEST_PASS naming.guard`.
- Build succeeded with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS host.tests`, including manifest, libvirt XML, std ASCII, boot sequence, and boot event tests.
- QEMU dry-run emitted `TEST_PASS qemu.runner.dryrun`.
- Manifest direct smoke emitted `TEST_PASS manifest.validate`.
- Libvirt direct smoke emitted deterministic domain XML for `os-test`.

## Deferred Work

- EFI/NativeAOT build and `EfiMain`.
- Real QEMU boot.
- Kernel manifest loading.
- Raw/qcow2 image generation.
- Libvirt connection and transient domain boot.
- Hosted .NET 10 runtime path, ASP.NET Minimal API app launch, and Kestrel.
- Drivers, storage, networking, scheduler, async, and sockets.

## Evidence Boundary

This step proves host-side validation and generation behavior only. It does not prove bootability, in-kernel manifest parsing, libvirt boot, Kestrel support, or cloud-provider readiness.
