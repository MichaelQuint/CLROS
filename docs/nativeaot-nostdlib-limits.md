# NativeAOT NoStdLib Limits

## Current Status

The no-stdlib surface is not implemented beyond the first EFI shell boundary. `std/no-runtime/shared` contains small host-tested helper slices for shared compatible contracts, but it is not a complete BCL or runtime surface.

## Implemented Behavior

- `OS.Std.CompatibilityStatus` records compatibility states.
- `OS.Std.Ascii` provides host-tested ASCII helpers for `char`, `string`,
  `byte`, and `ReadOnlySpan<byte>` inputs: `EqualsIgnoreCase`, `IsDigit`,
  `IsLetter`, `IsUpperLetter`, `IsLowerLetter`, `IsLetterOrDigit`,
  `IsPrintable`, `IsVisible`, `IsWhiteSpace`, `IsSpaceOrTab`,
  `ToUpperInvariant`, and `ToLowerInvariant`. The byte APIs are ASCII-only and
  leave non-ASCII bytes unchanged for casing.
- `OS.Std.Text.AsciiString` provides an OS-specific, allocation-free
  `ReadOnlySpan<byte>` helper slice for ASCII ordinal-ignore-case
  `Equals`, `EqualsIgnoreCase`, `StartsWithIgnoreCase`,
  `EndsWithIgnoreCase`, `IndexOfIgnoreCase`, `TrimSpaceOrTab`, and
  `TrimTrailingSpace`. Non-ASCII bytes are compared byte-for-byte and are not
  Unicode-folded. `TrimSpaceOrTab` trims only HTTP-style OWS bytes through
  `OS.Std.Ascii.IsSpaceOrTab`. `TrimTrailingSpace` trims only byte `0x20`; it
  intentionally preserves tabs and other ASCII whitespace for FAT short-name
  compatibility.
- `OS.Std.Text.AsciiTokenSplitter` provides a caller-owned ASCII token splitting
  helper for manifest/protocol-style byte spans. It validates source ranges and
  delimiters, preserves empty fields by default, optionally trims ASCII
  whitespace, optionally removes empty entries after trimming, reports required
  token capacity before writing, rejects non-ASCII/control input with the
  failing index, rejects unknown option bits, and returns status-coded results
  without allocating.
- `OS.Std.Text.AsciiKeyValueSplitter` provides a caller-owned ASCII key/value
  splitting helper over `ReadOnlySpan<byte>` inputs. It splits only on the
  first delimiter, returns source-backed `AsciiTokenRange` metadata for the key
  and value, preserves additional delimiter bytes inside the value, optionally
  trims ASCII whitespace with the existing `AsciiTokenSplitOptions` style, and
  rejects invalid ranges, invalid delimiters, invalid options, missing
  delimiters, empty keys, non-ASCII/control input, and default-result success
  without allocating.
- `OS.Std.Text.AsciiTextBuilder` provides a caller-owned ASCII byte formatting
  helper for diagnostics and protocol text. It appends checked ASCII bytes and
  chars plus decimal/hex integer values into caller-provided buffers, reports
  required byte counts, preserves overlap-safe byte copy through
  `OS.Std.Memory.BufferOperations`, and rejects non-ASCII/control input,
  undersized destinations, invalid offsets, and invalid hex digit widths with
  status-coded results.
- `OS.Std.Text.AsciiDecimalParser` provides allocation-free signed and unsigned
  decimal parsing for `ReadOnlySpan<byte>` ASCII input. It parses `int`, `long`,
  `uint`, and `ulong` values, rejects empty, sign-only, whitespace, non-digit,
  and overflow input with status-coded results, accepts a leading `-` only for
  signed methods, intentionally rejects `+`, reuses the shared `OS.Std.Ascii`
  digit classifier, and leaves default/zero results out of success evidence.
- `OS.Std.Text.AsciiHexParser` provides allocation-free unsigned hexadecimal
  parsing for `ReadOnlySpan<byte>` ASCII input. It parses `uint` and `ulong`
  values, accepts an optional `0x` or `0X` prefix, rejects empty, prefix-only,
  non-hex, and overflow input with status-coded results, and leaves
  default/zero results out of success evidence.
- `OS.Std.Text.AsciiDecimalFormatter` provides allocation-free signed and
  unsigned decimal formatting for `int`, `long`, `uint`, and `ulong` into caller-owned `Span<byte>`
  buffers. It exposes digit-count helpers for the same signed and unsigned
  value shapes, reports required byte counts, rejects undersized destinations
  without writing partial output, and leaves default/zero results out of
  success evidence. Kernel/PAL diagnostic formatters should use this shared
  helper before adding any local integer-to-ASCII loops; the PAL export-catalog
  validation marker is host-tested against this shared path.
- `OS.Std.MemoryExtensions` provides a partial byte-oriented span helper slice: `Contains`, `Count(byte)`, `EndsWith`, `IndexOf(byte)`, `IndexOf(ReadOnlySpan<byte>)`, `IndexOfAny(byte, byte)`, `IndexOfAny(byte, byte, byte)`, `LastIndexOf(byte)`, `LastIndexOf(ReadOnlySpan<byte>)`, `SequenceEqual`, `StartsWith`, BCL-shaped `CopyTo(ReadOnlySpan<byte>, Span<byte>)`, and no-throw `TryCopyTo(ReadOnlySpan<byte>, Span<byte>)`. It also provides a narrow char-span helper slice for `IndexOf(char)`, `LastIndexOf(char)`, `SequenceEqual(ReadOnlySpan<char>)`, `StartsWith(ReadOnlySpan<char>)`, `EndsWith(ReadOnlySpan<char>)`, BCL-shaped `CopyTo(ReadOnlySpan<char>, Span<char>)`, and no-throw `TryCopyTo(ReadOnlySpan<char>, Span<char>)`.
- `OS.Std.Memory.BufferOperations` provides a host-tested partial byte-copy
  helper with status-coded `MemoryCopy(ReadOnlySpan<byte>, Span<byte>)`. It
  preserves memmove-style overlap behavior and rejects short destinations
  before writing.
