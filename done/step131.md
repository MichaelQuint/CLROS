# Step 131 - ImageBuilder Package Payload Validation

## What Changed

- Added `tools/OS.ImageBuilder validate-package --manifest <native-manifest> --package <pkg>`.
- The command parses the kernel-facing native manifest, builds a host-side launch plan, parses the `OSPKGv0` package, resolves the manifest-selected `app.path`, and validates the selected file's `OSAPPv0` payload header.
- Added ImageBuilder host tests for successful manifest/package/payload validation and deterministic failures for missing manifest, malformed manifest, missing package, missing selected app file, and invalid selected payload header.
- Updated image-builder, manifest, app-model, and completion-gate docs.

## Why It Changed

Gitea issue #148 needed a host-side consistency check before package artifacts are placed on an image or later consumed by a boot-time loader. The new command validates artifact shape only and does not execute app code.

## Files Changed

- `tools/OS.ImageBuilder/OS.ImageBuilder.csproj`
- `tools/OS.ImageBuilder/Program.cs`
- `tests/OS.HostTests/ImageBuilderTests.cs`
- `docs/image-builder.md`
- `docs/manifest.md`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step131.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun
git diff --check
```

## Logs And Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted:
  - `TEST_PASS imagebuilder.package validates manifest app payload`
  - `TEST_PASS imagebuilder.package validation rejects missing manifest`
  - `TEST_PASS imagebuilder.package validation rejects malformed manifest`
  - `TEST_PASS imagebuilder.package validation rejects missing package`
  - `TEST_PASS imagebuilder.package validation rejects missing app file`
  - `TEST_PASS imagebuilder.package validation rejects invalid payload header`
  - `TEST_PASS host.tests`
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `.\scripts\run-qemu-smoke.ps1 -DryRun` emitted `TEST_PASS qemu.runner.dryrun`.
- `git diff --check` exited successfully. It printed existing line-ending normalization warnings for the shared dirty tree.

## Failures

- None in this verification run.

## Deferred Work

- No live storage path, boot-time package load, executable mapping, app ABI, native app execution, hosted .NET 10 runtime, ASP.NET Minimal API launch, or Kestrel behavior was added.

## Evidence Boundary

This step proves host-side package/manifest/payload consistency validation only. It does not prove guest storage, native app launch, hosted .NET 10, ASP.NET Minimal API, or Kestrel.
