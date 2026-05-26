# Step 325 - Native App Selection Loader

## Summary

Added `OS.Manifest.NativeAppSelectionLoader`, a host-tested composition boundary that reads the kernel native manifest and app package from the same caller-buffer GPT ESP image, parses the package, builds the native app launch plan, and validates the selected app descriptor.

## Why

The app-model backlog needed a small bridge between the already host-tested manifest/package storage loaders and the boot readiness helpers. This keeps the selected-app evidence status-coded and caller-buffer based without claiming live virtio-blk reads, executable mapping, app launch, hosted .NET, Minimal API, or Kestrel support.

## Files changed

- `src/OS.Kernel/Manifest/NativeAppSelectionLoader.cs`
- `tests/OS.HostTests/NativeAppSelectionLoaderTests.cs`
- `docs/app-model.md`
- `docs/manifest.md`
- `docs/cstorage.md`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/completion-gates.md`
- `done/step325.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`
- `rg --files src std apps | Where-Object { $_ -match '\.(c|cpp|h|asm|s)$' }`

## Evidence

- Host test project build passed with 0 warnings and 0 errors.
- Full host harness passed and emitted:

```text
TEST_PASS appmodel selection loader composes image inputs
TEST_PASS appmodel selection loader rejects manifest load
TEST_PASS appmodel selection loader rejects package load
TEST_PASS appmodel selection loader rejects launch capability
TEST_PASS appmodel selection loader rejects descriptor failure
TEST_PASS host.tests
```
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` exited 0. Git reported existing LF-to-CRLF working-copy warnings only.
- Forbidden first-party source scan produced no `.c`, `.cpp`, `.h`, `.asm`, or `.s` paths under `src`, `std`, or `apps`.

## Failures

- Gitea issue updates are still blocked by missing write credentials in the current task context; API write attempts return `token is required`.

## Deferred work

- Wire `NativeAppSelectionLoader` into the real boot path after fixed boot-owned scratch buffers and a live block-device implementation exist.
- Continue toward live package payload mapping, executable memory materialization, app ABI definition, and native app launch before any hosted .NET or Kestrel claim.