- `OS.Std.Binary.EndianPrimitives` provides allocation-free span-based `UInt16`, `UInt32`, and `UInt64` little-endian and big-endian read/write helpers plus non-throwing `TryRead`/`TryWrite` variants for parser-facing kernel code.
- `OS.Std.Hash.Crc32` provides an allocation-free reflected IEEE CRC32 helper using polynomial `0xEDB88320`, initial state `0xFFFFFFFF`, and final XOR `0xFFFFFFFF`. It supports one-shot `Compute`, incremental `Update`, and `Finish` over caller-provided `ReadOnlySpan<byte>` buffers without lazy static lookup tables.
- `OS.Std.Net.InternetChecksum` provides allocation-free 16-bit ones-complement internet checksum support for network-order packet buffers. It supports one-shot checksums, two-, three-, and four-segment checksum overloads, validity checks, and an `InternetChecksumAccumulator` that preserves odd pending bytes across segment boundaries without allocating.
- `OS.Std.NativeInt` provides a small pointer-sized unsigned arithmetic helper slice over `nuint`: checked add/subtract, power-of-two detection, alignment checks, and checked align-up/align-down results. It uses status-coded results so kernel/runtime planning paths can avoid exception-driven overflow handling.
- `OS.Std.Math` provides a host-tested partial integer helper slice for BCL-shaped `Min`, `Max`, `Clamp`, and `Abs` behavior over `int`, `uint`, `long`, and `ulong` where applicable. `Clamp` follows the BCL invalid-bound throwing behavior, and `Abs(int.MinValue)` / `Abs(long.MinValue)` throw overflow rather than wrapping.
- `OS.Std.ArrayHelpers` provides a host-tested partial unmanaged-span helper slice for `Clear`, `Fill`, `IndexOf`, `LastIndexOf`, BCL-shaped `BinarySearch` insertion-point results, full-span `Sort`, status-coded range-limited `TrySort`, and status-coded `TryBinarySearch` over caller-specified ranges. The current slice is no-throw, allocation-free, deterministic for small buffers, and constrained to unmanaged element types.
- `src/OS/Runtime/MinimalRuntime.cs` defines the current compiler-required primitive stubs, throw helpers, memory helpers, fail-fast type-cast placeholders, `IDynamicInterfaceCastable` metadata shape, and reverse-P/Invoke exports needed by the explicit EFI shell project.
- `BootPageFreeListBuilder`, `BootPageAllocatorStateInitializer`,
  `BootPageAllocatorMutation`, `BootPageAllocatorReadinessComposer`, and
  `NativeAotHeapBootstrapPlanner` are host-tested prerequisites for future
  runtime allocation work. They convert successful boot page free-segment
  planning into caller-owned `BootPageFreeListNode` metadata, value-type
  allocator state metadata, caller-owned free-list mutation evidence, aggregate
  boot readiness marker metadata, and future managed-heap page-budget metadata
  without allocating live pages, and their readiness results reject default/zero
  values as successful evidence. They do not implement NativeAOT object
  allocation helpers, GC ownership, or live heap mutation.
- `NativeAotClassConstructorStateTable` provides a host-tested,
  allocation-free class-constructor state table over caller-owned storage. It
  uses numeric module/type keys, explicit value-type states, and status-coded
  transitions for register, begin, mark-initialized, mark-failed, and readback.
  It detects reentry cycles, failed cctor reuse, duplicate keys, missing keys,
  capacity exhaustion, invalid keys, missing failure codes, and default-result
  success bugs without executing constructors, delegates, reflection, dynamic
  invocation, allocation, or exception-based control flow.
- `NativeAotClassConstructorStateDiagnosticFormatter` writes initialized
  `NativeAotClassConstructorStateTableResult` metadata into caller-owned ASCII
  byte buffers with the stable `NATIVEAOT_CCTOR_STATE status=` prefix. It
  reports required bytes, rejects short destinations before writing, and rejects
  default/uninitialized results without allocating or throwing for normal
  formatter failures. Its byte-count accounting uses the shared
  `OS.Std.Text.AsciiDecimalFormatter` digit-count helpers, and its caller-owned
  write path uses `OS.Std.Text.AsciiTextBuilder` appends instead of
  formatter-local decimal or byte-copy loops.
- `NativeAotClassConstructorRunnerPreflight` provides a host-tested,
  allocation-free preflight contract for the future class-constructor runner. It
  validates caller-owned state-table initialization, type counts, target
  metadata counts, missing target metadata, pending/reentrant `Initializing`
  states, failed constructor states, and invalid states without executing
  constructors, discovering metadata, invoking delegates, using reflection, or
  relying on exception control flow. Its diagnostic formatter emits the stable
  `NATIVEAOT_CCTOR_RUNNER_PREFLIGHT status=` marker into caller-owned ASCII
  buffers and reports short destinations before writing. Its byte-count
  accounting uses the shared `OS.Std.Text.AsciiDecimalFormatter` digit-count
  helpers, and its caller-owned write path uses
  `OS.Std.Text.AsciiTextBuilder` appends instead of formatter-local decimal or
  byte-copy loops. `BootSequence` can emit host-tested
  `NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_READY` readiness markers, and QEMU tooling
  exposes an opt-in `cctor-preflight` marker preset for future runtime-focused
  smoke images. These remain host-tested preflight/formatter/marker contracts
  only; they do not execute class constructors, implement managed exception
  handling, or complete NativeAOT runtime support.
- `NativeAotClassConstructorWorkItemPlanner` turns an already-successful
  preflight result plus caller-supplied target metadata into caller-owned
  `NativeAotClassConstructorWorkItem` records. The records carry only
  module/type ids, nonzero `nuint` target tokens, and source state-table slot
  indexes for future dispatcher use. The planner rejects default preflight
  results, failed/pending preflight states, missing target metadata, zero target
  tokens, and insufficient destination capacity without invoking delegates,
  discovering metadata, executing constructors, or using exception control flow.
  `NativeAotClassConstructorWorkItemPlanDiagnosticFormatter` writes the stable
  `NATIVEAOT_CCTOR_WORK_ITEMS status=` marker into caller-owned ASCII byte
  buffers, reports required byte counts, rejects default/uninitialized plan
  results, and rejects undersized destinations before writing. Its byte-count
  accounting uses the shared `OS.Std.Text.AsciiDecimalFormatter` digit-count
  helpers, and its caller-owned write path uses
  `OS.Std.Text.AsciiTextBuilder` appends instead of formatter-local decimal or
  byte-copy loops.
  `BootSequence.RunNativeAotClassConstructorWorkItemReadiness` exposes the same
  already-produced plan metadata through host-tested
  `NATIVEAOT_CCTOR_WORK_ITEMS_READY` / `NATIVEAOT_CCTOR_WORK_ITEMS_NOT_READY`
  markers without wiring default boot, QEMU presets, constructor execution,
  delegates, reflection, exception control flow, or metadata discovery.

## EFI-Only Shim Boundary

The canonical no-stdlib `System.*` stubs are confined to `src/OS/Runtime/MinimalRuntime.cs` and are compiled only by the explicit EFI project `src/OS/OS.csproj`.

Boundary rules:

- `src/OS/OS.csproj` has `NoStdLib=true`, `DisableImplicitFrameworkReferences=true`, `EnableDefaultCompileItems=false`, and explicit `Compile` includes.
- Normal host-testable projects such as `src/OS.Kernel` and `std/no-runtime/shared` must not compile `Runtime/MinimalRuntime.cs`.
- Host-safe shared helpers live under `OS.Std.*` first, not under canonical `System.*`, until compatibility is complete and documented.
- The EFI shim can contain compiler-required placeholder exports, but placeholders must fail fast or provide real behavior. They must not silently return success for unsupported runtime features.

