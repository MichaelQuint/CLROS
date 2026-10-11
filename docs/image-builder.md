# Image Builder

The bounded disk-manifest/package/staging batch has host-tested validators in
[step1153](../done/step1153.md). Its independently authored test packages use the
existing OSPKGv0 table and OSAPPv0 payload formats. CRC32 is an explicit expected
fixture checksum, not an embedded or cryptographic package hash.

[Step1154](../done/step1154.md) adds the compiled smoke-only `PackageImageFixture`
after the accepted FAT fixture. It preserves GPT, EFI, root entries and the
fragmented long-name fixture, adds `OS/NATIVE.MAN` and `OS/APP.PKG` in exactly two
free clusters, and updates both FATs and FSInfo copies. Identity, geometry,
existing names, free space and all planned writes are checked before mutation;
flushed bytes and hashes are read back. Repeat application is rejected.
`--PackageImageVariant` selects `Intact`, `ManifestVersion`, `PackageChecksum`,
`PackageHeader`, `PackageTruncated` or `PackageLoop` only with the package profile.
Dry runs do not mutate images. The underlying image builder commands are unchanged.
Focused host mutation/preservation checks and all six real guest variants pass.
[Step1155](../done/step1155.md) records native and ancestor review;
[step1156](../done/step1156.md) accepts the bounded batch after 4,393 unfiltered
full-host checks on unchanged corrected inputs. These are smoke fixtures.

## FAT smoke fixture

The default-off FAT profile in [step1152](../done/step1152.md) applies the compiled
`FatImageFixture` only after a fresh image build. It preserves GPT and EFI bytes,
adds a second root-directory cluster and the long name
`CLROS Live Storage Fixture.bin`, splits its VFAT records across the root boundary,
and writes1,301 deterministic bytes into three non-contiguous-order clusters.
Both FATs and primary/backup FSInfo free counts are updated and read back. The
receipt binds original/final image SHA256, EFI SHA256, cluster order and CRC32.
It rejects repeat application, noncanonical geometry, unequal copies and wrong
EFI identity before writing. Dry runs do not mutate images. The host fixture is
qualified with independent mutation controls; the separate 241-record real guest
capture establishes the bounded file-read behavior with native/full-host review.

The GPT batch accepted in [step1150](../done/step1150.md) adds a smoke-only
`GptImageVariant`: `Intact` by default, or `PrimaryHeaderCrcFault` with the explicit
GPT diagnostic. The latter validates both GPT copies in a freshly generated raw
image, changes only byte528 by XOR1, and records original/final SHA256 plus the
exact byte read-back. It preserves the backup tables, FAT and EFI bytes. The
fixture requires the image builder's adjacent backup array; the guest validator
also permits bounded nonadjacent arrays. Dry runs never modify images. Actual
intact/backup captures, native review and full-host qualification pass.

For the fault variant, retain the fresh intact image as `<Image>.boot.raw`,
boot it through SATA with `snapshot=on` (temporary writes only), and expose the
one-byte-damaged image as the
read-only Virtio device at slot6. This OVMF build cannot boot directly from the
damaged primary. The wrapper verifies the companion against the pre-mutation
hash, and final image checks require exactly the CRC-byte difference. Paths
must be distinct. The fault capture uses the existing exact SATA firmware
prefix; intact captures retain the existing Virtio boot prefix.

The new `--EnableM06LiveBlockContractProbe` requires the read diagnostic and
adds an LBA0 receipt. Host checks independently compare disk capacity, CRC32,
first/last words, signature55AA and the single protective MBR entry with guest
records; malformed or hybrid entries fail. This does not interpret GPT or load
files in the guest. [Step1147](../done/step1147.md) accepts actual guest and
full-host qualification. Independent host inspection also checks both GPT
copies, FAT copies and the exact EFI payload in each capture image; those host
checks do not establish guest GPT/FAT parsing.

## Current Status

The accepted read diagnostic adds `--EnableM06VirtioBlockReadProbe` to that block
queue profile. It keeps the same read-only image and transports LBA1 into owned
memory. Its host validator compares device capacity and the recorded 512-byte
CRC/first/last words with the actual image. [Step1143](../done/step1143.md) records
full-host, native and guest qualification with independent image association. Image
generation, GPT interpretation and file/package loading are unchanged.

