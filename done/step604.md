# Step 604 - Std Array Helpers Slice

## Summary

Added a host-tested `OS.Std.ArrayHelpers` slice for unmanaged span clear, fill,
linear search, reverse search, and BCL-shaped binary-search insertion-point
behavior.

## Why

Gitea issue #3 tracks the missing shared std/no-runtime BCL/runtime helper base.
This adds a small array/search helper slice under `std/no-runtime/shared` before
kernel or app code grows another local implementation.

## Files changed

- `std/no-runtime/shared/ArrayHelpers.cs`
  - Added partial, OS-specific unmanaged-span helpers: `Clear`, `Fill`,
    `IndexOf`, `LastIndexOf`, and `BinarySearch`.
- `tests/OS.HostTests/StdArrayHelpersTests.cs`
  - Added marker-based host tests for empty spans, clear/fill boundaries,
    duplicate search behavior, and binary-search insertion-point complements.
- `docs/nativeaot-nostdlib-limits.md`
  - Documented the supported subset, tests, and non-`System.Array` boundary.
- `docs/completion-gates.md`
  - Added Gate 4 std/runtime evidence for the new helper slice without
    changing gate status.

## Tests run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
.\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe | Select-String -Pattern 'TEST_FAIL|Unhandled|Exception|std\.arrayhelpers|host\.tests'
dotnet run --project .\artifacts\tmp\StdArrayHelpersFocus\StdArrayHelpersFocus.csproj --no-restore
dotnet build .\OS.slnx --nologo -m:1
```

## Evidence

- Host-test project build passed with 0 warnings and 0 errors.
- The full host harness, after the broader solution build refreshed tool
  outputs, emitted:
  - `TEST_PASS std.arrayhelpers clear fill unmanaged spans`
  - `TEST_PASS std.arrayhelpers indexof unmanaged spans`
  - `TEST_PASS std.arrayhelpers binarysearch insertion points`
  - `TEST_PASS host.tests`
- The focused temporary runner emitted the same three `std.arrayhelpers`
  markers before the full harness rerun succeeded.
- Solution build passed with 0 warnings and 0 errors.

## Failures

- An initial shared-host-harness run failed before the new std markers because
  `ManifestToolTests` spawned a stale manifest-tool output that could not load
  `OS.Kernel`. A subsequent `dotnet build .\OS.slnx --nologo -m:1` refreshed
  outputs, and the filtered full harness rerun emitted the new markers plus
  `TEST_PASS host.tests`.

## Deferred work

- This is not a complete `System.Array` replacement.
- Managed-reference element types, array allocation/resizing, sort, comparer
  overloads, covariance, multidimensional arrays, and sorted-input validation
  remain deferred.
- Gate 4 remains failed until the broader BCL subset, NativeAOT runtime
  helpers, GC, exceptions, and dispatch evidence exist.

## Evidence boundary

This proves only host-built std/no-runtime helper behavior for caller-provided
unmanaged spans. It does not prove a freestanding runtime, GC, exception
support, boot integration, kernel allocation, storage, networking, PAL exports,
hosted .NET, or Kestrel.