## Runtime Helper Ledger

`src/OS.Kernel/Runtime/NativeAotHelperLedger.cs` is the host-testable ledger for NativeAOT helper status. It records helper symbol or contract detail, evidence, required contract, allowed milestone stub behavior, current test coverage, owner subsystem, next step, and completion-gate applicability only; it does not implement helpers and does not make EFI-only stubs count toward kernel/runtime completion.

| Helper area | Current status | Evidence / boundary |
| --- | --- | --- |
| Primitive/CoreLib type stubs | Implemented only for EFI compile boundary | `src/OS/OS.csproj` explicitly includes `Runtime/MinimalRuntime.cs`; host boundary tests verify it is not compiled by `src/OS.Kernel` or `std/no-runtime/shared` |
| `System.Runtime.RuntimeExportAttribute` | Implemented only for EFI runtime export metadata | Needed by `EfiMain` and NativeAOT helper exports |
| `System.Buffer.MemoryCopy` / `Memmove` | Implemented as byte loops for EFI shell needs | Used for no-stdlib memory copy paths; not a complete BCL buffer contract |
| `System.SpanHelpers` clear/fill/memmove slice | Implemented as narrow byte/pointer helper slice | Supports current EFI publish boundary only |
| Object allocation helpers | Deferred | Ledger entry `ObjectAllocation`; no OS-owned NativeAOT object allocation helper or managed heap handoff exists. `BootPageFreeListBuilder`, `BootPageAllocatorStateInitializer`, `BootPageAllocatorMutation`, `BootPageAllocatorReadinessComposer`, and `NativeAotHeapBootstrapPlanner` only prove prerequisite caller-owned free-list node, allocator state, caller-owned mutation, aggregate readiness-marker, and heap page-budget metadata |
| Array allocation helpers | Deferred | Ledger entry `ArrayAllocation`; no OS-owned NativeAOT array allocation helper exists |
| String allocation helpers | Deferred | Ledger entry `StringAllocation`; no OS-owned NativeAOT string allocation helper exists |
| Write barriers | Deferred | Ledger entry `WriteBarriers`; no kernel/runtime write barrier implementation exists |
| Interface dispatch | Deferred | Ledger entry `InterfaceDispatch`; EFI type-cast helpers fail fast and do not prove interface dispatch |
| Class constructors | Deferred | Ledger entry `ClassConstructors`; `NativeAotClassConstructorStateTable` proves caller-owned state transitions, cycle detection, duplicate/missing/capacity errors, failed-cctor preservation, and caller-owned `NATIVEAOT_CCTOR_STATE` diagnostic formatting. `NativeAotClassConstructorRunnerPreflight` proves ready/default/failed/pending/missing-metadata preflight status, caller-owned `NATIVEAOT_CCTOR_RUNNER_PREFLIGHT` diagnostic formatting, and host-tested `BootSequence` readiness markers only. `NativeAotClassConstructorWorkItemPlanner` now proves caller-owned work-item metadata planning from already-supplied target tokens plus caller-owned `NATIVEAOT_CCTOR_WORK_ITEMS` diagnostic formatting and host-tested `NATIVEAOT_CCTOR_WORK_ITEMS_READY` / `NATIVEAOT_CCTOR_WORK_ITEMS_NOT_READY` BootSequence readiness markers while preserving no-delegate/no-reflection/no-execution boundaries. No cctor runner invocation, constructor execution, metadata discovery, or constructor-body failure diagnostics is implemented |
| Thread statics | Deferred | Ledger entry `ThreadStatics`; no TLS bootstrap or NativeAOT thread-static base helper exists |
| Exception handling/unwind | Deferred | Ledger entry `ExceptionUnwind`; throw helpers halt and catch/finally/unwind are not implemented |
| Module initialization | Deferred | Ledger entry `ModuleInitialization`; no kernel/runtime module initialization helper behavior is implemented |
| TypeManager/RTR scans | Deferred | Ledger entry `TypeManagerRtrScans`; hosted runtime metadata scanning is not implemented |
| GC stack roots | Deferred | Ledger entry `GcStackRoots`; stack-root registration/scanning helpers are not implemented |
| EFI reverse P/Invoke helpers | EFI-only link stubs | Ledger entry `EfiReversePInvoke`; `RhpReversePInvoke`, `RhpReversePInvokeReturn`, `RhpPInvoke`, and `RhpPInvokeReturn` exist only in the EFI shim and do not satisfy Gate 4 |
| EFI type-cast helpers | EFI-only fail-fast stubs | Ledger entry `EfiFailFastTypeCasts`; `RhpFallbackFailFast` and `RhTypeCast_*` helpers halt and do not satisfy kernel/runtime completion |
| EFI stack probe | EFI-only audited bridge/byte plan | Ledger entry `EfiStackProbe`; M01-021 source/readback tests and emitted-object audits cover the fail-closed `RhpStackProbe` bridge, exact 27-byte page-probe plan, and generated pre-transfer frame budget. This is source/build evidence only: no guest execution or kernel/runtime completion is claimed |

## Unsupported Behavior

The required BCL subset, real NativeAOT helpers, GC, class constructor runner, exceptions, thread statics, TypeManager/RTR scans, hosted .NET 10 runtime boundary, ASP.NET Minimal API hosting, and Kestrel prerequisites are not implemented.

## Tests

`tests/OS.HostTests/StdAsciiTests.cs` covers ASCII character classification,
casing, upper/lower letter helpers, space-or-tab classification, and non-ASCII
pass-through behavior for the current helper slice. It emits
`TEST_PASS std.ascii upper lower letter helpers` and
`TEST_PASS std.ascii space-or-tab helper` for the focused helper additions.
`tests/OS.HostTests/StdAsciiByteTests.cs` covers byte classification, including
printable `0x20..0x7E` and visible `0x21..0x7E` boundaries, casing, and
case-insensitive span equality without converting protocol bytes to strings.
`tests/OS.HostTests/StdAsciiStringTests.cs` covers ASCII ordinal-ignore-case
prefix, suffix, and search behavior over `ReadOnlySpan<byte>` inputs, including
empty needles, too-long needles, repeated matches, and non-ASCII byte
pass-through. It also covers equality helpers, `TrimSpaceOrTab` for HTTP OWS
semantics, source-guarded use of `OS.Std.Ascii.IsSpaceOrTab`, and
`TrimTrailingSpace` for empty, all-space, no-trim, partial-trim, and
tab-preservation cases. It emits `TEST_PASS std.ascii.string startswith
endswith ignorecase`, `TEST_PASS std.ascii.string equality helpers`,
`TEST_PASS std.ascii.string indexof ignorecase`, `TEST_PASS std.ascii.string
trims space and tab`, `TEST_PASS std.ascii.string trim uses shared
space-or-tab helper`, `TEST_PASS std.ascii.string trims trailing space`, and
`TEST_PASS std.ascii.string compatibility boundary`.

