# Step 790 - OSHost package file reads use shared buffer copy

## Summary

Migrated the in-process PAL package file table `ReadAt` content copy from direct
`Span.CopyTo` to `OS.Std.Memory.BufferOperations.MemoryCopy`.

Added a host source guard marker so the package file table path keeps using the
shared no-runtime buffer operation.

## Why

PAL package reads are part of the host-tested storage/PAL boundary. Routing the
bounded payload copy through `OS.Std.Memory.BufferOperations` keeps the package
file table aligned with the shared no-runtime memory helper surface and gives the
host harness a stable guard against reintroducing direct span copies.

## Files changed

- `src/OS.Kernel/Pal/OSHostPackageFileTable.cs`
- `tests/OS.HostTests/PalPackageFileTests.cs`
- `docs/pal.md`
- `docs/cstorage.md`
- `done/step790.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check -- .\src\OS.Kernel\Pal\OSHostPackageFileTable.cs .\tests\OS.HostTests\PalPackageFileTests.cs .\docs\pal.md .\docs\cstorage.md`
- Scoped trailing whitespace scan over the changed source, test, docs, and done
  step files.

## Evidence

- `artifacts/issue747-pal-packagefile-bufferops/focused-build-normal.log`
  contains `Build succeeded.`
- `artifacts/logs/issue747-focused-build-normal.log` mirrors the focused build.
- `artifacts/issue747-pal-packagefile-bufferops/full-host-tests-normal.log`
  contains:
  - `TEST_PASS pal.package file table read uses shared buffer operations`
  - `TEST_PASS host.tests`
- `artifacts/logs/issue747-full-host-tests-normal.log` mirrors the full host
  test run.
- `artifacts/issue747-pal-packagefile-bufferops/scoped-diff-check.log`
- `artifacts/logs/issue747-scoped-diff-check.log`
- `artifacts/issue747-pal-packagefile-bufferops/trailing-whitespace-scan.log`
- `artifacts/logs/issue747-trailing-whitespace-scan.log`

## Failures

- An earlier normal focused build attempt was blocked by a concurrent `.NET
  Host` process holding `tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll`.
- An isolated full host-test attempt avoided that lock but broke tests that
  expect default tool output locations. After the concurrent lock cleared, the
  normal focused build and normal full host-test run passed.

## Deferred work

- No live virtio-blk, exported `OSHost_*` ABI, real `ExitBootServices`, app
  execution, hosted .NET, Minimal API, or Kestrel behavior changed or verified.
- Evidence remains limited to host-tested in-process PAL package table behavior
  and source guards.