The default-off [block queue diagnostic](efi-build.md#bounded-virtio-block-queue-profile)
attaches the existing GPT/FAT EFI image as a read-only modern Virtio boot disk.
Image contents and ordinary SATA attachment are unchanged. Firmware loads the
EFI payload; CLROS only binds/configures/resets the device in this batch, with
no CLROS sector read or package load. Independent GPT/FAT/EFI inspection and
accepted evidence are in [step1140](../done/step1140.md).

`tools/OS.ImageBuilder` can emit a non-writing dry-run layout plan, create a deterministic raw disk image containing a GPT-partitioned FAT32 EFI System Partition, build deterministic `OSPKGv0` app-package files from an input directory, wrap a raw native app binary in the minimal `OSAPPv0` native payload header, build a validated single-file native app package directly from a native manifest plus raw app bytes, compose that package into a raw native-app image in one host command, validate that a package contains the manifest-selected `app.path` with a valid native payload header, and emit local size/SHA-256 metadata for existing image artifacts. It also has a host-side `qemu-img` wrapper that can convert an existing raw image to qcow2 when `qemu-img` is available. `dotnet run --project tools/OS.Tooling -- qemu qcow2` coordinates conversion and local QEMU/OVMF smoke; boot proof requires its real execution and captured guest markers.

The raw image places the staged EFI artifact at the default UEFI removable-media path:

```text
\EFI\BOOT\BOOTX64.EFI
```

It always places a kernel-facing native manifest into the ESP at:

```text
\OS\MANIFEST.JSON
```

When `--manifest <path>` is provided, the tool copies that file byte-for-byte. When omitted, the tool writes a deterministic default native manifest v0 so the generated ESP has a manifest loader target.

It can optionally place an app/package payload into the ESP at:

```text
\OS\APP.PKG
```

Host tests now verify deterministic package construction from a directory, byte-for-byte package file placement, generated GPT readback through `GptPartitionTableParser`, parser-valid package readback from the generated raw image through `OsPackageLoader.LoadFromPrimaryGptEsp`, and a host-only MinimalApiSmoke publish-script round trip through `--kestrel-package`. The host-test harness runs ImageBuilder commands through the already-built `tools/OS.ImageBuilder/bin/<configuration>/net10.0/OS.ImageBuilder.dll`, rather than invoking nested `dotnet run`, so `--no-build` host-test runs do not repeatedly rebuild the tool under the test process.

This is host-side packaging only. Boot proof still belongs to QEMU smoke evidence, but the generated raw and qcow2 images have now been consumed successfully by QEMU/OVMF for the EFI smoke path, ordered boot markers, UEFI memory-map readiness, compact boot-memory-region materialization, and ACPI RSDP handoff markers. qcow2 conversion is an artifact-format convenience and is not cloud import validation.

## Implemented Behavior

### FAT geometry correction (#514)

Independent review of the preserved PR #509 and #511 64 MiB images found that
the FAT-sizing recurrence can oscillate and return a stale cluster count.
Their 128,991-sector ESP declares 126,975 data clusters but its 992-sector FAT
contains only 126,976 entries, one fewer than needed with reserved entries 0
and 1. FSInfo also uses stale accounting. Bounded extraction confirms the EFI
boot payloads, and QEMU boot markers remain observed evidence; neither proves
whole-filesystem geometry. Historical images remain unchanged.

The current host implementation sizes the FAT once using an upper bound on
data clusters, then derives the actual cluster count from that same FAT size.
For a 64 MiB image it uses 1,008 FAT sectors and 126,943 data clusters. This
conservative capacity calculation removes the oscillation and stale FSInfo
count; round-up arithmetic uses a wider intermediate.

It also rejects a derived cluster count of `0x0FFFFFF6` or greater with exit 5
and `image_too_large`, before image creation. The current one-sector-per-cluster
format accepts a plan through 133,153 MiB; 133,154 MiB is rejected. Those large
boundaries are plan evidence only. Tests emit 64, 65 and 128 MiB images and
independently check BPB-derived capacity, FSInfo accounting, backup metadata,
FAT copies, payload bytes and determinism. Step934 records focused evidence;
step939 adds the complete host gate and three fresh corrected default/layout/
heap guest images. Each 64 MiB image has 129,024 FAT entries for 126,943 data
clusters, with no unaddressable trailing cluster. Independent decoding confirms
the EFI payload byte-for-byte in both the pristine and post-boot images.

Separate pristine images use each guest lane's archived actual Debug builder,
without rebuilding it. Their allocated/free counts are 114/126,829 (default),
190/126,753 (layout) and 532/126,411 (heap); both pristine FSInfo copies match
the complete FAT census and first free cluster. After OVMF execution each image
has a 1,393-byte NVVARS file occupying exactly three new clusters. Changes are
confined to its directory entry/data, the new links in both FATs and primary
FSInfo. Backup FSInfo retains its pristine hints; post-firmware hints are
advisory and are not substituted for the pristine builder check. Existing file
chains, payloads and FAT clean/no-hard-error status remain unchanged.
This bounded review does not establish general filesystem conformance, live
guest storage, cloud import or broader runtime support. Historical defective
images remain preserved; this acceptance applies to the new images only.

Host tool projects inherit `tools/Directory.Build.props`, which excludes each
tool-local `artifacts/**` tree from SDK default source globs. Generated
image-builder or runner verification scratch files under `tools/*/artifacts`
are therefore not compiled as host tool source.

Dry-run the raw layout plan from the repository root:

```powershell
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- plan-raw --efi .\artifacts\efi\BOOTX64.EFI --manifest .\manifest.json --package .\app.pkg --output .\artifacts\images\os.raw
```

`create-raw --dry-run` is also accepted and routes to the same non-writing plan path.

The plan accepts `--workload-profile SingleMinimalApi|MultiMinimalApi`. The
default is the build-selected `OSWorkloadProfile`, which is normally
`SingleMinimalApi`. The selector is evidence metadata only; it does not change
the current FAT32/GPT layout and does not implement hosted .NET, Kestrel,
processes, networking, or app isolation.

The plan validates the EFI input, optional manifest input, optional package input, output path shape, minimum image size, maximum FAT32 sector-count geometry, FAT32 cluster requirements, and EFI artifact size. Manifest input is optional. When provided, the file must exist, be nonempty, and be no larger than 64 KiB. Package input is optional. When provided, the file must exist, be nonempty, and be no larger than 16 MiB. It emits stable line-oriented machine-readable markers and JSON payloads:

```text
TEST_START imagebuilder.plan
WORKLOAD_PROFILE SingleMinimalApi
IMAGE_PLAN {"mode":"dry-run","format":"raw",...,"writesSuppressed":true}
IMAGE_PLAN_ENTRY {"order":1,"kind":"protective-mbr",...}
IMAGE_PLAN_ENTRY {"order":4,"kind":"efi-system-partition",...}
IMAGE_PLAN_ENTRY {"order":11,"kind":"efi-boot-file","guestPath":"\\EFI\\BOOT\\BOOTX64.EFI",...}
IMAGE_PLAN_ENTRY {"order":12,"kind":"fat32-os-directory","guestPath":"\\OS",...}
IMAGE_PLAN_ENTRY {"order":13,"kind":"manifest-file","guestPath":"\\OS\\MANIFEST.JSON","sourcePath":"...","bytes":...}
IMAGE_PLAN_ENTRY {"order":14,"kind":"app-package-file","guestPath":"\\OS\\APP.PKG","sourcePath":"...","bytes":...}
IMAGE_PLAN_NO_WRITE <output>
TEST_PASS imagebuilder.plan
```

The `IMAGE_PLAN` JSON includes `workloadProfile`, for example:

```text
IMAGE_PLAN {"mode":"dry-run","format":"raw","workloadProfile":"SingleMinimalApi",...}
```

Explicit multi-profile plan:

```powershell
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- plan-raw --efi .\artifacts\efi\BOOTX64.EFI --output .\artifacts\images\os.raw --workload-profile MultiMinimalApi
```

Expected evidence includes:

```text
WORKLOAD_PROFILE MultiMinimalApi
"workloadProfile":"MultiMinimalApi"
```

Without `--manifest`, the plan writes a default manifest and omits `sourcePath`:

```text
IMAGE_PLAN_ENTRY {"order":13,"kind":"manifest-file","guestPath":"\\OS\\MANIFEST.JSON","bytes":110}
```

The dry-run path does not create the output directory, open the output image, or write raw sectors.

Run from the repository root:

```powershell
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- create-raw --efi .\artifacts\efi\BOOTX64.EFI --manifest .\manifest.json --package .\app.pkg --output .\artifacts\images\os.raw
```

For the host-only Kestrel smoke package contract, `create-raw` and `plan-raw`
also accept a package root directory:

```powershell
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- create-raw --efi .\artifacts\efi\BOOTX64.EFI --manifest .\artifacts\kestrel-smoke\package.json --kestrel-package .\artifacts\kestrel-smoke\package-root --output .\artifacts\images\kestrel-smoke.raw
```

`--kestrel-package <dir>` requires `--manifest <path>`, cannot be combined with
`--package`, and is supported only for `SingleMinimalApi`. The image builder
reuses `KestrelSmokePackageContractValidator` from `OS.ManifestTool`; it does
not maintain a separate Kestrel contract. When validation succeeds, it converts
the package root into a deterministic `OSPKGv0` package and stages it at
`\OS\APP.PKG`. The guest path is deliberately the same package path used by
native-tier package staging so later guest package readers have one stable
on-disk target.

Host tests also run `dotnet run --project tools/OS.Tooling -- kestrel package`
into a temporary output root with `-WorkloadProfile SingleMinimalApi`, require
the script's workload-profile, package, and contract markers, stage that real
publish output with `create-raw --kestrel-package`, then read back
`\OS\MANIFEST.JSON` and `\OS\APP.PKG` from the generated raw ESP. The readback
verifies that the manifest bytes match the generated `package.json`, that the
package contains the required MinimalApiSmoke files, and that the required file
bytes match the published files exactly.

Defaults:

- EFI input: `artifacts/efi/BOOTX64.EFI`
- Manifest input: deterministic default native manifest unless `--manifest <path>` is provided
- Package input: omitted unless `--package <path>` or `--kestrel-package <dir>` is provided
- Raw image output: `artifacts/images/os.raw`
- Image size: 64 MiB

The raw image layout is:

- Protective MBR at LBA 0.
- Primary GPT header at LBA 1.
- Primary GPT entries starting at LBA 2.
- One EFI System Partition starting at LBA 2048.
- FAT32 boot sector, FSInfo sector, backup boot sector, and two FATs inside the ESP.
- `\EFI\BOOT\BOOTX64.EFI` copied byte-for-byte from the EFI input.
- `\OS\MANIFEST.JSON` copied byte-for-byte from `--manifest` or generated from the deterministic native manifest default.
- Optional `\OS\APP.PKG` copied byte-for-byte from `--package`.
- Optional `\OS\APP.PKG` generated from the validated `--kestrel-package`
  root when that contract path is used.
- Backup GPT entries and backup GPT header at the end of the disk.

The tool emits machine-readable markers:

```text
TEST_START imagebuilder.raw
WORKLOAD_PROFILE SingleMinimalApi
IMAGE_ARTIFACT <path>
TEST_PASS imagebuilder.raw
```

Input validation failures emit `TEST_FAIL imagebuilder.raw <reason>` or `TEST_FAIL imagebuilder.plan <reason>` and return nonzero. Manifest-specific reasons are:

- `manifest_not_found`
- `manifest_empty`
- `manifest_too_large`

Package-specific reasons are:

- `package_not_found`
- `package_empty`
- `package_too_large`

Image-layout-specific reasons are:

- `image_too_small`
- `image_too_large`

Kestrel-package-specific failures reuse the manifest tool contract codes,
including:

- `kestrel_package_missing_file`
- `kestrel_package_missing_expected_env`
- `kestrel_package_unsupported_profile`
- `kestrel_package_unsupported_runtime`
- `kestrel_package_invalid_listen`

Build an `OSPKGv0` app package from a host directory:

```powershell
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- build-package --input .\artifacts\app-root --output .\artifacts\packages\app.pkg
```

The package builder recursively reads files below `--input`, normalizes package paths to forward-slash relative paths, sorts entries by ordinal package path, writes the existing `OSPKGv0` header/file-table/content layout, and rejects unsafe path shapes before writing the output. Successful output is deterministic for the same input bytes and path names:

```text
TEST_START imagebuilder.package
PACKAGE_FILE_COUNT <count>
PACKAGE_BYTES <bytes>
PACKAGE_ARTIFACT <path>
TEST_PASS imagebuilder.package
```

Package-build failures emit `TEST_FAIL imagebuilder.package <reason>` and return nonzero. Current reasons include:

- `package_input_not_found`
- `package_input_empty`
- `package_input_unsafe_path`
- `package_input_duplicate_path`
- `package_output_too_large`

Wrap a raw native app binary with the v0 native payload header:

```powershell
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- build-native-payload --input .\artifacts\apps\hello.raw.bin --output .\artifacts\payloads\hello.bin
```

The payload builder validates that `--input` exists and is nonempty, writes a 32-byte `OSAPPv0\0` header with version `0`, header size `32`, flags `0`, entry offset `32`, reserved `0`, then appends the input bytes byte-for-byte. Successful output is suitable for the host-tested `NativeAppPayloadHeader` parser and future package inclusion, but it does not prove executable mapping or app launch.

Successful output emits:

```text
TEST_START imagebuilder.native-payload
NATIVE_PAYLOAD_BYTES <bytes>
NATIVE_PAYLOAD_ENTRY_OFFSET 32
NATIVE_PAYLOAD_ARTIFACT <path>
TEST_PASS imagebuilder.native-payload
```

Native-payload failures emit `TEST_FAIL imagebuilder.native-payload <reason>` and return nonzero. Current reasons include:

- `native_payload_input_not_found`
- `native_payload_input_empty`

Build a validated single-file native app package from a kernel-facing native manifest and raw app bytes:

```powershell
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- build-native-app-package --manifest .\manifest.txt --input .\artifacts\apps\hello.raw.bin --output .\artifacts\packages\app.pkg
```

The command parses the native manifest, reads `app.path`, wraps the raw input bytes with the same `OSAPPv0` header used by `build-native-payload`, creates a deterministic single-file `OSPKGv0` package at that manifest-selected package path, validates the generated package with the same manifest/package/payload validation path as `validate-package`, and writes the package only after validation succeeds.

Successful output emits:

```text
TEST_START imagebuilder.native-app-package
NATIVE_APP_PACKAGE_PATH apps/hello.bin
NATIVE_APP_PACKAGE_RAW_APP_BYTES <bytes>
NATIVE_APP_PACKAGE_PAYLOAD_BYTES <bytes>
NATIVE_APP_PACKAGE_ENTRY_OFFSET 32
NATIVE_APP_PACKAGE_BYTES <bytes>
NATIVE_APP_PACKAGE_ARTIFACT <path>
TEST_PASS imagebuilder.native-app-package
```

Native-app-package failures emit `TEST_FAIL imagebuilder.native-app-package <reason>` and return nonzero. Current reasons include:

- `manifest_not_found`
- `manifest_empty`
- `manifest_too_large`
- `manifest_malformed`
- `manifest_launch_plan_invalid`
- `native_payload_input_not_found`
- `native_payload_input_empty`
- `package_output_too_large`
- `package_malformed`
- `package_app_file_missing`
- `package_app_payload_invalid`
- `package_app_file_invalid_path`
- `package_app_validation_failed`

Build a raw image with a manifest-selected native app package in one host command:

```powershell
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- build-native-app-image --efi .\artifacts\efi\BOOTX64.EFI --manifest .\manifest.txt --input .\artifacts\apps\hello.raw.bin --output .\artifacts\images\os.raw
```

The command first validates the raw-image inputs, builds a package with the same validated `build-native-app-package` path, then creates the raw GPT/FAT32 image with the existing raw image writer. The generated package path defaults to `<raw-image>.app.pkg`; `--package <path>` can override that intermediate package output path. The raw image contains:

```text
\EFI\BOOT\BOOTX64.EFI
\OS\MANIFEST.JSON
\OS\APP.PKG
```

After building a native app image, the QEMU smoke wrapper can dry-run the future app-readiness marker requirements without launching QEMU:

```powershell
dotnet run --project tools/OS.Tooling -- qemu smoke -DryRun -RequireNativeAppMarkers -Image .\artifacts\images\os.raw
```

This only proves the runner would require the existing boot smoke markers plus `APP_PACKAGE_READY`, `APP_LAUNCH_READY`, `APP_PAYLOAD_READY`, `APP_EXEC_MAPPING_READY`, `APP_EXEC_MEMORY_READY`, `APP_INVOCATION_READY`, and `APP_ENTRY_READY`. It does not prove that the image boots to those markers or that app code executes.

Successful output emits:

```text
TEST_START imagebuilder.native-app-image
NATIVE_APP_IMAGE_PACKAGE_PATH <path>
NATIVE_APP_IMAGE_RAW_PATH <path>
TEST_PASS imagebuilder.native-app-image
```

Native-app-image failures emit `TEST_FAIL imagebuilder.native-app-image <reason>` and return nonzero. Current reasons include the raw-image, manifest, native-payload, package-output, and package-validation reasons used by the composed commands, including:

- `efi_not_found`
- `manifest_not_found`
- `manifest_empty`
- `manifest_too_large`
- `manifest_malformed`
- `manifest_launch_plan_invalid`
- `native_payload_input_not_found`
- `native_payload_input_empty`
- `package_output_too_large`
- `package_malformed`
- `package_app_file_missing`
- `package_app_payload_invalid`
- `package_app_file_invalid_path`
- `package_app_validation_failed`

Validate an app package against a kernel-facing native manifest:

```powershell
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- validate-package --manifest .\manifest.txt --package .\artifacts\packages\app.pkg
```

The validator parses the native manifest, builds the host-side launch plan, parses the `OSPKGv0` package, opens the package file named by manifest `app.path`, and validates that selected file's `OSAPPv0` payload header. It does not execute app code.

Successful validation emits:

```text
TEST_START imagebuilder.package-validation
PACKAGE_APP_PATH apps/hello.bin
PACKAGE_APP_BYTES <bytes>
PACKAGE_APP_ENTRY_OFFSET 32
TEST_PASS imagebuilder.package-validation
```

Validation failures emit `TEST_FAIL imagebuilder.package-validation <reason>` and return nonzero. Current reasons include:

- `manifest_not_found`
- `manifest_empty`
- `manifest_too_large`
- `manifest_malformed`
- `manifest_launch_plan_invalid`
- `package_not_found`
- `package_empty`
- `package_too_large`
- `package_malformed`
- `package_app_file_missing`
- `package_app_payload_invalid`
- `package_app_file_invalid_path`
- `package_app_validation_failed`

Convert an existing raw image to qcow2:

```powershell
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- convert-qcow2 --input .\artifacts\images\os.raw --output .\artifacts\images\os.qcow2
```

Use an explicit `qemu-img` path when PATH discovery is not desirable:

```powershell
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- convert-qcow2 --input .\artifacts\images\os.raw --output .\artifacts\images\os.qcow2 --qemu-img "C:\Program Files\qemu\qemu-img.exe"
```

Dry-run conversion validates the input image and output path, prints deterministic discovery and command lines, and does not require `qemu-img` to exist:

```powershell
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- convert-qcow2 --input .\artifacts\images\os.raw --output .\artifacts\images\os.qcow2 --dry-run
```

Expected dry-run markers:

```text
TEST_START imagebuilder.qcow2
QEMU_IMG_DISCOVERY source=PATH requested=qemu-img resolved=<not-found> available=false
QCOW2_COMMAND "qemu-img" convert -f raw -O qcow2 "<input>" "<output>"
QCOW2_NO_WRITE <output>
TEST_PASS imagebuilder.qcow2
```

Real conversion with missing `qemu-img` emits:

```text
TEST_FAIL imagebuilder.qcow2 qemu_img_not_found
```

qcow2-specific input failures use stable reasons:

- `input_not_found`
- `input_empty`
- `qemu_img_not_found`
- `qemu_img_failed`

Create an Azure fixed VHD conversion plan from an existing raw image:

```powershell
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- convert-azure-vhd --input .\artifacts\images\os.raw --output .\artifacts\images\os.vhd --dry-run
```

Expected dry-run markers:

```text
TEST_START imagebuilder.azure-vhd
QEMU_IMG_DISCOVERY source=PATH requested=qemu-img resolved=<not-found> available=false
AZURE_VHD_COMMAND "qemu-img" convert -f raw -o subformat=fixed,force_size -O vpc "<input>" "<output>"
AZURE_VHD_NO_WRITE <output>
TEST_PASS imagebuilder.azure-vhd
```

`convert-azure-vhd` validates that the raw input exists, is non-empty, and has a virtual size aligned to 1 MiB before emitting the fixed VHD command. Real conversion requires `qemu-img`; dry-run mode does not.

Create an AWS AMI import metadata dry-run plan:

```powershell
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- plan-aws-ami --input .\artifacts\images\os.raw --format raw --architecture x86_64 --virtualization hvm --root-device ebs --boot-mode uefi --ena-support true --dry-run
```

Expected dry-run markers:

```text
TEST_START imagebuilder.aws-ami
AWS_AMI_PLAN {...}
AWS_AMI_IMPORT_DRY_RUN format=raw architecture=x86_64 virtualization=hvm rootDevice=ebs bootMode=uefi enaSupport=true
TEST_PASS imagebuilder.aws-ami
```

The AWS plan validates the first Nitro target metadata: `x86_64`, `hvm`, EBS-backed root, UEFI metadata for the current GPT/ESP raw image layout, and ENA support requested. It does not call AWS, upload to S3, create an AMI, or validate EC2 boot.

Describe an existing local image artifact:

```powershell
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- describe-image --input .\artifacts\images\os.raw --format raw
```

Expected metadata markers:

```text
TEST_START imagebuilder.image-metadata
IMAGE_METADATA {...}
IMAGE_METADATA_PATH <absolute-path>
IMAGE_METADATA_FORMAT raw
IMAGE_METADATA_BYTES <bytes>
IMAGE_METADATA_SHA256 <lowercase-sha256>
TEST_PASS imagebuilder.image-metadata
```

`describe-image` accepts `raw`, `qcow2`, `vhd`, `vhdx`, `vmdk`, `ova`, and
`iso` format labels. It validates that the input exists and is nonempty before
hashing. This is local artifact evidence only; it does not upload to a cloud
provider, create an import task, or prove that a provider can boot the image.

## Unsupported Behavior

ISO generation is pending.

The dry-run plan writes a default manifest when `--manifest` is omitted and reserves the app-package guest path when `--package` is omitted. It can plan and write manifest/package bytes when `--manifest` or `--package` is provided, `build-package` can create parser-valid package files from a host directory, and `validate-package` can validate manifest/package/payload consistency on the host. None of these paths prove guest app loading.

QEMU execution is handled by `tools/OS.QemuRunner` and the `OS.Tooling qemu smoke` and `OS.Tooling qemu qcow2` commands. The image builder's qcow2 conversion only produces the artifact; the qcow2 smoke command combines that conversion with the QEMU runner. The Azure fixed VHD, AWS AMI, and image metadata paths are local validation/dry-run or local inspection contracts only.

`dotnet run --project tools/OS.Tooling -- baseline evidence` and `dotnet run --project tools/OS.Tooling -- ci smoke` include qcow2 smoke-intent evidence by default. They create small intent placeholder images under the evidence log directory and invoke `qemu qcow2 --dry-run`; this validates command, marker, and workload-profile wiring without requiring `qemu-img`, QEMU, OVMF, or a real converted qcow2 artifact. Passing `--run-qcow2-real-smoke` to the CI or baseline command switches that lane to real qcow2 smoke execution.

The baseline and CI evidence commands also record
`qemu-img` discovery before planning or executing the qcow2 lane. They emit
`BASELINE_QEMU_IMG_DISCOVERY`/`CI_SMOKE_QEMU_IMG_DISCOVERY` and
`BASELINE_QCOW2_CONVERSION_INTENT`/`CI_SMOKE_QCOW2_CONVERSION_INTENT`, then
write matching `qemuImgDiscovery` and `qcow2ConversionIntent` objects into the
top-level summary and the qcow2 step. Missing `qemu-img` is a host-tool
prerequisite blocker for `--run-qcow2-real-smoke`; it is not a raw image generation
failure and does not block default dry-run intent evidence.

## Tests

`tests/OS.HostTests/ImageBuilderTests.cs` verifies:

- The dry-run plan emits `TEST_START imagebuilder.plan`, `IMAGE_PLAN`, `IMAGE_PLAN_ENTRY`, `IMAGE_PLAN_NO_WRITE`, and `TEST_PASS imagebuilder.plan`.
- The dry-run plan includes entries for the EFI System Partition, `\EFI\BOOT\BOOTX64.EFI`, `\OS\MANIFEST.JSON`, and `\OS\APP.PKG`.
- The default raw image includes `\OS\MANIFEST.JSON`, and the kernel-facing `NativeManifestLoader` can load it from the generated GPT/FAT32 image bytes.
- The dry-run plan includes manifest source path and byte count when `--manifest` is provided.
- The dry-run plan includes package source path and byte count when `--package` is provided.
- The dry-run plan leaves the requested output image path absent.
- The tool creates a 64 MiB raw image.
- The image has a protective MBR.
- Primary and backup GPT headers are present.
- The first GPT entry is an EFI System Partition at LBA 2048.
- The generated GPT layout remains compatible with the kernel storage parser contract; `TEST_PASS imagebuilder.gpt-parser-readback` feeds LBA 1 plus the primary GPT entry sectors from a generated raw image into `GptPartitionTableParser` and asserts the EFI System Partition descriptor LBA/range.
- The ESP has a FAT32 boot sector.
- `\EFI\BOOT\BOOTX64.EFI` is discoverable through FAT directory entries and matches the source bytes.
- `\OS\MANIFEST.JSON` is discoverable through FAT directory entries and matches the source bytes exactly.
- The default generated manifest is discoverable through FAT and parses as native manifest v0.
- `\OS\APP.PKG` is discoverable through FAT directory entries and matches the source bytes exactly.
- A parser-valid `\OS\APP.PKG` in a generated raw image can be read through `OsPackageLoader.LoadFromPrimaryGptEsp`.
- The package readback path verifies package header metadata and `bin/app.dll` content through `OsPackageParser.GetFileContent`.
- `build-package` creates a parser-valid package from a directory and emits `TEST_PASS imagebuilder.package-builds-directory`.
- `build-package` creates byte-identical package output for unchanged input and emits `TEST_PASS imagebuilder.package-builds-deterministically`.
- `build-package` rejects unsafe package paths and emits `TEST_PASS imagebuilder.package-build rejects unsafe input`.
- `build-native-payload` writes a parser-valid v0 native payload header and emits `TEST_PASS imagebuilder.native-payload writes v0 header`.
- `build-native-payload` preserves raw app bytes after the 32-byte header and emits `TEST_PASS imagebuilder.native-payload preserves app bytes`.
- `build-native-payload` rejects missing input and emits `TEST_PASS imagebuilder.native-payload rejects missing input`.
- `build-native-payload` rejects empty input and emits `TEST_PASS imagebuilder.native-payload rejects empty input`.
- `validate-package` verifies a manifest-selected package app payload and emits `TEST_PASS imagebuilder.package validates manifest app payload`.
- `validate-package` rejects missing manifest input and emits `TEST_PASS imagebuilder.package validation rejects missing manifest`.
- `validate-package` rejects malformed native manifests and emits `TEST_PASS imagebuilder.package validation rejects malformed manifest`.
- `validate-package` rejects missing package input and emits `TEST_PASS imagebuilder.package validation rejects missing package`.
- `validate-package` rejects packages without the manifest-selected app file and emits `TEST_PASS imagebuilder.package validation rejects missing app file`.
- `validate-package` rejects invalid selected native payload headers and emits `TEST_PASS imagebuilder.package validation rejects invalid payload header`.
- `build-native-app-package` builds a manifest-selected, validated single-file package and emits `TEST_PASS imagebuilder.native-app-package builds validated package`.
- `build-native-app-package` preserves the manifest-selected package path and emits `TEST_PASS imagebuilder.native-app-package preserves manifest app path`.
- `build-native-app-package` rejects malformed native manifests and emits `TEST_PASS imagebuilder.native-app-package rejects malformed manifest`.
- `build-native-app-package` rejects empty raw app input and emits `TEST_PASS imagebuilder.native-app-package rejects empty app input`.
- `build-native-app-image` creates a raw image with `BOOTX64.EFI`, `OS/MANIFEST.JSON`, and a parser-valid manifest-selected `OS/APP.PKG`, and emits `TEST_PASS imagebuilder.native-app-image creates raw image with package`.
- `build-native-app-image` rejects missing EFI input and emits `TEST_PASS imagebuilder.native-app-image rejects missing efi`.
- `build-native-app-image` rejects malformed native manifests and emits `TEST_PASS imagebuilder.native-app-image rejects malformed manifest`.
- `create-raw --kestrel-package` validates the `kestrel.smoke.package.v0`
  contract, stages the package root into a parser-valid `OSPKGv0` at
  `\OS\APP.PKG`, and emits `TEST_PASS imagebuilder.kestrel_package stages files`.
- `plan-raw --kestrel-package` records `WORKLOAD_PROFILE SingleMinimalApi`,
  `KESTREL_PACKAGE_CONTRACT kestrel.smoke.package.v0`, and the planned
  `\OS\APP.PKG` entry, and emits
  `TEST_PASS imagebuilder.kestrel_package records single profile`.
- `create-raw --kestrel-package` can consume the real
  `dotnet run --project tools/OS.Tooling -- kestrel package` output and emits
  `TEST_PASS imagebuilder.kestrel_package stages published package`.
- The generated raw ESP preserves the published MinimalApiSmoke package-file
  bytes and emits
  `TEST_PASS imagebuilder.kestrel_package readback preserves published files`.
- The generated raw ESP preserves the generated Kestrel package manifest bytes
  at `\OS\MANIFEST.JSON` and emits
  `TEST_PASS imagebuilder.kestrel_package readback preserves manifest json`.
- Invalid Kestrel package contracts fail deterministically with contract
  reasons such as `kestrel_package_missing_expected_env`, and emit
  `TEST_PASS imagebuilder.kestrel_package rejects invalid contract`.
- `MultiMinimalApi` is rejected for the Kestrel staging path until process and
  supervisor semantics exist, and emits
  `TEST_PASS imagebuilder.kestrel_package rejects multi profile`.
- Missing EFI input returns a machine-readable failure.
- Missing, empty, and oversized manifest input returns machine-readable failures.
- Missing, empty, and oversized package input returns machine-readable failures.
- Oversized raw image sizes that exceed the current FAT32 ESP sector-count
  geometry return `TEST_FAIL imagebuilder.plan image_too_large`.
- `convert-qcow2 --dry-run` emits deterministic `QEMU_IMG_DISCOVERY`, `QCOW2_COMMAND`, `QCOW2_NO_WRITE`, and `TEST_PASS imagebuilder.qcow2` markers without requiring `qemu-img`.
- Missing and empty qcow2 raw inputs return machine-readable failures.
- Real qcow2 conversion with an explicit missing `--qemu-img` returns `TEST_FAIL imagebuilder.qcow2 qemu_img_not_found`.
- Baseline evidence plan tests verify missing and found fake `qemu-img` paths
  are recorded in `BASELINE_QEMU_IMG_DISCOVERY`, top-level summary fields, and
  the `baseline.qemu.qcow2.*` step without executing conversion.
- CI smoke dry-run tests verify missing and found fake `qemu-img` paths are
  recorded in `CI_SMOKE_QEMU_IMG_DISCOVERY`, `ci-smoke.artifacts.json`,
  `ci-smoke.summary.json`, and the `ci.smoke.qemu.qcow2.*` step without
  executing conversion.
- `convert-azure-vhd --dry-run` emits deterministic `QEMU_IMG_DISCOVERY`, `AZURE_VHD_COMMAND`, `AZURE_VHD_NO_WRITE`, and `TEST_PASS imagebuilder.azure-vhd` markers without requiring `qemu-img`.
- `convert-azure-vhd` rejects unaligned raw input size with `TEST_FAIL imagebuilder.azure-vhd azure_vhd_unaligned_size`.
- `plan-aws-ami --dry-run` emits `AWS_AMI_PLAN`, `AWS_AMI_IMPORT_DRY_RUN`, and `TEST_PASS imagebuilder.aws-ami` for the first x86_64/HVM/EBS/UEFI/ENA Nitro target.
- `plan-aws-ami` rejects non-HVM virtualization, ARM64 for the first target, and legacy BIOS metadata for the current GPT/ESP/BOOTX64.EFI raw image layout.
- `describe-image` emits deterministic local artifact size and SHA-256 metadata, and emits `TEST_PASS imagebuilder.image_metadata emits size and sha256`.
- `describe-image` rejects empty input and emits `TEST_PASS imagebuilder.image_metadata rejects empty input`.
- `describe-image` rejects unsupported format labels and emits `TEST_PASS imagebuilder.image_metadata rejects unsupported format`.
- Workload profile plan tests verify `WORKLOAD_PROFILE SingleMinimalApi`,
  `WORKLOAD_PROFILE MultiMinimalApi`, `IMAGE_PLAN` `workloadProfile`, and
  invalid profile rejection with `TEST_FAIL imagebuilder.plan invalid_args`.
- ImageBuilder command tests execute the built tool DLL directly and fail fast with a deterministic missing-tool message if `OS.ImageBuilder.dll` was not produced by the outer build.

Relevant commands:

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- plan-raw --efi .\artifacts\efi\BOOTX64.EFI --manifest .\manifest.json --package .\app.pkg --output .\artifacts\images\os.raw
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- build-package --input .\artifacts\app-root --output .\artifacts\packages\app.pkg
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- build-native-payload --input .\artifacts\apps\hello.raw.bin --output .\artifacts\payloads\hello.bin
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- build-native-app-package --manifest .\manifest.txt --input .\artifacts\apps\hello.raw.bin --output .\artifacts\packages\app.pkg
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- build-native-app-image --efi .\artifacts\efi\BOOTX64.EFI --manifest .\manifest.txt --input .\artifacts\apps\hello.raw.bin --output .\artifacts\images\os.raw
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- validate-package --manifest .\manifest.txt --package .\artifacts\packages\app.pkg
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- create-raw --efi .\artifacts\efi\BOOTX64.EFI --manifest .\manifest.json --package .\app.pkg --output .\artifacts\images\os.raw
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- convert-qcow2 --input .\artifacts\images\os.raw --output .\artifacts\images\os.qcow2 --dry-run
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- convert-azure-vhd --input .\artifacts\images\os.raw --output .\artifacts\images\os.vhd --dry-run
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- plan-aws-ami --input .\artifacts\images\os.raw --format raw --architecture x86_64 --virtualization hvm --root-device ebs --boot-mode uefi --ena-support true --dry-run
dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- describe-image --input .\artifacts\images\os.raw --format raw
dotnet run --project tools/OS.Tooling -- qemu qcow2 -DryRun
dotnet run --project tools/OS.Tooling -- qemu qcow2 -RawImage artifacts/images/os.raw -Qcow2Image artifacts/images/os.qcow2 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -QemuImg "C:\Program Files\qemu\qemu-img.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15
```

Evidence from 2026-05-22 for issue #298:

- `.\scripts\run-qemu-qcow2-smoke.ps1 -DryRun -RawImage artifacts/images/os.raw -Qcow2Image artifacts/images/os.qcow2 -SerialLog artifacts/logs/qemu-smoke-qcow2.log -SummaryLog artifacts/logs/qemu-smoke-qcow2.summary.log` emitted `TEST_PASS imagebuilder.qcow2`, `TEST_PASS qemu.runner.dryrun`, and `TEST_PASS qemu.qcow2.smoke`.
- The qcow2 dry-run QEMU command included `if=none,id=esp,format=qcow2,file=artifacts/images/os.qcow2`.
- `.\scripts\run-qemu-qcow2-smoke.ps1 -RawImage artifacts/images/os.raw -Qcow2Image artifacts/images/os.qcow2 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -QemuImg "C:\Program Files\qemu\qemu-img.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15` emitted `TEST_PASS imagebuilder.raw`, `TEST_PASS imagebuilder.qcow2`, `TEST_PASS qemu.smoke`, and `TEST_PASS qemu.qcow2.smoke`.
- `artifacts/images/os.qcow2` was created at 720,896 bytes on this host.
- `artifacts/logs/qemu-smoke-qcow2.summary.log` reported `QEMU_SMOKE_STATUS pass`, `IMAGE_ARTIFACT artifacts/images/os.qcow2`, `MISSING_MARKERS none`, and `FAIL_REASON none`.
- `artifacts/logs/qemu-smoke-qcow2.log` contained `BOOT_MEMORY_REGIONS_READY`, `BOOT_MEMORY_REGION_COUNT 102`, `OS_BOOT_OK`, and `OS_SMOKE_PASS`.
- CI dry-run on 2026-05-22 planned `ci.smoke.qemu.qcow2.dryrun` with an evidence-directory raw intent input, an evidence-directory qcow2 intent output, `qemu-qcow2-serial.log`, and `qemu-qcow2.summary.log`. This is not qcow2 conversion proof and does not replace the real qcow2 smoke evidence above.

Evidence from 2026-05-22 for issue #224:

- `dotnet build .\CLROS.slnx --nologo -m:1` passed with 0 warnings/errors.
- A temporary focused harness that compiled only `tests/OS.HostTests/ImageBuilderTests.cs` emitted `TEST_PASS imagebuilder.gpt-parser-readback`.
- The focused run also emitted the existing image-builder package, manifest, native-app, and qcow2 host markers.
- Integrated host-harness evidence is tracked in the current batch log after the GPT CRC integration landed; earlier focused evidence is retained only as the issue-local proof recorded before integration.
- No QEMU smoke was run for this step, and no boot, virtio-blk, or guest storage claim is made.

Evidence from 2026-05-22:

- `dotnet build .\CLROS.slnx --nologo -m:1` passed with 0 warnings/errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build *> .\artifacts\logs\host-tests-integrated-177-183.log` emitted `TEST_PASS host.tests`.
- The same host-test run emitted `TEST_PASS imagebuilder.package validation rejects missing app file` while running ImageBuilder through the built `OS.ImageBuilder.dll` path.
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `.\scripts\run-qemu-smoke.ps1 -DryRun -RequireNativeAppMarkers` emitted `TEST_PASS qemu.runner.dryrun`.
- `git diff --check` exited 0; it reported only line-ending normalization warnings.

Evidence from 2026-05-22:

- `dotnet build .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj --nologo` passed with 0 warnings/errors.
- A focused `build-native-app-image` run emitted `TEST_START imagebuilder.native-app-image`, `NATIVE_APP_IMAGE_PACKAGE_PATH <temp>\native-app-image.raw.app.pkg`, `NATIVE_APP_IMAGE_RAW_PATH <temp>\native-app-image.raw`, and `TEST_PASS imagebuilder.native-app-image`.
- Focused negative runs emitted `TEST_FAIL imagebuilder.native-app-image efi_not_found` with exit code 3 and `TEST_FAIL imagebuilder.native-app-image manifest_malformed` with exit code 23.
- `dotnet build .\CLROS.slnx --nologo -m:1` was attempted but is currently blocked by an out-of-scope `NativeAppInvocationPlanTests.cs` compile error in the app-invocation planning area.
- No QEMU smoke was run for this step, and no boot or app-execution claim is made.

Evidence from 2026-05-22:

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` passed with 0 warnings/errors after an earlier retry was blocked by an out-of-scope live `.NET Host` process holding test output DLLs.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted `TEST_PASS host.tests`.
- The same host-test run emitted `TEST_PASS imagebuilder.kestrel_package stages published package`.
- The same host-test run emitted `TEST_PASS imagebuilder.kestrel_package readback preserves published files`.
- The same host-test run emitted `TEST_PASS imagebuilder.kestrel_package readback preserves manifest json`.
- This is host image-builder/package evidence only; no QEMU, guest package read, hosted .NET launch, socket bind, or Kestrel runtime claim is made.

Evidence from 2026-05-22:

- `dotnet build .\CLROS.slnx --nologo -m:1` passed with 0 warnings/errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted `TEST_PASS host.tests`.
- The host test run emitted `TEST_PASS imagebuilder.native-app-package builds validated package`.
- The same run emitted `TEST_PASS imagebuilder.native-app-package preserves manifest app path`.
- The same run emitted `TEST_PASS imagebuilder.native-app-package rejects malformed manifest`.
- The same run emitted `TEST_PASS imagebuilder.native-app-package rejects empty app input`.

Evidence from 2026-05-22:

- `dotnet build .\CLROS.slnx --nologo -m:1` passed with 0 warnings/errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted `TEST_PASS host.tests`.
- The host test run emitted `TEST_PASS imagebuilder.package validates manifest app payload`.
- The same run emitted `TEST_PASS imagebuilder.package validation rejects missing manifest`.
- The same run emitted `TEST_PASS imagebuilder.package validation rejects malformed manifest`.
- The same run emitted `TEST_PASS imagebuilder.package validation rejects missing package`.
- The same run emitted `TEST_PASS imagebuilder.package validation rejects missing app file`.
- The same run emitted `TEST_PASS imagebuilder.package validation rejects invalid payload header`.

Evidence from 2026-05-22:

- `dotnet build .\CLROS.slnx --nologo -m:1` passed with 0 warnings/errors.
- A full host-test run emitted `TEST_PASS host.tests`.
- The host-test run emitted `TEST_PASS imagebuilder.package-builds-directory`.
- The same run emitted `TEST_PASS imagebuilder.package-builds-deterministically`.
- The same run emitted `TEST_PASS imagebuilder.package-build rejects unsafe input`.
- The same run emitted `TEST_PASS imagebuilder.package-file` and `TEST_PASS imagebuilder.package-readback`.
- The same run also emitted `TEST_PASS imagebuilder creates raw ESP with manifest and APP.PKG` and `TEST_PASS storage.package loader reads package from gpt esp`.

Evidence from 2026-05-22:

- `dotnet build .\CLROS.slnx --nologo -m:1` passed with 0 warnings/errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted `TEST_PASS host.tests`.
- The host test run emitted `TEST_PASS imagebuilder.manifest-file`.
- The generated default manifest was loaded through `NativeManifestLoader.LoadFromPrimaryGptEsp`.

Evidence from 2026-05-22:

- `dotnet build .\CLROS.slnx --nologo -m:1` passed with 0 warnings/errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted `TEST_PASS host.tests`.
- The host test run emitted `TEST_PASS imagebuilder emits dry-run package placement plan without writing image`.
- The same run emitted `TEST_PASS imagebuilder creates raw ESP with manifest and APP.PKG`.
- The same run emitted `TEST_PASS imagebuilder rejects missing package`, `TEST_PASS imagebuilder rejects empty package`, and `TEST_PASS imagebuilder rejects oversized package`.

Evidence from 2026-05-22:

- `dotnet build .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj --nologo` passed with 0 warnings/errors.
- `dotnet build .\CLROS.slnx --nologo -m:1` passed with 0 warnings/errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted `TEST_PASS host.tests`.
- The host test run emitted `TEST_PASS imagebuilder emits dry-run qcow2 conversion without qemu-img`.
- The same run emitted `TEST_PASS imagebuilder rejects missing qcow2 input`, `TEST_PASS imagebuilder rejects empty qcow2 input`, and `TEST_PASS imagebuilder rejects missing qemu-img in real qcow2 mode`.

Evidence from 2026-05-22:

- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted `TEST_PASS imagebuilder emits dry-run manifest placement plan without writing image`.
- The same run emitted `TEST_PASS imagebuilder creates raw ESP with MANIFEST.JSON`.
- The same run emitted `TEST_PASS imagebuilder rejects missing manifest`, `TEST_PASS imagebuilder rejects empty manifest`, and `TEST_PASS imagebuilder rejects oversized manifest`.

Evidence from 2026-05-22:

- `dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj --no-build -- plan-raw --efi <temp>\BOOTX64.EFI --output <temp>\os.raw --image-size-mib 64` emitted `TEST_PASS imagebuilder.plan`.
- The same dry-run emitted `IMAGE_PLAN_NO_WRITE <temp>\os.raw`.
- The dry-run target output path remained absent after the command.

Evidence from 2026-05-21:

- `dotnet run --project .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj -- create-raw --efi .\artifacts\efi\BOOTX64.EFI --output .\artifacts\images\os.raw` emitted `TEST_PASS imagebuilder.raw`.
- `artifacts/images/os.raw` is 67,108,864 bytes.
- QEMU/OVMF loaded `\EFI\BOOT\BOOTX64.EFI` from the generated image during `scripts/run-qemu-smoke.ps1`.

## Known Limitations

The FAT32 writer is intentionally minimal and only creates the ESP directory tree needed for the current boot artifact, optional manifest, and optional app package. It does not implement a general-purpose FAT writer.

`MANIFEST.JSON` is represented with a VFAT long-file-name directory entry because the `.JSON` extension is not an 8.3 FAT short-name extension.

The dry-run layout plan is a host-side contract for the current raw ESP layout. It is not a substitute for byte-level image validation after `create-raw`.

The image builder uses deterministic GPT and volume identifiers for reproducible host-side output. Future work may add configurable identifiers if cloud import tooling needs unique disks.

## Next Steps

1. Add ISO generation if it remains useful.
2. Include image metadata output in CI/baseline artifact summaries when useful.
3. Extend native app package tooling when needed to stage multi-file app config or hosted-workload archives; the current one-step command intentionally builds one native payload file selected by `app.path`.