`tests/OS.HostTests/StdAsciiTokenSplitterTests.cs` covers caller-owned token
splitting over ASCII byte spans, empty-field preservation, ASCII whitespace
trimming, empty-entry removal, combined trim/remove-empty behavior, all-empty
zero-token success, destination-capacity reporting without partial writes,
invalid-range rejection, non-ASCII/control input rejection, invalid-delimiter
and invalid-option rejection, and default-result rejection. It emits
`TEST_PASS std.ascii tokens splits fields`, `TEST_PASS std.ascii tokens
preserves empty fields`, `TEST_PASS std.ascii tokens trims fields`,
`TEST_PASS std.ascii tokens removes empty fields`, `TEST_PASS std.ascii tokens
trims then removes empty fields`, `TEST_PASS std.ascii tokens removes all empty
fields`, `TEST_PASS std.ascii tokens reports destination capacity`,
`TEST_PASS std.ascii tokens rejects invalid ranges`, `TEST_PASS std.ascii
tokens rejects non ascii input`, and `TEST_PASS std.ascii tokens compatibility
boundary`.

`tests/OS.HostTests/StdAsciiKeyValueSplitterTests.cs` covers caller-owned
key/value splitting over ASCII byte spans, first-delimiter-only splitting,
additional delimiter preservation inside the value, range-scoped source-backed
metadata, ASCII whitespace trimming, empty-value success, empty-key rejection,
missing-delimiter rejection, invalid-range rejection, invalid-delimiter and
invalid-option rejection, non-ASCII/control input rejection, default-result
rejection, and no-managed-reference result layout. It emits `TEST_PASS
std.ascii keyvalue splits first delimiter`, `TEST_PASS std.ascii keyvalue
preserves delimiter in value`, `TEST_PASS std.ascii keyvalue trims key and
value`, `TEST_PASS std.ascii keyvalue allows empty value`, `TEST_PASS std.ascii
keyvalue rejects empty key`, `TEST_PASS std.ascii keyvalue rejects missing
delimiter`, `TEST_PASS std.ascii keyvalue rejects invalid ranges`, `TEST_PASS
std.ascii keyvalue rejects invalid delimiter and options`, `TEST_PASS std.ascii
keyvalue rejects non ascii input`, and `TEST_PASS std.ascii keyvalue
compatibility boundary`.

`OS.Manifest.NativeManifestParser` now uses `AsciiKeyValueSplitter` for
kernel-facing manifest line parsing. Host tests verify that manifest values keep
additional `=` bytes inside source-backed values and that the parser source
depends on the shared splitter instead of the previous local token-split path.
This preserves the existing manifest format and error status behavior while
moving the common first-delimiter parsing primitive into `std/no-runtime/shared`.

`tests/OS.HostTests/StdAsciiTextBuilderTests.cs` covers caller-owned ASCII text
builder appends for ASCII byte spans, single bytes, ASCII chars, CR/LF, decimal
`ulong`/`long`, padded prefixed hex, overlap-safe byte appends through the shared
buffer operation helper, too-small destinations without writes, non-ASCII and
NUL rejection, invalid offsets, invalid hex digit widths, default/zero result
rejection, and no-managed-reference result layout. It emits `TEST_PASS
std.ascii.builder appends bytes and chars`, `TEST_PASS std.ascii.builder
appends decimal and hex`, `TEST_PASS std.ascii.builder rejects capacity and
input`, and `TEST_PASS std.ascii.builder default result is not success`.
`tests/OS.HostTests/PanicTextBufferTests.cs` emits `TEST_PASS panic text buffer
uses shared ascii text builder` to guard the panic-path numeric formatter
against reintroducing diagnostics-local decimal or hex loops.
`tests/OS.HostTests/NativeAppExitStatusTests.cs` emits `TEST_PASS
appmodel.exit status formatter uses shared ascii text builder` to guard the
native app `APP_EXIT` marker formatter against reintroducing app-model-local
decimal counting or writing loops.
`tests/OS.HostTests/NativeAppExitLogObserverTests.cs` emits `TEST_PASS
appmodel.exit observer formatter uses shared ascii text builder` to guard the
native app exit observer diagnostic formatter against reintroducing
app-model-local decimal counting or writing loops.
`tests/OS.HostTests/NativeManifestParserTests.cs` emits `TEST_PASS manifest env
formatter uses shared ascii text builder` to guard the manifest environment
diagnostic formatter against reintroducing manifest-local decimal counting or
writing loops.
`tests/OS.HostTests/NativeAppPackageStagingFormatterTests.cs` emits `TEST_PASS
appmodel package staging formatter uses shared ascii text builder` to guard the
package-staging readiness diagnostic formatter against reintroducing
app-model-local decimal counting or writing loops.
`tests/OS.HostTests/NativeAppArgumentVectorPlanTests.cs` emits `TEST_PASS
appmodel args vector formatter uses shared ascii text builder` to guard the
argument-vector diagnostic formatter against reintroducing app-model-local
decimal counting or writing loops.

`tests/OS.HostTests/StdAsciiDecimalParserTests.cs` covers unsigned ASCII
decimal parsing for `uint` and `ulong`, signed ASCII decimal parsing for `int`
and `long`, including zero, leading zeros, minimum and maximum signed values,
maximum unsigned values, empty input, sign-only rejection, `+` rejection,
whitespace/non-digit rejection, overflow detection, `TryParse` failure value
clearing, use of the shared `Ascii.IsDigit` classifier, default/zero result rejection, and no-managed-reference result
layout. It emits `TEST_PASS std.ascii.decimal parses uint32 values`,
`TEST_PASS std.ascii.decimal parses uint64 values`, `TEST_PASS
std.ascii.decimal parses int32 values`, `TEST_PASS std.ascii.decimal parses
int64 values`, `TEST_PASS std.ascii.decimal rejects invalid input`, `TEST_PASS
std.ascii.decimal rejects signed invalid input`, `TEST_PASS std.ascii.decimal
uses shared digit helper`, and `TEST_PASS std.ascii.decimal default result is not
success`.

`tests/OS.HostTests/StdAsciiHexParserTests.cs` covers unsigned ASCII
hexadecimal parsing for `uint` and `ulong`, including zero, leading zeros,
mixed uppercase/lowercase digits, maximum values, optional `0x` and `0X`
prefixes, empty input, prefix-only rejection, sign/whitespace/non-hex
rejection, overflow detection, `TryParse` failure value clearing,
default/zero result rejection, and no-managed-reference result layout. It emits
`TEST_PASS std.ascii.hex parses uint32 values`, `TEST_PASS std.ascii.hex parses
uint64 values`, `TEST_PASS std.ascii.hex accepts optional 0x prefix`,
`TEST_PASS std.ascii.hex rejects invalid input`, `TEST_PASS std.ascii.hex
rejects overflow`, `TEST_PASS std.ascii.hex default result is not success`, and
`TEST_PASS std.ascii.hex layouts contain no managed references`.

