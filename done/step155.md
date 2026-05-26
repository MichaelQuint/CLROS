# Step 155 - no-runtime StartsWith and EndsWith Coverage

## What Changed

- Verified the existing `OS.Std.MemoryExtensions.StartsWith` and `EndsWith` byte-span helpers as the shared no-runtime prefix/suffix implementation.
- Added a focused host-test marker for empty prefix/suffix, exact matches, positive prefix/suffix matches, mismatches, and too-long prefix/suffix inputs.
- Updated no-runtime limits and completion-gate docs.

## Why It Changed

Gitea issue #176 called for byte-span `StartsWith` and `EndsWith` in `std/no-runtime/shared` before packet, manifest, and package code grows local duplicate prefix/suffix checks. The helpers already existed, so this step adds explicit focused evidence and documentation instead of duplicating code.

## Files Changed

- `tests/OS.HostTests/StdMemoryExtensionsTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- `done/step155.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors after generated-output exclusions were added to shared projects.
- Host-test execution emitted:
  - `TEST_PASS std.memoryextensions byte span startswith endswith`
  - `TEST_PASS std.memoryextensions byte span helpers`
  - `TEST_PASS host.tests`

## Failures

- An overlapping build/test attempt locked `OS.HostTests.exe`; that run was discarded and rerun cleanly.
- A later host-test run exposed generated-output inclusion in `OS.Kernel`, which was fixed by excluding `bin/**`, `obj/**`, and `artifacts/**` from default compile items.

## Deferred Work

- No `System.MemoryExtensions` canonical namespace exposure was added.
- No string/span generic overloads were added.
- No kernel migration work was done in this slice.

## Evidence Boundary

This is host-tested shared utility evidence only. It does not complete the wider BCL subset, runtime helper, GC, exception, or class-constructor gates.
