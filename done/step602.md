# Step 602 - Image Artifact Metadata Command

## Summary
Added a host-only `OS.ImageBuilder describe-image` command that validates an
existing image artifact, emits its format label, byte length, and lowercase
SHA-256 digest, and records those fields as stable machine-readable markers.

## Why
Cloud import and release workflows need local artifact metadata before any
provider upload/import step. This slice gives raw/qcow2/vhd/iso artifacts a
deterministic size/hash evidence path without claiming cloud-provider import or
boot support.

## Files changed
- `tools/OS.ImageBuilder/Program.cs`
  - Added `describe-image` command parsing and `imagebuilder.image-metadata`
    marker mapping.
  - Added `ImageArtifactMetadataWriter` for local input validation, supported
    format normalization, SHA-256 calculation, JSON metadata, and line-oriented
    metadata markers.
- `tests/OS.HostTests/ImageBuilderTests.cs`
  - Added host tests for metadata emission, empty input rejection, and
    unsupported format rejection.
- `docs/image-builder.md`
  - Documented the command, expected markers, test coverage, and evidence
    boundary.

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Passed with 0 errors. The first attempt reported transient MSBuild copy
    retry warnings because an existing `OS.HostTests` process temporarily held
    test output DLLs.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 -t:Rebuild /p:OSWorkloadProfile=SingleMinimalApi`
  - Final rerun passed with 0 warnings and 0 errors after stale
    MultiMinimalApi build outputs and stale host-test locks were cleared.
- `dotnet build .\tools\OS.ImageBuilder\OS.ImageBuilder.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi`
  - Passed with 0 warnings and 0 errors after the help text correction.
- Focused host-test marker probe against the rebuilt `OS.HostTests.exe`
  - Started the host-test executable, waited for the three new metadata test
    markers, printed them, then stopped the process before the rest of the
    long harness continued.
- Direct tool command:
  - `dotnet C:\Projects\OS\tools\OS.ImageBuilder\bin\Debug\net10.0\OS.ImageBuilder.dll describe-image --input artifacts\logs\image-metadata-command\metadata.raw --format raw`
- `git diff --check -- tools/OS.ImageBuilder/Program.cs tests/OS.HostTests/ImageBuilderTests.cs docs/image-builder.md done/step602.md`
  - Passed. Git reported only existing LF-to-CRLF normalization warnings for
    touched text files.

## Evidence
- Host-test markers observed:
  - `TEST_PASS imagebuilder.image_metadata emits size and sha256`
  - `TEST_PASS imagebuilder.image_metadata rejects empty input`
  - `TEST_PASS imagebuilder.image_metadata rejects unsupported format`
- `describe-image` command markers implemented:
  - `TEST_START imagebuilder.image-metadata`
  - `IMAGE_METADATA {...}`
  - `IMAGE_METADATA_PATH <absolute-path>`
  - `IMAGE_METADATA_FORMAT <format>`
  - `IMAGE_METADATA_BYTES <bytes>`
  - `IMAGE_METADATA_SHA256 <lowercase-sha256>`
  - `TEST_PASS imagebuilder.image-metadata`
- Direct tool evidence for a six-byte raw fixture:
  - `IMAGE_METADATA_BYTES 6`
  - `IMAGE_METADATA_SHA256 2a2f856eb1c65f8d9ee6af1a817f672b498e6147fd91eb401aaa85f8f8027754`
- Gitea issue #7 evidence comment:
  - `http://192.168.2.42:3000/michael.quint/OS/issues/7#issuecomment-18003`

## Failures
- A full host-test run was initially blocked by stale build/profile state:
  default image-builder tests were running against MultiMinimalApi tool output.
  A forced SingleMinimalApi rebuild corrected that stale output.
- A later full host-test run exited before completion while running existing
  MinimalApiSmoke publish-package coverage. The new image metadata tests had
  already emitted their pass markers. The stale host-test process was stopped
  and a clean SingleMinimalApi rebuild passed afterward.

## Deferred work
- Wire image metadata into CI/baseline artifact summaries if release evidence
  needs a single metadata manifest.
- Add provider-specific upload/import tooling only after the image and boot
  gates have the required local evidence.
- No AWS, Azure, GCE, QEMU boot, or libvirt boot claim is made here.

