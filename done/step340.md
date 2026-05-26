# Step 340 - App Selection Backup GPT Fallback Evidence

## Summary

Added host-test coverage showing `NativeAppSelectionLoader` inherits the
caller-buffer backup GPT fallback used by the manifest and package loaders.

## Why

The selection loader composes manifest loading, package loading, package
parsing, launch planning, and launch descriptor validation over one injected
block-device image. After backup GPT fallback was added below that layer, the
selection path needed direct evidence that it still succeeds when the primary
GPT header is invalid but the backup GPT metadata is valid.

## Files changed

- `tests/OS.HostTests/NativeAppSelectionLoaderTests.cs`
- `docs/app-model.md`
- `docs/manifest.md`
- `docs/completion-gates.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- Solution build passed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS appmodel selection loader gpt falls back to backup`.
- Host tests emitted `TEST_PASS host.tests`.
- `git diff --check` passed with line-ending warnings only.

## Failures

- None in the final verification run.

## Deferred work

- The selection-loader fallback is still host-tested over an in-memory block
  device only.
- Boot does not yet own fixed scratch buffers for this path.
- No live virtio-blk, post-`ExitBootServices` disk I/O, app execution, hosted
  .NET, Minimal API, or Kestrel behavior is proven.

## Evidence boundary

This slice is selection-loader composition evidence only. It proves the host
selection path can use backup GPT metadata through existing caller-buffer
manifest/package loaders when primary GPT location fails. It does not prove
boot-time package loading, cloud-provider disk boot behavior, hosted .NET,
Kestrel, or production/cloud readiness.

