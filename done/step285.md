# Step 285 - OS.Std Byte Span TryCopyTo Helper

## What Changed

- Added `OS.Std.MemoryExtensions.TryCopyTo(ReadOnlySpan<byte>, Span<byte>)`.
- Preserved the existing throwing `CopyTo` contract.
- Made `TryCopyTo` return `false` for too-small destinations before writing
  anything.
- Made `TryCopyTo` return `true` for sufficient destinations and use the same
  overlap-safe copy direction as `CopyTo`.
- Added focused host-test markers for success, too-small no-write behavior, and
  overlapping span behavior.
- Updated the no-stdlib limits documentation for the new no-throw helper.

## Why It Changed

Kernel-facing and no-runtime-adjacent callers need a byte-span copy primitive
that can report destination capacity failures without relying on managed
exceptions. This step adds that status-coded helper while keeping the existing
BCL-shaped throwing helper available for compatibility tests.

## Files Changed

- `std/no-runtime/shared/MemoryExtensions.cs`
- `tests/OS.HostTests/StdMemoryExtensionsTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `done/step285.md`

## Tests Run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

The host-test run was filtered in the terminal for:

```text
std\.memoryextensions trycopyto|TEST_FAIL|host\.tests
```

## Logs And Evidence

The focused host-test project build passed:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

The focused marker run emitted:

```text
TEST_PASS std.memoryextensions trycopyto success
TEST_PASS std.memoryextensions trycopyto too-small
TEST_PASS std.memoryextensions trycopyto overlap
TEST_PASS host.tests
```

No `TEST_FAIL` lines were emitted by the filtered run.

## Failures

No failures were observed in this slice.

## Evidence Boundary

This is host-test evidence for the shared `OS.Std` byte-span helper only. It
does not prove kernel runtime helper completeness, EFI no-stdlib runtime
behavior, boot behavior, PAL behavior, app launch behavior, or QEMU behavior.

## Deferred Work

- Migrate early boot, parser, or panic-path callers to `TryCopyTo` only when a
  focused issue owns that call-site change.
- Keep the canonical `System.MemoryExtensions` surface deferred until the
  no-stdlib compatibility contract is complete and documented.