`tests/OS.HostTests/StdAsciiDecimalFormatterTests.cs` covers ASCII decimal
formatting for `int`, `long`, `uint`, and `ulong`, including zero, maximum
values, signed minimum values, larger destinations, short destinations without
writes, required byte count reporting, public digit-count helpers,
default/zero result rejection, and no-managed-reference result layout. It emits `TEST_PASS
std.ascii.decimal.formatter formats uint32 values`, `TEST_PASS
std.ascii.decimal.formatter formats uint64 values`, `TEST_PASS
std.ascii.decimal.formatter formats int32 values`, `TEST_PASS
std.ascii.decimal.formatter formats int64 values`, `TEST_PASS
std.ascii.decimal.formatter reports capacity`, `TEST_PASS
std.ascii.decimal.formatter reports signed capacity`, `TEST_PASS
std.ascii.decimal.formatter reports required byte counts`, and `TEST_PASS
std.ascii.decimal.formatter default result is not success`.
`tests/OS.HostTests/PalExportCatalogTests.cs` additionally emits
`TEST_PASS pal.export catalog formatter uses shared decimal formatter` to guard
the PAL export-catalog marker against reintroducing PAL-local decimal loops.

`tests/OS.HostTests/StdMemoryExtensionsTests.cs` covers byte-span sequence equality, prefix/suffix checks, byte search, byte counting, any-of byte search, span search, last byte search, last span search, empty inputs, repeated patterns, exact and larger-destination copy behavior, overlap-safe copy behavior, too-small destination rejection, no-throw `TryCopyTo` success, no-write too-small failure, overlap-safe `TryCopyTo` behavior, and compatibility status. It also covers char-span `IndexOf`, `LastIndexOf`, sequence equality, empty prefix/suffix success, prefix/suffix mismatch and too-long behavior, exact/larger copy behavior, overlap-safe `CopyTo` and `TryCopyTo`, too-small `CopyTo` rejection, no-write `TryCopyTo` failure, and empty-source copy preservation. It emits the focused markers `TEST_PASS std.memoryextensions byte span startswith endswith`, `TEST_PASS std.memoryextensions count byte values`, `TEST_PASS std.memoryextensions indexofany byte values`, `TEST_PASS std.memoryextensions trycopyto success`, `TEST_PASS std.memoryextensions trycopyto too-small`, `TEST_PASS std.memoryextensions trycopyto overlap`, `TEST_PASS std.memoryextensions char search`, `TEST_PASS std.memoryextensions char sequence prefix suffix`, `TEST_PASS std.memoryextensions char copyto`, and `TEST_PASS std.memoryextensions char trycopyto`.

`tests/OS.HostTests/StdBufferOperationsTests.cs` covers exact byte copy, larger
destination copy, overlap-forward and overlap-backward memmove behavior,
too-small destination rejection without partial writes, zero-length copy,
default/zero result rejection, and no-managed-reference result layout. It emits
`TEST_PASS std.buffer memorycopy exact bytes`, `TEST_PASS std.buffer memorycopy
overlap forward`, `TEST_PASS std.buffer memorycopy overlap backward`,
`TEST_PASS std.buffer memorycopy rejects short destination`, and `TEST_PASS
std.buffer memorycopy zero length and layout`.

`tests/OS.HostTests/StdEndianPrimitivesTests.cs` covers exact little-endian and big-endian byte order, too-short `TryRead`/`TryWrite` buffers, no-write failure behavior, and round trips for `UInt16`, `UInt32`, and `UInt64`.

`tests/OS.HostTests/StdCrc32Tests.cs` covers the empty-input CRC32 value, the standard ASCII `123456789` vector (`0xCBF43926`), a binary byte-range vector (`0x456CD746`), and incremental update/finalization over a split binary payload (`0x7C9CA35A`). It emits `std.crc32` markers for each scenario.

`tests/OS.HostTests/StdInternetChecksumTests.cs` covers the empty input result (`0xFFFF`), the RFC 1071 sample vector (`0x220D`), odd trailing byte padding, two- and four-segment odd-byte boundary carryover, accumulator pending-byte behavior, packet validation after checksum insertion, and accumulator reset behavior. It emits `std.internet.checksum` markers for each scenario.

`tests/OS.HostTests/StdNativeIntTests.cs` covers checked pointer-sized add/subtract, overflow/underflow reporting, power-of-two validation, align-up/align-down behavior, invalid alignment rejection, align-up overflow reporting, default/zero result rejection as success evidence, and no-managed-reference result layout. It emits `std.nativeint` markers for the focused scenarios.

`tests/OS.HostTests/StdMathTests.cs` covers the current partial integer math helper slice: signed and unsigned `Min`/`Max`, lower/inside/upper `Clamp` behavior, invalid clamp-bound rejection, `Abs` for positive/zero/negative values, and overflow rejection for `int.MinValue` and `long.MinValue`. It emits `TEST_PASS std.math min max integer helpers`, `TEST_PASS std.math clamp integer bounds`, and `TEST_PASS std.math abs overflow behavior`.

`tests/OS.HostTests/StdArrayHelpersTests.cs` covers the current partial unmanaged-span array helper slice: empty-span clear/fill behavior, full-span clear, partial-span fill, first/last/missing search over duplicate values, BCL-shaped binary-search exact matches, insertion-point complements, duplicate-match tolerance, full-span sort for empty, single-element, already-sorted, reverse, and duplicate values, status-coded range-limited `TrySort` success/invalid-range behavior, status-coded range-limited `TryBinarySearch` found/not-found/invalid-range behavior, and default-result rejection. It emits `TEST_PASS std.arrayhelpers clear fill unmanaged spans`, `TEST_PASS std.arrayhelpers indexof unmanaged spans`, `TEST_PASS std.arrayhelpers binarysearch insertion points`, `TEST_PASS std.arrayhelpers sort unmanaged spans`, `TEST_PASS std.arrayhelpers trysort range and status`, `TEST_PASS std.arrayhelpers trysort default result is not success`, `TEST_PASS std.arrayhelpers trybinarysearch found range`, `TEST_PASS std.arrayhelpers trybinarysearch notfound range`, `TEST_PASS std.arrayhelpers trybinarysearch invalid range`, and `TEST_PASS std.arrayhelpers trybinarysearch default result is not success`.

