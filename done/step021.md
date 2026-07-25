# Step 021 - Image Builder Dry-Run Layout Plan

## What Changed

- Added a non-writing `plan-raw` command to `tools/OS.ImageBuilder`.
- Added `create-raw --dry-run` as an alias for the same plan path.
- Reused the raw ESP layout validation for dry-run mode, including EFI input validation, output path validation, image sizing, FAT32 cluster sizing, and EFI artifact size checks.
- Emitted stable machine-readable `IMAGE_PLAN` and `IMAGE_PLAN_ENTRY` JSON lines for the raw GPT/FAT32 ESP layout.
- Added plan entries for the EFI System Partition, `\EFI\BOOT\BOOTX64.EFI`, and placeholders for future `\OS\MANIFEST.JSON` and `\OS\APP.PKG` package entries.
- Added a host test that proves the dry-run plan does not create the output image.
- Updated `docs/image-builder.md` with current behavior and evidence boundaries.

## Why It Changed

Gitea issue #15 requires the image builder to document and emit a dry-run layout plan before raw-sector writes. The plan gives agents and future CI a stable contract for the current EFI image layout without making destructive writes or claiming package/manifest placement is implemented.

## Files Changed

- `tools/OS.ImageBuilder/Program.cs`
- `tests/OS.HostTests/ImageBuilderTests.cs`
- `docs/image-builder.md`
- `done/step021.md`

## Tests Run

```powershell
dotnet build .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj --nologo
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj --no-build -- plan-raw --efi <temp>\BOOTX64.EFI --output <temp>\os.raw --image-size-mib 64
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj --no-build -- create-raw --dry-run --efi <temp>\BOOTX64.EFI --output <temp>\alias.raw --image-size-mib 64
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
git diff --check -- tools\OS.ImageBuilder\Program.cs tests\OS.HostTests\ImageBuilderTests.cs docs\image-builder.md done\step021.md
```

## Logs and Evidence

- `dotnet build .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj --nologo` passed with 0 warnings and 0 errors.
- The manual `plan-raw` run emitted `TEST_START imagebuilder.plan`, `IMAGE_PLAN`, ordered `IMAGE_PLAN_ENTRY` rows, `IMAGE_PLAN_NO_WRITE <temp>\os.raw`, and `TEST_PASS imagebuilder.plan`.
- The manual dry-run output included `efi-system-partition`, `efi-boot-file`, `manifest-placeholder`, and `app-package-placeholder` entries.
- The manual dry-run target output path remained absent after the command.
- The `create-raw --dry-run` alias emitted `TEST_START imagebuilder.plan`, `IMAGE_PLAN_NO_WRITE <temp>\alias.raw`, and `TEST_PASS imagebuilder.plan`; the alias output path remained absent.
- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS imagebuilder emits dry-run raw layout plan without writing image`, `TEST_PASS imagebuilder creates raw GPT FAT32 ESP with BOOTX64.EFI`, and `TEST_PASS imagebuilder rejects missing EFI artifact`.
- `git diff --check` exited 0 for the owned files. It printed line-ending normalization warnings only.

## Failures

No failures remain for this slice.

## Deferred Work

- Write manifest and app-package bytes into the image once the package/app layout is implemented.
- Add qcow2 conversion, ISO generation if still useful, and cloud artifact metadata.
- Add byte-level tests for manifest/app package placement once those writes exist.

## Evidence Boundary

This step proves a host-side, non-writing raw ESP layout plan and targeted tool build evidence. It does not prove package filesystem support, manifest placement, app package placement, in-kernel manifest loading, native-tier app launch, hosted .NET 10, ASP.NET Minimal API, Kestrel, or cloud import readiness.
