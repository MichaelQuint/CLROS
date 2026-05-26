# Step 134 - One-Step Native App Package Builder

## What Changed

- Added `tools/OS.ImageBuilder build-native-app-package`.
- The command accepts `--manifest <native-manifest>`, `--input <raw-app-bytes>`, and `--output <package.pkg>`.
- It parses the kernel-facing native manifest, reads the manifest-selected `app.path`, wraps the raw app bytes with the existing `OSAPPv0` header, creates a deterministic single-file `OSPKGv0` package at that package path, validates the generated package, and writes the package only after validation succeeds.
- Added focused host tests for successful package generation, manifest path preservation, malformed manifest rejection, and empty app input rejection.
- Updated image-builder, manifest, app-model, and completion-gate docs with the host-side evidence boundary.

## Why It Changed

Gitea issue #153 needed one host-side packaging command for native-tier app payload experiments. The separate `build-native-payload`, `build-package`, and `validate-package` commands remain available, but this step gives developers a single deterministic path from native manifest plus raw app bytes to a validated package artifact.

## Files Changed

- `tools/OS.ImageBuilder/Program.cs`
- `tests/OS.HostTests/ImageBuilderTests.cs`
- `docs/image-builder.md`
- `docs/manifest.md`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step134.md`

## Tests Run

Executed:

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun
git diff --check
```

## Logs And Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- Full host tests emitted `TEST_PASS host.tests`.
- Host tests emitted `TEST_PASS imagebuilder.native-app-package builds validated package`.
- Host tests emitted `TEST_PASS imagebuilder.native-app-package preserves manifest app path`.
- Host tests emitted `TEST_PASS imagebuilder.native-app-package rejects malformed manifest`.
- Host tests emitted `TEST_PASS imagebuilder.native-app-package rejects empty app input`.
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `.\scripts\run-qemu-smoke.ps1 -DryRun` emitted `TEST_PASS qemu.runner.dryrun`.
- `git diff --check` exited 0, with existing line-ending warnings allowed by the issue.

## Failures

- None in the requested verification commands.

## Deferred Work

- No native-tier app compilation was added.
- No hosted .NET archive creation was added.
- No complete executable-format validation was added.
- No executable mapping, app ABI, app launcher, native app execution, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior was added.

## Evidence Boundary

This step is host-side artifact packaging only. It proves the local ImageBuilder can compose a manifest-selected `OSAPPv0` payload into a parser-valid and validation-passing `OSPKGv0` package. It does not prove guest storage reads, boot-time package loading, executable mapping, app execution, hosted .NET 10 startup, ASP.NET Minimal API execution, or Kestrel support.