`tests/OS.HostTests/NativeAotHelperLedgerTests.cs` verifies that the helper ledger contains the required runtime helper areas, carries symbol/contract detail, evidence, required contract, allowed milestone stub behavior, current test coverage, owner subsystem, next-step text, and completion-gate applicability, leaves incomplete runtime helpers unmarked as complete, prevents EFI-only helpers from satisfying kernel/runtime completion, represents all 17 current EFI `[RuntimeExport]` symbols from `src/OS/Runtime/MinimalRuntime.cs` in exactly one ledger entry, keeps `RhpStackProbe` separate from the four reverse-P/Invoke and twelve type-cast symbols, preserves existing append-only enum values, and keeps `BootPageAllocatorMutation` evidence below the object/array/string allocation helper completion boundary. It emits `TEST_PASS runtime.nativeaot helper ledger structured columns`, `TEST_PASS runtime.nativeaot helper ledger append-only values`, and `TEST_PASS runtime.nativeaot helper ledger allocator mutation boundary`.

`tests/OS.HostTests/BootPageFreeListPlanTests.cs` covers the caller-owned boot page free-list node construction prerequisite for future allocation work. It emits `TEST_PASS memory.boot freelist nodes constructs`, `TEST_PASS memory.boot freelist nodes rejects failed planner`, `TEST_PASS memory.boot freelist nodes reports destination capacity`, `TEST_PASS memory.boot freelist nodes rejects invalid segment shape`, and `TEST_PASS memory.boot freelist nodes rejects arithmetic overflow`.

`tests/OS.HostTests/BootPageAllocatorStateTests.cs` covers the host-tested allocator state metadata prerequisite after free-list node construction. It emits `TEST_PASS memory.boot allocator state initializes`, `TEST_PASS memory.boot allocator state rejects failed free-list plan`, `TEST_PASS memory.boot allocator state rejects empty free-list`, `TEST_PASS memory.boot allocator state rejects source capacity`, `TEST_PASS memory.boot allocator state rejects corrupted node shape`, `TEST_PASS memory.boot allocator state rejects invalid chain`, `TEST_PASS memory.boot allocator state default result is not success`, and `TEST_PASS memory.boot allocator state layouts contain no managed references`.

`tests/OS.HostTests/BootPageAllocatorMutationTests.cs` covers caller-owned
free-list mutation after allocator-state initialization. It proves allocation,
aligned splitting, free/coalescing, free-into-empty-list behavior, invalid
mutation rejection, default-result rejection, no-managed-reference result
layout, and the explicit boundary constants that keep the slice below live
allocation, paging, and NX/W^X enforcement. It emits `TEST_PASS memory.boot
allocator mutates allocation`, `TEST_PASS memory.boot allocator splits aligned
range`, `TEST_PASS memory.boot allocator frees and coalesces`, `TEST_PASS
memory.boot allocator frees empty list`, `TEST_PASS memory.boot allocator
rejects invalid mutation`, `TEST_PASS memory.boot allocator mutation default
result is not success`, and `TEST_PASS memory.boot allocator mutation layouts
contain no managed references`.

`tests/OS.HostTests/BootPageAllocatorReadinessComposerTests.cs` covers the host-tested aggregate boundary from compact boot memory-region records through allocator-state initialization and `BootSequence.RunBootPageAllocatorReadiness`. It emits `TEST_PASS memory.boot allocator readiness composes`, `TEST_PASS memory.boot allocator readiness reports scratch capacity`, `TEST_PASS memory.boot allocator readiness rejects invalid regions`, and `TEST_PASS memory.boot allocator readiness emits boot marker metadata`.

`tests/OS.HostTests/NativeAotHeapBootstrapPlanTests.cs` covers the host-tested heap-bootstrap planning boundary after allocator-state initialization. It proves a requested future heap page budget, object alignment, remaining free-page metadata, allocator byte-count consistency, no-managed-reference result layout, default-result rejection, and the `BootSequence.RunNativeAotHeapBootstrapReadiness` marker boundary while keeping `SatisfiesObjectAllocationHelpers` false. It emits `TEST_PASS runtime.nativeaot heap bootstrap plans page budget`, `TEST_PASS runtime.nativeaot heap bootstrap rejects allocator state`, `TEST_PASS runtime.nativeaot heap bootstrap rejects allocator byte metadata`, `TEST_PASS runtime.nativeaot heap bootstrap rejects page budget`, `TEST_PASS runtime.nativeaot heap bootstrap rejects object alignment`, `TEST_PASS runtime.nativeaot heap bootstrap layout contains no managed references`, `TEST_PASS runtime.nativeaot heap bootstrap default result is not success`, `TEST_PASS boot.sequence nativeaot heap bootstrap ready`, `TEST_PASS boot.sequence nativeaot heap bootstrap rejects failed plan`, `TEST_PASS boot.sequence nativeaot heap bootstrap blocks default result`, and `TEST_PASS boot.sequence nativeaot heap bootstrap preserves default markers`.

`tests/OS.HostTests/NativeAotClassConstructorStateTableTests.cs` covers the
host-tested class-constructor state-table prerequisite. It proves
allocation-free registration and state transitions over caller-owned storage,
numeric module/type key lookup, reentry-cycle detection, failed-cctor
preservation, duplicate/missing/capacity rejection, default-result rejection,
and no-managed-reference value layouts. It emits `TEST_PASS runtime.cctor state
begins and completes`, `TEST_PASS runtime.cctor state detects reentry cycle`,
`TEST_PASS runtime.cctor state preserves failure`, `TEST_PASS runtime.cctor
state rejects duplicate keys`, `TEST_PASS runtime.cctor state default result is
not success`, and `TEST_PASS runtime.cctor state layouts contain no managed
references`. The same test file also covers caller-owned diagnostic formatting
for initialized state-table results. It emits `TEST_PASS runtime.cctor state
formatter formats success marker`, `TEST_PASS runtime.cctor state formatter
reports short destination`, and `TEST_PASS runtime.cctor state formatter
rejects default result`. It also emits `TEST_PASS runtime.cctor state formatter
uses shared ascii text builder` to guard the `NATIVEAOT_CCTOR_STATE` formatter
against reintroducing formatter-local decimal counting, direct digit writes, or
direct byte-copy loops.

The same file covers the future class-constructor runner preflight contract. It
proves ready metadata, default/uninitialized rejection, failed constructor state
rejection, pending/reentrant state rejection, missing and mismatched target
metadata rejection, caller-owned diagnostic formatting, short-destination
reporting without partial writes, default-result rejection, and primitive-only
result layouts. It emits `TEST_PASS runtime.cctor runner preflight ready`,
`TEST_PASS runtime.cctor runner preflight rejects default table`, `TEST_PASS
runtime.cctor runner preflight rejects failed state`, `TEST_PASS runtime.cctor
runner preflight rejects pending state`, `TEST_PASS runtime.cctor runner
preflight rejects missing metadata`, and `TEST_PASS runtime.cctor runner
preflight formatter reports short destination`. It also emits `TEST_PASS
runtime.cctor runner preflight formatter uses shared ascii text builder` to
guard the preflight formatter against reintroducing formatter-local decimal
counting, direct digit writes, or direct byte-copy loops. It also covers the
managed boot-readiness marker wrapper and emits `TEST_PASS boot.sequence cctor
runner preflight ready`, `TEST_PASS boot.sequence cctor runner preflight
rejects failed result`, `TEST_PASS boot.sequence cctor runner preflight rejects
default result`, and `TEST_PASS boot.sequence cctor runner preflight preserves
default markers`.

