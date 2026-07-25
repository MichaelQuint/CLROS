# Step 722 - Shared std helper migrations for TCP, DNS, and FAT32

## Summary

Closed three Phase C/F cleanup slices that removed local byte/ASCII helper logic
from kernel storage and networking code:

- TCP established stream receive/read copy paths now use
  `OS.Std.MemoryExtensions.TryCopyTo`.
- DNS hostname byte validation now uses `OS.Std.Ascii.IsLetterOrDigit`.
- FAT32 long-name case folding now uses `OS.Std.Ascii.ToUpperInvariant`.

## Why

The CloudReady invariant requires shared string, memory, numeric, collection,
and BCL-like behavior to live first in `std/no-runtime/shared`, then kernel and
app code should migrate to that shared implementation. These slices shrink
duplicated parser/driver helper code without changing the runtime behavior of
the host-tested storage and networking contracts.

## Files changed

- `src/OS.Kernel/Net/TcpEstablishedStream.cs`
- `tests/OS.HostTests/TcpEstablishedStreamTests.cs`
- `src/OS.Kernel/Net/DnsMessage.cs`
- `tests/OS.HostTests/DnsMessageTests.cs`
- `src/OS.Kernel/Storage/Fat32Parser.cs`
- `tests/OS.HostTests/StorageFat32ParserTests.cs`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- `artifacts\logs\issues671-673-host-build-20260525.log`
  - Build succeeded with `0 Warning(s)` and `0 Error(s)`.
- `artifacts\logs\issues671-673-hosttests-20260525.log`
  - Includes `TEST_PASS net.tcp stream uses shared memory copy helper`.
  - Includes `TEST_PASS net.dns message uses shared ascii hostname helper`.
  - Includes `TEST_PASS storage fat32 uses shared ascii long-name case helper`.
  - Ends with `TEST_PASS host.tests`.
- `artifacts\logs\issues671-673-git-diff-check-20260525.log`
  - `git diff --check` exited 0. Output contains only existing LF/CRLF
    working-copy warnings.

## Failures

- No failures in the integrated build or host test run.
- No real QEMU boot was run for this slice; these changes are host-tested
  std-helper migrations only.

## Deferred work

- Continue migrating duplicated ASCII classification in manifest and HTTP parser
  validation code to `OS.Std.Ascii`.
- Continue moving remaining byte copy/equality helpers to
  `OS.Std.MemoryExtensions` where the shared no-runtime contract is suitable.
