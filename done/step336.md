# Step 336 - OS.Std Printable ASCII Byte Helpers

## Summary
Implemented Gitea issue #321 by adding no-runtime `OS.Std.Ascii` byte helpers for printable and visible ASCII ranges, adding boundary tests, and moving manifest/PAL duplicate byte checks to the shared helper.

## Why
Manifest and PAL validation already duplicated small ASCII byte range checks. Keeping the helpers under `std/no-runtime/shared` first makes later kernel and boot parser work reuse a NativeAOT/no-runtime-compatible utility surface instead of spreading local ad hoc checks.

## Files changed
- `std/no-runtime/shared/Ascii.cs`
- `tests/OS.HostTests/StdAsciiByteTests.cs`
- `src/OS.Kernel/Manifest/NativeManifestParser.cs`
- `src/OS.Kernel/Pal/OSHostConfigEnvironmentTable.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- `done/step336.md`

## Tests run
- `.\scripts\new-done-step.ps1 -Reserve`
- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence
- Solution build completed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS std.ascii byte helpers` and final `TEST_PASS host.tests`.
- `git diff --check` exited 0 with LF-to-CRLF warnings only.
- The shared helper now exposes `IsPrintable(byte)` for `0x20..0x7E` and `IsVisible(byte)` for `0x21..0x7E`, with boundary coverage for control, DEL, and high-bit bytes.

## Failures
- No #321 command failures.
- `git diff --check` reported only LF-to-CRLF warnings.

## Deferred work
- This does not implement BCL/runtime helpers, GC, exceptions, object allocation, or hosted .NET/Kestrel support.
- Broader parser migrations should be handled as separate small issues.