`tests/OS.HostTests/NativeAotClassConstructorWorkItemPlanTests.cs` covers the
next host-tested class-constructor runner prerequisite after preflight. It
proves caller-owned work-item planning for uninitialized entries, initialized
entry skipping, missing target-token rejection, missing metadata rejection,
destination-capacity reporting, failed/pending preflight rejection,
default-preflight rejection, caller-owned `NATIVEAOT_CCTOR_WORK_ITEMS`
diagnostic formatting, short-destination preservation, default-result
diagnostic rejection, and no-managed-reference layouts. It emits
`TEST_PASS runtime.cctor work item plans uninitialized entries`, `TEST_PASS
runtime.cctor work item skips initialized entries`, `TEST_PASS runtime.cctor
work item rejects missing target token`, `TEST_PASS runtime.cctor work item
rejects missing target metadata`, `TEST_PASS runtime.cctor work item reports
destination capacity`, `TEST_PASS runtime.cctor work item rejects failed
preflight`, `TEST_PASS runtime.cctor work item rejects pending preflight`,
`TEST_PASS runtime.cctor work item rejects default preflight`, `TEST_PASS
runtime.cctor work item formatter formats success marker`, `TEST_PASS
runtime.cctor work item formatter formats failure marker`, `TEST_PASS
runtime.cctor work item formatter reports short destination`, `TEST_PASS
runtime.cctor work item formatter rejects default result`, `TEST_PASS
runtime.cctor work item formatter uses shared ascii text builder`, `TEST_PASS
boot.sequence cctor work item ready`, `TEST_PASS boot.sequence cctor work item
rejects failed result`, `TEST_PASS boot.sequence cctor work item rejects default
result`, `TEST_PASS boot.sequence cctor work item preserves default markers`,
and `TEST_PASS runtime.cctor work item layouts contain no managed references`.

`tests/OS.HostTests/NativeAppPayloadMappingPlanTests.cs` covers the current host-tested native app payload range planner. It proves that a selected `OSAPPv0` payload descriptor can be reduced to deterministic executable-content and entrypoint offsets without allocating executable memory or invoking app code.

`tests/OS.HostTests/NativeAppExecutableMemoryPlanTests.cs` covers the current host-tested native app executable-memory permission planner. It proves that a payload range plan can be reduced to page-aligned write-then-execute mapping requirements without allocating pages, changing live protections, or invoking app code.

`tests/OS.HostTests/NativeAppInvocationPlanTests.cs` covers the current host-tested native app invocation planning contract. It proves that executable-memory planning and launch metadata can be reduced to deterministic stack, entry-offset, and bounded manifest metadata requirements without allocating stacks, switching stacks, creating threads, mapping pages, or invoking app code.

## Known Limitations

M01-034 adds the minimum no-stdlib binding metadata needed to retain and name
one host-generated native helper in the EFI image:

- `System.Runtime.RuntimeImportAttribute` stores only the module and entry-point
  metadata consumed by NativeAOT;
- `System.Runtime.CompilerServices.MethodImplAttribute` and the exact
  `InternalCall` option provide only the method-implementation metadata needed
  by this binding; and
- `OS.Boot.X64InstructionPublication.Serialize()` is an `extern` binding to
  `CLROS_X64_SerializeInstructionPublication` and has no managed fallback.

These shims are deliberately narrower than the public BCL types. They do not
implement reflection behavior, metadata discovery APIs, runtime invocation,
exception behavior, or general internal-call resolution. The host integration
test decodes the freshly built managed assembly and requires the exact
no-body `RuntimeImport`/`InternalCall` metadata. The canonical EFI artifact
verifier independently binds that managed-assembly hash to the same build and
proves that the final PE contains the expected nonwritable executable bytes
plus matching nonwritable unwind coverage. M01-034 does not call the binding,
prove its success cookie in a guest, implement NativeAOT runtime helpers, or
advance a runtime or boot completion gate.

Most solution projects reference normal `net10.0` libraries to keep host
contracts buildable. `src/OS/OS.csproj` is included in `CLROS.slnx` as explicit
no-stdlib work under stricter directory-local rules; it produces a minimal EFI
PE artifact, but that artifact is not yet a boot-proven kernel. Canonical
`System.*` replacement types must not be imported into the normal host build
path.

The current `OS.Std.MemoryExtensions` type intentionally lives under `OS.Std`, not `System`, because only partial byte-span and char-span subsets are implemented. Do not expose it as canonical `System.MemoryExtensions` until the signatures and behavior are compatible with the public BCL contract and documented as such. The `CopyTo` helper follows the BCL-shaped throwing contract in the host-testable subset; kernel-facing callers that cannot depend on exceptions should use the OS-specific `TryCopyTo` helper so too-small destinations are reported as `false` without modifying the destination.

The current `OS.Std.Text.AsciiString` type intentionally lives under
`OS.Std.Text`, not `System.String`, because it is an OS-specific byte-span
helper for protocol-style ASCII text. It does not allocate strings, does not
perform culture-sensitive comparison, does not Unicode-fold non-ASCII bytes,
and does not implement substring, split, join, concat, collation, or
normalization behavior.

The current `OS.Std.Text.AsciiTokenSplitter` type intentionally lives under
`OS.Std.Text`, not `System.String`, because it is an OS-specific byte-span
tokenizer for fixed ASCII manifests and protocols. It does not allocate
strings, decode UTF-8, quote/unescape values, normalize paths, or parse nested
formats. Its trim/remove-empty options are ASCII-only convenience behavior for
caller-owned token ranges and are not a `System.String.Split` compatibility
claim.

The current `OS.Std.Text.AsciiTextBuilder` type intentionally lives under
`OS.Std.Text`, not `System.Text.StringBuilder`, because it is an OS-specific
caller-owned byte-buffer formatter. It does not allocate storage, grow buffers,
encode Unicode, format floating-point values, format culture-specific text, or
provide the full mutable-object `StringBuilder` API. It is intended for panic,
serial, protocol, and manifest formatting paths that must know required byte
counts and avoid hidden allocation.

