# Step 102 - Default ESP Manifest Readback

## What Changed

- Updated `tools/OS.ImageBuilder` so every generated raw ESP includes `\OS\MANIFEST.JSON`.
- Added a deterministic native manifest v0 default when `--manifest` is omitted.
- Added host-test coverage that builds a raw image and loads the generated manifest through `NativeManifestLoader.LoadFromPrimaryGptEsp`.

## Why It Changed

Issue #120 closes the gap between image creation and the kernel-facing manifest loader. The generated image now contains a manifest target by default, so future boot/storage work has a deterministic file to read.

## Files Changed

- `tools/OS.ImageBuilder/Program.cs`
- `tests/OS.HostTests/ImageBuilderTests.cs`
- `docs/image-builder.md`
- `docs/manifest.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build > .\artifacts\logs\host-tests-current.log
```

## Logs And Evidence

- Build passed with 0 warnings and 0 errors.
- Host tests emitted:
  - `TEST_PASS imagebuilder.manifest-file`
  - `TEST_PASS host.tests`

## Failures

- Earlier overlapping worker verification runs left host-test/image-builder child processes active. Final verification was rerun once from the parent after stopping stale processes and passed.

## Deferred Work

- No guest disk reads.
- No boot-time manifest load.
- No app launch.
- No JSON-to-native manifest conversion.

## Evidence Boundary

This step proves host-generated raw image bytes contain a loadable native manifest. It is not virtio-blk, post-ExitBootServices storage, app launch, hosted .NET 10, or Kestrel evidence.
