# Step 273 - MinimalApiSmoke raw ESP package round trip

## What changed

- Added host tests that run `apps/MinimalApiSmoke/publish-kestrel-smoke-package.ps1` into a temporary output root, require the script's package/contract markers, and stage the published package with `tools/OS.ImageBuilder create-raw --kestrel-package`.
- Added raw ESP readback checks for `\OS\MANIFEST.JSON` and `\OS\APP.PKG` using the existing in-memory GPT/FAT/package parser path.
- Verified the required MinimalApiSmoke package files are present in the staged `OSPKGv0` package and match the published file bytes exactly:
  - `apps/minimal-api/MinimalApiSmoke.dll`
  - `apps/minimal-api/MinimalApiSmoke.deps.json`
  - `apps/minimal-api/MinimalApiSmoke.runtimeconfig.json`
  - `apps/minimal-api/appsettings.json`
- Updated image-builder and app-model docs to describe the host-only evidence boundary.

## Why it changed

Gitea #273 needed proof that the real MinimalApiSmoke publish output can move through the current image-builder path, land at the stable ESP package path, and be read back through existing host storage/package parsers. This unblocks more detailed hosted-workload packaging work without claiming guest execution or Kestrel support.

## Files changed

- `tests/OS.HostTests/ImageBuilderTests.cs`
- `docs/image-builder.md`
- `docs/app-model.md`
- `done/step273.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` passed with 0 warnings/errors after an earlier retry was blocked by an out-of-scope live `.NET Host` process holding test output DLLs.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted:
  - `TEST_PASS imagebuilder.kestrel_package stages published package`
  - `TEST_PASS imagebuilder.kestrel_package readback preserves published files`
  - `TEST_PASS imagebuilder.kestrel_package readback preserves manifest json`
  - `TEST_PASS host.tests`

## Failures

- The first build attempt failed because another active `.NET Host` process held `tests/OS.HostTests/bin/Debug/net10.0/OS.Kernel.dll` and `OS.Std.NoRuntime.Shared.dll`. The lock cleared, and the rerun passed.

## Deferred work

- No QEMU boot-time package read was attempted.
- No guest virtio-blk package read is implemented.
- No hosted .NET 10 launch, ASP.NET Minimal API execution, socket bind, TCP listener, or in-guest Kestrel support is proven by this step.
- The live boot path still does not consume the staged Kestrel package.