The current `OS.Std.Text.AsciiDecimalParser` type intentionally lives under
`OS.Std.Text`, not `System.Int32`, `System.Int64`, `System.UInt32`,
`System.UInt64`, or `System.Number`, because it is an OS-specific byte-span
parser for ASCII decimal text. The signed methods accept only a leading `-`;
there is no leading `+` convention, so `+` is rejected for both signed and
unsigned methods. It does not accept whitespace, thousands separators,
hexadecimal input, culture-specific digits, Unicode digits, floating-point
values, or partial token parsing.

The current `OS.Std.Text.AsciiHexParser` type intentionally lives under
`OS.Std.Text`, not `System.UInt32`, `System.UInt64`, or `System.Number`,
because it is an OS-specific byte-span parser for ASCII hexadecimal text. It
accepts only unsigned values and an optional `0x` or `0X` prefix. It does not
accept signs, whitespace, thousands separators, decimal-only culture-specific
digits, Unicode digits, floating-point values, or partial token parsing.

The current `OS.Std.Text.AsciiDecimalFormatter` type intentionally lives under
`OS.Std.Text`, not `System.UInt32`, `System.UInt64`, `System.Number`, or
`System.ISpanFormattable`, because it is an OS-specific byte-span formatter for
ASCII decimal text. It does not allocate strings, accept format strings, pad
output, format hexadecimal/floating-point values, or perform culture-specific
formatting.

The current `OS.Std.Binary.EndianPrimitives` type intentionally lives under `OS.Std.Binary`, not `System.Buffers.Binary`, because it is an OS-specific no-runtime helper. Direct read/write methods require spans of the correct minimum length; storage and network parsers should use the `TryRead`/`TryWrite` variants when malformed input must be status-coded instead of exception-driven.

The current `OS.Std.Hash.Crc32` type intentionally lives under `OS.Std.Hash`, not a canonical `System.*` namespace, because .NET does not expose this exact no-runtime contract as a BCL compatibility surface. It uses a compact bitwise loop rather than a static table so it can migrate toward freestanding NativeAOT without lazy static reference fields. Callers that need maximum throughput can add a separately reviewed table-driven variant later only after static-data and boot-time initialization rules are documented.

The current `OS.Std.Net.InternetChecksum` type intentionally lives under `OS.Std.Net`, not a canonical `System.*` namespace, because the public BCL does not expose this exact packet-checksum contract. It is a compact shared primitive for IPv4, ICMP, UDP, and TCP checksum work. This slice does not migrate existing packet code, does not add pseudo-header builders, and does not provide vectorized or table-driven acceleration.

The current `OS.Std.NativeInt` type intentionally lives under `OS.Std`, not a canonical `System.*` namespace, because it is an OS-specific status-coded helper for no-runtime pointer-sized arithmetic. It is not a replacement for `System.IntPtr`, `System.UIntPtr`, checked operators, or runtime pointer semantics. It does not expose signed arithmetic, pointer dereferencing, unsafe memory access, or page-table mutation.

The current `OS.Std.Math` type intentionally lives under `OS.Std`, not `System`, because only a partial integer subset is implemented. It does not expose floating-point helpers, decimal helpers, trigonometry, rounding, unsigned native integer overloads, or a complete `System.Math` / `System.MathF` compatibility surface. The supported throwing behavior is host-tested, but kernel paths that cannot rely on managed exception handling should not use throwing `Clamp` or overflow `Abs` paths until exception support is complete or status-coded alternatives are added.

The current `OS.Std.ArrayHelpers` type intentionally lives under `OS.Std`, not `System`, because only a partial unmanaged-span subset is implemented. It is not a replacement for `System.Array`, does not accept managed-reference element types, does not provide array allocation, resize, covariance, comparer-based search/sort, stable-sort guarantees beyond the current insertion-sort implementation, or multidimensional-array behavior, and does not validate whether binary-search inputs are sorted. `TrySort` and `TryBinarySearch` are OS-specific result metadata for no-throw range validation rather than BCL APIs.

The current `OS.Std.Memory.BufferOperations` type intentionally lives under
`OS.Std.Memory`, not `System.Buffer`, because only a partial status-coded
byte-copy subset is implemented. It does not provide `BlockCopy`, array
covariance handling, object-array behavior, unsafe pointer overloads, or a
complete BCL buffer contract.

The current EFI shell stubs are deliberately narrow and should not be generalized into kernel runtime policy without a matching design/test step. In particular, the shell type-cast helpers are fail-fast placeholders, not a correct interface-dispatch or exception implementation.

`NativeAppExecutableMemoryPlan` is not a NativeAOT runtime helper implementation. It documents future page layout and W^X policy for native app payload bytes only; it does not implement allocation helpers, write barriers, unwind support, GC root scanning, page-table updates, or instruction-cache maintenance.

`BootPageFreeListBuilder`, `BootPageAllocatorStateInitializer`,
`BootPageAllocatorMutation`, `BootPageAllocatorReadinessComposer`, and
`NativeAotHeapBootstrapPlanner` are not NativeAOT runtime helper
implementations. They write, validate, mutate, or compose caller-owned metadata
from a validated boot memory plan and can plan a future managed-heap page budget
from allocator-state metadata, but they do not allocate objects or arrays,
reserve live pages, register GC roots, run write barriers, install a managed
heap, mutate allocator-owned live state, or run in the live EFI path.

`NativeAotClassConstructorStateTable` and
`NativeAotClassConstructorRunnerPreflight` and
`NativeAotClassConstructorWorkItemPlanner` are not a full NativeAOT
class-constructor runner. They do not discover metadata, execute constructors,
call delegates, run module initializers, manage lazy static reference fields,
unwind exceptions, diagnose constructor bodies, or integrate with boot/runtime
helper exports. They only track caller-owned value-type state transitions,
validate already-supplied preflight metadata, turn already-supplied target
tokens into caller-owned work-item metadata, and format already-produced state,
preflight, and work-item plan results for future runner diagnostics.

`NativeAppInvocationPlanner` is not a NativeAOT runtime helper implementation. It documents host-tested metadata prerequisites for a future native app launcher only; it does not allocate stacks, switch stacks, create threads, define the final app ABI, map pages, relocate code, or invoke app code.

## Next Steps

1. Add host-safe `OS.Std.*` string, memory query, and remaining integer helper overloads where they do not collide with CoreLib.
2. Move all shared utilities into `std/no-runtime/shared` before use by kernel or apps.
3. Add a compatibility ledger for every `System.*` type implemented.
4. Migrate IPv4, ICMP, UDP, and TCP checksum paths onto `OS.Std.Net.InternetChecksum` where it reduces duplicated packet-local checksum loops.
5. Replace shell-only runtime placeholders with tested runtime helpers as the boot path starts using them.
6. Reserve heap pages from a live allocator state and define GC root/write-barrier ownership before changing object allocation helper status.
7. Add helper-specific QEMU probes before changing any panic-stubbed helper to successful behavior.
