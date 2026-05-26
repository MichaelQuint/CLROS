# Step 788 - PAL config table shared buffer copies

## Summary

Migrated `OSHostConfigEnvironmentTable` caller-buffer byte copies onto
`OS.Std.Memory.BufferOperations.MemoryCopy` and added a host source guard for
the shared copy contract.

## Why

PAL config/environment lookup, enumeration, and source-backed listen endpoint
construction should use the shared no-runtime memory helper instead of direct
span `CopyTo` calls. This keeps the host-tested config/environment boundary
aligned with the repository's shared `std/no-runtime` memory surface.

## Files changed

- `src/OS.Kernel/Pal/OSHostConfigEnvironmentTable.cs`
  - uses `BufferOperations.MemoryCopy` for config/environment lookup values;
  - uses `BufferOperations.MemoryCopy` for config/environment enumeration keys
    and values;
  - uses `BufferOperations.MemoryCopy` for listen endpoint host and port bytes;
  - maps impossible helper failures after existing bounds checks to the
    existing `BufferTooSmall` or launch-plan `InvalidArgument` status paths.
- `tests/OS.HostTests/PalConfigEnvironmentTests.cs`
  - adds `TEST_PASS pal.config table uses shared buffer operations`;
  - source-guards the `OS.Std.Memory` import, expected memory-copy calls, and
    removal of the direct `CopyTo` candidates.
- `docs/pal.md`
  - records the host-tested shared-buffer-copy boundary and marker.
- `done/step788.md`
  - records this milestone and evidence.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 -p:BaseOutputPath=C:\Projects\OS\artifacts\issue745-pal-config-bufferops\bin\`
- `dotnet .\artifacts\issue745-pal-config-bufferops\bin\Debug\net10.0\OS.HostTests.dll`
- `git -c core.autocrlf=false diff --check -- .\src\OS.Kernel\Pal\OSHostConfigEnvironmentTable.cs .\tests\OS.HostTests\PalConfigEnvironmentTests.cs .\docs\pal.md .\done\step788.md`
- `rg -n '[ \t]$' .\src\OS.Kernel\Pal\OSHostConfigEnvironmentTable.cs .\tests\OS.HostTests\PalConfigEnvironmentTests.cs .\docs\pal.md .\done\step788.md`

## Evidence

- `artifacts/logs/issue745-host-build-20260525.log`
  - build completed with 0 warnings and 0 errors.
- `artifacts/logs/issue745-hosttests-20260525.log`
  - `TEST_PASS pal.config table uses shared buffer operations`
  - `TEST_PASS host.tests`
- `artifacts/logs/issue745-diff-check-20260525.log`
  - `TEST_PASS issue745.diff-check`
- `artifacts/logs/issue745-trailing-whitespace-20260525.log`
  - `TEST_PASS issue745.trailing-whitespace none`
- `artifacts/logs/issue745-copyto-scan-20260525.log`
  - `TEST_PASS issue745.copyto-scan none`

## Failures

- The issue's exact focused build command with a shared
  `BaseIntermediateOutputPath` failed before compile with
  `Cannot create a file when that file already exists`; logged at
  `artifacts/logs/issue745-host-build-exact-failed-20260525.log`.
- A follow-up attempt with a custom intermediate path exposed existing
  dirty-worktree `tools/OS.QemuRunner/obj` generated files as duplicate
  assembly attributes; logged at
  `artifacts/logs/issue745-host-build-custom-obj-failed-20260525.log`.
- No #745 implementation or host-test failures were observed after using the
  artifact output path with default project intermediates.
- This is host-tested contract evidence only. It is not QEMU boot, live PAL
  export, hosted CoreCLR, ASP.NET configuration, or Kestrel evidence.

## Deferred work

- Continue migrating remaining PAL/kernel byte-copy sites onto shared
  no-runtime helpers in separate issue-sized slices.
- Live PAL config/environment exports and hosted runtime consumption remain
  future work.

## Evidence boundary

The PAL config/environment table now has host source-guard evidence for shared
buffer helper use in lookup, enumeration, and listen-endpoint assembly paths.
It remains an in-process host model over caller-provided/source-backed bytes and
does not export `OSHost_*` symbols, read guest storage, provide process
environment variables, start hosted .NET, or prove Kestrel.

