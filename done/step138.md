# Step 138 - ImageBuilder Native App Raw Image Command

## What Changed

- Added `OS.ImageBuilder build-native-app-image`.
- The command composes the existing `build-native-app-package` path with the existing raw image writer.
- Successful output emits:
  - `TEST_START imagebuilder.native-app-image`
  - `NATIVE_APP_IMAGE_PACKAGE_PATH <path>`
  - `NATIVE_APP_IMAGE_RAW_PATH <path>`
  - `TEST_PASS imagebuilder.native-app-image`
- Failure output emits `TEST_FAIL imagebuilder.native-app-image <reason>` and returns nonzero.
- Added host-test coverage for creating a raw image with a package, rejecting missing EFI input, and rejecting malformed manifests.
- Updated image-builder, app-model, and completion-gate docs with the host-tooling-only evidence boundary.

## Why It Changed

Gitea issue #156 needed a single host command that starts from `BOOTX64.EFI`, a native manifest, and raw app bytes, then produces a raw disk image containing `\EFI\BOOT\BOOTX64.EFI`, `\OS\MANIFEST.JSON`, and `\OS\APP.PKG`.

## Files Changed

- `tools/OS.ImageBuilder/Program.cs`
- `tests/OS.HostTests/ImageBuilderTests.cs`
- `docs/image-builder.md`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step138.md`

## Tests Run

```powershell
dotnet build .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj --nologo
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj --no-restore -- build-native-app-image --efi <temp>\BOOTX64.EFI --manifest <temp>\manifest.txt --input <temp>\app.raw.bin --output <temp>\native-app-image.raw
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj --no-restore -- build-native-app-image --efi <missing> --manifest <temp>\manifest.txt --input <temp>\app.raw.bin --output <temp>\missing-efi.raw
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj --no-restore -- build-native-app-image --efi <temp>\BOOTX64.EFI --manifest <temp>\bad-manifest.txt --input <temp>\app.raw.bin --output <temp>\bad-manifest.raw
dotnet build .\OS.slnx --nologo -m:1
```

## Logs And Evidence

- `dotnet build .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj --nologo` passed with 0 warnings and 0 errors.
- The focused success run emitted `TEST_START imagebuilder.native-app-image`.
- The focused success run emitted `NATIVE_APP_IMAGE_PACKAGE_PATH <temp>\native-app-image.raw.app.pkg`.
- The focused success run emitted `NATIVE_APP_IMAGE_RAW_PATH <temp>\native-app-image.raw`.
- The focused success run emitted `TEST_PASS imagebuilder.native-app-image`.
- The generated raw image and adjacent `.app.pkg` file both existed after the focused success run.
- The missing-EFI run emitted `TEST_FAIL imagebuilder.native-app-image efi_not_found` and returned exit code 3.
- The malformed-manifest run emitted `TEST_FAIL imagebuilder.native-app-image manifest_malformed` and returned exit code 23.

## Failures

- `dotnet build .\OS.slnx --nologo -m:1` is currently blocked by an out-of-scope compile error in `tests/OS.HostTests/NativeAppInvocationPlanTests.cs`, where `NativeAppExecutableMemoryPlanResult.Success(...)` is called without the required `descriptorStatus` argument.
- Full host tests were not run because the solution test project does not currently compile.
- A separate worker's host-test process briefly had `OS.ImageBuilder create-raw` children running. They were not this step's verification processes and were not interrupted.

## Deferred Work

- No BootSequence or native app invocation plan work was changed.
- No QEMU smoke was run.
- No guest storage, boot-time package loading, executable mapping, native app execution, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior was added.
- Gitea issue #156 was not closed.

## Evidence Boundary

This step is host-side ImageBuilder composition only. It proves the one-step command can generate a parser-valid package and raw image artifact from local input bytes through existing builders. It does not prove QEMU boot, live disk reads, package loading in the boot path, executable-format validation beyond the existing `OSAPPv0` header, app launch, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
