# Step 726 - Shared ASCII and MAC Helper Migration

## Summary

Implemented Gitea issues #688 through #692 as a focused shared-helper migration
slice:

- #688: DHCP client MAC validation now uses `MacAddress.IsZero` and
  `MacAddress.IsBroadcast`.
- #689: TCP active, passive, established-stream, and close frame planners now
  use `MacAddress.IsZero` for zero-MAC validation.
- #690: `AsciiDecimalParser` now uses the shared `Ascii.IsDigit(byte)` helper.
- #691: `AsciiString.TrimTrailingSpace` was added and FAT32 short-name parsing
  now uses it for base and extension trimming.
- #692: `PanicTextBuffer` now uses `OS.Std.Ascii.IsWhiteSpace` for text-field
  whitespace detection.

## Why

This removes another set of parser-local and subsystem-local helper logic before
the future live storage, networking, and panic paths depend on these contracts.
The behavior remains host-tested only, but the shared utility surface is now the
single source for these ASCII and MAC identity checks.

## Files changed

- `src/OS.Kernel/Net/DhcpClientTransactionPlanner.cs`
- `tests/OS.HostTests/DhcpClientTransactionPlannerTests.cs`
- `src/OS.Kernel/Net/TcpIpv4FramePlanner.cs`
- `src/OS.Kernel/Net/TcpPassiveIpv4FramePlanner.cs`
- `src/OS.Kernel/Net/TcpEstablishedStreamIpv4FramePlanner.cs`
- `src/OS.Kernel/Net/TcpConnectionCloseIpv4FramePlanner.cs`
- `tests/OS.HostTests/TcpIpv4FramePlannerTests.cs`
- `std/no-runtime/shared/Text/AsciiDecimalParser.cs`
- `tests/OS.HostTests/StdAsciiDecimalParserTests.cs`
- `std/no-runtime/shared/Text/AsciiString.cs`
- `src/OS.Kernel/Storage/Fat32Parser.cs`
- `tests/OS.HostTests/StdAsciiStringTests.cs`
- `tests/OS.HostTests/StorageFat32ParserTests.cs`
- `src/OS.Kernel/Diagnostics/PanicTextBuffer.cs`
- `tests/OS.HostTests/PanicTextBufferTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/cstorage.md`
- `docs/diagnostics.md`
- `docs/networking.md`
- `done/step726.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- Build log: `artifacts\logs\issues688-692-host-build-20260525.log`
  - Result: `Build succeeded.`
  - Result: `0 Warning(s)`, `0 Error(s)`
- Host test log: `artifacts\logs\issues688-692-hosttests-20260525.log`
  - `TEST_PASS net.dhcp.client mac validation uses shared helpers`
  - `TEST_PASS net.tcp zero mac validation uses shared helper`
  - `TEST_PASS std.ascii.decimal uses shared digit helper`
  - `TEST_PASS std.ascii.string trims trailing space`
  - `TEST_PASS storage fat32 uses shared ascii trailing-space helper`
  - `TEST_PASS panic text buffer uses shared ascii whitespace helper`
  - `TEST_PASS host.tests`
- Whitespace log before docs/done update:
  `artifacts\logs\issues688-692-git-diff-check-20260525.log`
  - Exit code: 0
  - Output contained only existing LF-to-CRLF working-copy warnings.
- Whitespace log after docs/done update:
  `artifacts\logs\issues688-692-git-diff-check-postdone2-20260525.log`
  - Exit code: 0
  - Output contained only existing LF-to-CRLF working-copy warnings.

## Failures

- The first isolated worker attempts for #688 and #691 saw transient default
  Debug output DLL locks from existing `OS.HostTests` processes. The integrated
  build and full host-test run passed afterward.
- No QEMU boot, live NIC, live disk, or real panic/fault probe was run for this
  helper migration slice.

## Deferred work

- Live virtio-net/ENA validation still needs real PCI/BAR/MMIO/DMA ownership and
  packet I/O before these MAC helpers are proven against hardware.
- FAT32 short-name helper use remains host-tested over injected block devices,
  not a guest virtio-blk disk read.
- Panic text-buffer helper use remains host-tested and is not wired into a real
  IDT/fault-handler panic path.
- `std/no-runtime/shared` still needs the broader BCL-compatible surface,
  NativeAOT runtime helpers, exceptions, cctor execution, and GC work tracked in
  `docs/completion-gates.md`.

