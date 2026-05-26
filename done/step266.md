# Step 266 - OS.Std ASCII Byte Helpers

## What Changed

- Added byte overloads to `OS.Std.Ascii` for ASCII digit, letter,
  letter-or-digit, whitespace, upper-case, and lower-case helpers.
- Added `Ascii.EqualsIgnoreCase(ReadOnlySpan<byte>, ReadOnlySpan<byte>)` for
  caller-owned protocol/config byte comparisons without string conversion.
- Added host tests for ASCII byte boundary cases, non-ASCII pass-through, and
  case-insensitive byte-span equality.
- Updated no-stdlib limits and completion-gate docs.

## Why It Changed

Issue #266 needed a small shared helper slice before more parsers add local
ASCII byte logic. Keeping this in `std/no-runtime/shared` prevents storage,
networking, manifest, and hosted-runtime package code from inventing divergent
ASCII behavior.

## Files Changed

- `std/no-runtime/shared/Ascii.cs`
- `tests/OS.HostTests/StdAsciiByteTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- `done/step266.md`

## Tests Run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll
dotnet build .\OS.slnx --nologo -m:1
.\scripts\check-naming.ps1
git diff --check
```

## Logs And Evidence

`dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1`
passed with:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

`dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll` passed and
the filtered evidence included:

```text
TEST_START std.ascii byte helpers
TEST_PASS std.ascii byte helpers
TEST_START std.ascii helpers
TEST_PASS std.ascii helpers
TEST_PASS host.tests
HOST_TEST_EXIT 0
```

`dotnet build .\OS.slnx --nologo -m:1` passed with:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

`.\scripts\check-naming.ps1` passed with:

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

`git diff --check` exited `0`. It printed only line-ending normalization
warnings for existing dirty files.

## Failures

No current verification failures for this helper slice.

## Evidence Boundary

This is host-tested shared helper work only:

- no complete BCL surface;
- no NativeAOT runtime helper implementation;
- no kernel parser migration;
- no guest boot or QEMU behavior;
- no Gate 4 completion claim.

## Deferred Work

- Migrate parser-local ASCII byte comparisons onto this helper where it reduces
  duplication.
- Continue the broader std/no-runtime BCL subset issue with arrays, strings,
  collections, and runtime-helper implementation evidence.
