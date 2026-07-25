# Step 129 - ImageBuilder Native Payload Generation

## What Changed

- Added `tools/OS.ImageBuilder build-native-payload --input <raw-bin> --output <payload-bin>`.
- The command writes a 32-byte `OSAPPv0\0` v0 native payload header, with version `0`, header size `32`, flags `0`, entry offset `32`, reserved `0`, then appends the input bytes unchanged.
- Added host tests for header generation, byte preservation, missing input rejection, and empty input rejection.
- Updated image-builder, app-model, manifest, and completion-gate docs.

## Why It Changed

Issue #145 needed a host-side way to produce the minimal native payload shape consumed by the existing `NativeAppPayloadHeader` validation path. This keeps payload generation deterministic without claiming executable loading or app launch.

## Files Changed

- `tools/OS.ImageBuilder/Program.cs`
- `tests/OS.HostTests/ImageBuilderTests.cs`
- `docs/image-builder.md`
- `docs/app-model.md`
- `docs/manifest.md`
- `docs/completion-gates.md`
- `done/step129.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
git diff --check
```

## Logs And Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted:
  - `TEST_PASS imagebuilder.native-payload writes v0 header`
  - `TEST_PASS imagebuilder.native-payload preserves app bytes`
  - `TEST_PASS imagebuilder.native-payload rejects missing input`
  - `TEST_PASS imagebuilder.native-payload rejects empty input`
  - `TEST_PASS host.tests`
- `git diff --check` exited successfully. It still printed existing line-ending normalization warnings.

## Failures

- None in this verification run.

## Deferred Work

- No executable mapping, relocation, stack allocation, app ABI entry, scheduler/thread startup, stdout/stderr routing, exit-code capture, hosted .NET 10 runtime, ASP.NET Minimal API launch, or Kestrel behavior was added.

## Evidence Boundary

This step proves host-side native payload byte generation only. It does not prove boot-time package loading, complete executable-format validation, native app execution, hosted .NET 10, ASP.NET Minimal API, or Kestrel.
